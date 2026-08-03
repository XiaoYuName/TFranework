using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 贴图统一尺寸 / 对齐工具（UIToolkit）。
///
/// 解决「同一 UI 位置要轮换显示、但宽高不一致的图（如 GOOD! / BAD...）切换时视觉跳动」的问题：
///   1. 把多张图统一到同一张画布尺寸（默认取所有图的最大宽高）。
///   2. 在透明画布里拖动每张图的内容，使它们在视觉上对齐。
///   3. 导出为同尺寸的 PNG（多余区域透明），可覆盖原文件或加后缀另存。
///
/// 入口：菜单 Tools/2D/Texture Aligner 统一尺寸对齐。打开时会自动载入当前在 Project 选中的贴图。
/// 读写方式沿用 <see cref="TextureTrimmer"/>：直接读磁盘 PNG 字节，绕过导入设置的可读/压缩限制。
/// </summary>
public class TextureAlignerWindow : EditorWindow
{
    class Entry
    {
        public string assetPath;     // Assets/ 相对路径（原文件）
        public Texture2D tex;        // 解码出的可读副本（DontSave）
        public int w, h;
        public Vector2Int offset;    // 内容在画布中的「左下角」像素坐标（y 向上）
        public bool moved;           // 用户是否手动移动过（移动过则画布变化时不再自动居中）
        public VisualElement node;   // 预览节点
        public IntegerField fx, fy;  // 列表里的 X/Y 输入框（拖动时同步）
        public VisualElement row;    // 列表行（用于高亮）
    }

    enum CanvasMode { AutoMaxBounds, Manual }

    readonly List<Entry> entries = new();
    Entry selected;

    CanvasMode mode = CanvasMode.AutoMaxBounds;
    int canvasW, canvasH, padding;
    bool onion = true, overwrite;
    string suffix = "_Unified";
    float scale = 1f;

    const float PreviewMaxW = 520f, PreviewMaxH = 400f;

    VisualElement stage, previewBox, listRoot;
    IntegerField wField, hField;
    Label sizeLabel, emptyHint;
    static Texture2D checker;

    [MenuItem(EditorMenuSet.Texture2D + "/Texture Aligner")]
    static void Open() => TextureToolsHomeWindow.OpenTool("align");

    #region 构建 UI（UIToolkit，自定义布局）
    // 配色：弱化的中性面板，叠加在编辑器主题之上，明暗皮肤都协调
    static readonly Color Accent = new(0.30f, 0.62f, 0.95f);
    static readonly Color PanelBg = new(0.205f, 0.205f, 0.22f);
    static readonly Color RowBg = new(1f, 1f, 1f, 0.04f);
    static readonly Color StageBg = new(0.12f, 0.12f, 0.14f);

    void CreateGUI()
    {
        BuildView(rootVisualElement);
    }

    internal void BuildEmbedded(VisualElement host)
    {
        BuildView(host);
    }

    void BuildView(VisualElement root)
    {
        root.Clear();
        root.style.flexDirection = FlexDirection.Row;
        root.style.flexGrow = 1;
        root.style.minWidth = 0;
        root.style.backgroundColor = new Color(0.16f, 0.16f, 0.175f);

        // ===================== 左：预览舞台 =====================
        var left = new VisualElement { style = { flexGrow = 1, minWidth = 330, paddingLeft = 14, paddingRight = 10, paddingTop = 14, paddingBottom = 14 } };
        left.Add(Title("Texture Aligner", "统一尺寸 · 透明区对齐"));

        stage = new VisualElement
        {
            style =
            {
                flexGrow = 1, height = PreviewMaxH, marginTop = 8,
                justifyContent = Justify.Center, alignItems = Align.Center,
                backgroundColor = StageBg, overflow = Overflow.Hidden,
            }
        };
        Round(stage, 8);
        previewBox = new VisualElement { style = { position = Position.Relative } };
        emptyHint = new Label("从右侧添加贴图开始") { style = { color = new Color(1, 1, 1, 0.35f) } };
        stage.Add(previewBox);
        stage.Add(emptyHint);
        stage.RegisterCallback<GeometryChangedEvent>(_ => RefreshAll());
        left.Add(stage);

        sizeLabel = new Label { style = { marginTop = 8, fontSize = 11, color = new Color(1, 1, 1, 0.55f) } };
        left.Add(sizeLabel);
        left.Add(new Label("拖动选中的图对齐 · 点击图或列表切换选中") { style = { marginTop = 2, fontSize = 11, color = new Color(1, 1, 1, 0.35f) } });
        root.Add(left);

        // ===================== 右：控制面板 =====================
        var right = new ScrollView { style = { width = 330, flexShrink = 0, paddingLeft = 8, paddingRight = 12, paddingTop = 14, paddingBottom = 14, backgroundColor = new Color(0.145f, 0.145f, 0.16f) } };

        // —— 来源 ——
        var srcCard = Card("贴图来源");
        var add = new ObjectField("添加 PNG") { objectType = typeof(Texture2D), allowSceneObjects = false };
        add.RegisterValueChangedCallback(e =>
        {
            if (e.newValue is Texture2D t) AddTexture(AssetDatabase.GetAssetPath(t));
            add.SetValueWithoutNotify(null);
        });
        srcCard.Add(add);
        srcCard.Add(MkButton("导入 Project 选中的贴图", ImportSelection));
        right.Add(srcCard);

        // —— 画布尺寸 ——
        var canvasCard = Card("画布尺寸");
        var modeField = new EnumField("尺寸模式", mode);
        modeField.RegisterValueChangedCallback(e => { mode = (CanvasMode)e.newValue; ApplyCanvas(); });
        canvasCard.Add(modeField);

        var whRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        wField = new IntegerField("宽") { style = { flexGrow = 1, marginRight = 6 } };
        hField = new IntegerField("高") { style = { flexGrow = 1 } };
        wField.RegisterValueChangedCallback(e => { if (mode == CanvasMode.Manual) { canvasW = Mathf.Max(1, e.newValue); ApplyCanvas(); } });
        hField.RegisterValueChangedCallback(e => { if (mode == CanvasMode.Manual) { canvasH = Mathf.Max(1, e.newValue); ApplyCanvas(); } });
        whRow.Add(wField);
        whRow.Add(hField);
        canvasCard.Add(whRow);

        var padField = new IntegerField("四周边距(px)");
        padField.RegisterValueChangedCallback(e => { padding = Mathf.Max(0, e.newValue); ApplyCanvas(); });
        canvasCard.Add(padField);
        right.Add(canvasCard);

        // —— 对齐 ——
        var alignCard = Card("对齐");
        var onionT = new Toggle("洋葱皮：半透明叠加其余图") { value = onion };
        onionT.RegisterValueChangedCallback(e => { onion = e.newValue; RefreshStyles(); });
        alignCard.Add(onionT);
        var rowBtns = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 4 } };
        rowBtns.Add(MkButton("全部居中", () => { foreach (var e in entries) Center(e, true, true); RefreshAll(); }));
        rowBtns.Add(MkButton("选中水平居中", () => { if (selected != null) { Center(selected, true, false); RefreshAll(); } }));
        rowBtns.Add(MkButton("选中垂直居中", () => { if (selected != null) { Center(selected, false, true); RefreshAll(); } }));
        alignCard.Add(rowBtns);
        right.Add(alignCard);

        // —— 列表 ——
        var listCard = Card("贴图列表");
        listRoot = new VisualElement();
        listCard.Add(listRoot);
        right.Add(listCard);

        // —— 导出 ——
        var exportCard = Card("导出");
        var owT = new Toggle("覆盖原文件") { value = overwrite };
        var sfxField = new TextField("另存后缀") { value = suffix };
        owT.RegisterValueChangedCallback(e => { overwrite = e.newValue; sfxField.SetEnabled(!overwrite); });
        sfxField.RegisterValueChangedCallback(e => suffix = e.newValue);
        exportCard.Add(owT);
        exportCard.Add(sfxField);
        exportCard.Add(MkButton("导出统一尺寸 PNG", Export, true));
        right.Add(exportCard);

        root.Add(right);

        ImportSelection();
        ApplyCanvas();
    }

    // —— 样式小工具 ——
    static void Round(VisualElement e, float r)
        => e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

    static VisualElement Title(string main, string sub)
    {
        var box = new VisualElement();
        box.Add(new Label(main) { style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold } });
        box.Add(new Label(sub) { style = { fontSize = 11, color = new Color(1, 1, 1, 0.45f), marginTop = 1 } });
        return box;
    }

    static VisualElement Card(string title)
    {
        var c = new VisualElement
        {
            style = { marginBottom = 10, paddingTop = 8, paddingBottom = 10, paddingLeft = 10, paddingRight = 10, backgroundColor = PanelBg }
        };
        Round(c, 7);
        c.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 6, color = new Color(1, 1, 1, 0.85f) } });
        return c;
    }

    static Button MkButton(string text, System.Action onClick, bool primary = false)
    {
        var b = new Button(onClick) { text = text };
        var s = b.style;
        s.height = primary ? 32 : 24;
        s.marginRight = 6; s.marginTop = 2; s.marginBottom = 2; s.marginLeft = 0;
        s.paddingLeft = 10; s.paddingRight = 10;
        Round(b, 5);
        if (primary)
        {
            s.backgroundColor = Accent;
            s.color = Color.white;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.flexGrow = 1;
        }
        return b;
    }
    #endregion

    #region 数据增删
    void ImportSelection()
    {
        foreach (var o in Selection.objects)
            if (o is Texture2D t) AddTexture(AssetDatabase.GetAssetPath(t));
    }

    void AddTexture(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || Path.GetExtension(assetPath).ToLowerInvariant() != ".png")
        {
            if (!string.IsNullOrEmpty(assetPath)) Debug.LogWarning($"[TextureAligner] 仅支持 PNG，已跳过：{assetPath}");
            return;
        }
        if (entries.Exists(e => e.assetPath == assetPath)) return;

        var tex = LoadReadable(assetPath);
        if (tex == null) return;

        var entry = new Entry { assetPath = assetPath, tex = tex, w = tex.width, h = tex.height };
        entries.Add(entry);
        selected = entry;
        ApplyCanvas();   // 画布可能变大 → 重新计算 + 居中未移动项
    }

    void Remove(Entry e)
    {
        entries.Remove(e);
        if (e.tex != null) DestroyImmediate(e.tex);
        if (selected == e) selected = entries.Count > 0 ? entries[0] : null;
        ApplyCanvas();
    }

    static Texture2D LoadReadable(string assetPath)
    {
        byte[] raw = File.ReadAllBytes(Path.GetFullPath(assetPath));
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
        if (t.LoadImage(raw)) return t;
        Debug.LogError($"[TextureAligner] 无法解码：{assetPath}");
        DestroyImmediate(t);
        return null;
    }
    #endregion

    #region 画布与对齐
    void ApplyCanvas()
    {
        if (entries.Count == 0) { canvasW = canvasH = 0; }
        else if (mode == CanvasMode.AutoMaxBounds)
        {
            int mw = 0, mh = 0;
            foreach (var e in entries) { mw = Mathf.Max(mw, e.w); mh = Mathf.Max(mh, e.h); }
            canvasW = mw + padding * 2;
            canvasH = mh + padding * 2;
        }
        else
        {
            canvasW = Mathf.Max(1, canvasW);
            canvasH = Mathf.Max(1, canvasH);
        }

        wField.SetValueWithoutNotify(canvasW);
        hField.SetValueWithoutNotify(canvasH);
        wField.SetEnabled(mode == CanvasMode.Manual);
        hField.SetEnabled(mode == CanvasMode.Manual);

        // 未手动移动过的图：跟随画布自动居中
        foreach (var e in entries)
            if (!e.moved) Center(e, true, true);

        RefreshAll();
    }

    void Center(Entry e, bool horizontal, bool vertical)
    {
        if (horizontal) e.offset.x = (canvasW - e.w) / 2;
        if (vertical) e.offset.y = (canvasH - e.h) / 2;
        // 通过按钮显式居中也算一次「确定位置」，但不标记 moved，便于继续跟随画布
    }
    #endregion

    #region 预览渲染
    void RefreshAll()
    {
        BuildPreview();
        BuildList();
        RefreshStyles();
        sizeLabel.text = entries.Count == 0 ? "" : $"画布：{canvasW} × {canvasH}    显示缩放：{scale * 100f:0}%    共 {entries.Count} 张";
        emptyHint.style.display = entries.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void BuildPreview()
    {
        previewBox.Clear();
        if (entries.Count == 0 || canvasW <= 0 || canvasH <= 0) return;

        float previewWidth = stage != null && stage.contentRect.width > 10f ? stage.contentRect.width - 20f : PreviewMaxW;
        float previewHeight = stage != null && stage.contentRect.height > 10f ? stage.contentRect.height - 20f : PreviewMaxH;
        scale = Mathf.Min(previewWidth / canvasW, previewHeight / canvasH, 4f);
        previewBox.style.width = canvasW * scale;
        previewBox.style.height = canvasH * scale;
        previewBox.style.backgroundImage = new StyleBackground(Checker());
        previewBox.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);

        foreach (var e in entries)
        {
            var node = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    backgroundImage = new StyleBackground(e.tex),
                    backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100)),
                    borderTopWidth = 2, borderBottomWidth = 2, borderLeftWidth = 2, borderRightWidth = 2,
                }
            };
            e.node = node;
            LayoutEntry(e);
            AddDrag(e);
            previewBox.Add(node);
        }

        // 顶层参考线（中心十字），不拦截鼠标
        var guides = new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 } };
        var gc = new Color(1f, 1f, 1f, 0.25f);
        guides.Add(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, left = Length.Percent(50), top = 0, bottom = 0, width = 1, backgroundColor = gc } });
        guides.Add(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, top = Length.Percent(50), left = 0, right = 0, height = 1, backgroundColor = gc } });
        previewBox.Add(guides);
    }

    void LayoutEntry(Entry e)
    {
        e.node.style.left = e.offset.x * scale;
        e.node.style.top = (canvasH - e.h - e.offset.y) * scale;
        e.node.style.width = e.w * scale;
        e.node.style.height = e.h * scale;
    }

    void RefreshStyles()
    {
        foreach (var e in entries)
        {
            if (e.node == null) continue;
            bool sel = e == selected;
            e.node.style.display = (onion || sel) ? DisplayStyle.Flex : DisplayStyle.None;
            e.node.style.opacity = sel ? 1f : 0.4f;
            var bc = sel ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0, 0, 0, 0);
            e.node.style.borderTopColor = e.node.style.borderBottomColor = bc;
            e.node.style.borderLeftColor = e.node.style.borderRightColor = bc;
            if (e.row != null) e.row.style.backgroundColor = sel ? new Color(1f, 0.85f, 0.2f, 0.15f) : RowBg;
        }
    }

    void AddDrag(Entry e)
    {
        bool dragging = false;
        Vector2 startMouse = default;
        Vector2Int startOffset = default;

        e.node.RegisterCallback<PointerDownEvent>(ev =>
        {
            Select(e);
            dragging = true;
            startMouse = ev.position;
            startOffset = e.offset;
            e.node.CapturePointer(ev.pointerId);
            ev.StopPropagation();
        });
        e.node.RegisterCallback<PointerMoveEvent>(ev =>
        {
            if (!dragging) return;
            float dx = (ev.position.x - startMouse.x) / scale;
            float dy = (ev.position.y - startMouse.y) / scale;   // 屏幕 y 向下
            e.offset.x = startOffset.x + Mathf.RoundToInt(dx);
            e.offset.y = startOffset.y - Mathf.RoundToInt(dy);   // 画布 y 向上
            e.moved = true;
            LayoutEntry(e);
            e.fx?.SetValueWithoutNotify(e.offset.x);
            e.fy?.SetValueWithoutNotify(e.offset.y);
        });
        e.node.RegisterCallback<PointerUpEvent>(ev =>
        {
            if (!dragging) return;
            dragging = false;
            e.node.ReleasePointer(ev.pointerId);
        });
    }

    void Select(Entry e) { selected = e; RefreshStyles(); }
    #endregion

    #region 列表
    void BuildList()
    {
        listRoot.Clear();
        if (entries.Count == 0)
        {
            listRoot.Add(new Label("暂无贴图") { style = { fontSize = 11, color = new Color(1, 1, 1, 0.35f) } });
            return;
        }
        foreach (var e in entries)
        {
            var cap = e; // 闭包捕获
            var row = new VisualElement { style = { marginBottom = 5, paddingTop = 6, paddingBottom = 6, paddingLeft = 6, paddingRight = 6, backgroundColor = RowBg } };
            Round(row, 5);
            e.row = row;

            var top = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var thumb = new VisualElement { style = { width = 38, height = 38, marginRight = 8, backgroundColor = new Color(0.13f, 0.13f, 0.13f), backgroundImage = new StyleBackground(e.tex) } };
            thumb.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            Round(thumb, 4);
            top.Add(thumb);
            top.Add(new Label($"{Path.GetFileNameWithoutExtension(e.assetPath)}\n{e.w} × {e.h}") { style = { flexGrow = 1, fontSize = 11, whiteSpace = WhiteSpace.Normal } });
            var rm = MkButton("✕", () => Remove(cap));
            rm.style.flexGrow = 0;
            top.Add(rm);
            row.Add(top);

            var ctrl = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            e.fx = new IntegerField("X") { value = e.offset.x, style = { flexGrow = 1, marginRight = 6 } };
            e.fy = new IntegerField("Y") { value = e.offset.y, style = { flexGrow = 1, marginRight = 6 } };
            e.fx.RegisterValueChangedCallback(ev => { cap.offset.x = ev.newValue; cap.moved = true; LayoutEntry(cap); });
            e.fy.RegisterValueChangedCallback(ev => { cap.offset.y = ev.newValue; cap.moved = true; LayoutEntry(cap); });
            ctrl.Add(e.fx);
            ctrl.Add(e.fy);
            var centerBtn = MkButton("居中", () => { Center(cap, true, true); LayoutEntry(cap); cap.fx.SetValueWithoutNotify(cap.offset.x); cap.fy.SetValueWithoutNotify(cap.offset.y); });
            centerBtn.style.flexGrow = 0;
            ctrl.Add(centerBtn);
            row.Add(ctrl);

            row.RegisterCallback<PointerDownEvent>(_ => Select(cap));
            listRoot.Add(row);
        }
    }
    #endregion

    #region 导出
    void Export()
    {
        if (entries.Count == 0) { Debug.LogWarning("[TextureAligner] 没有可导出的贴图。"); return; }
        if (overwrite && !EditorUtility.DisplayDialog("覆盖原文件", $"将以 {canvasW}×{canvasH} 覆盖写入 {entries.Count} 个原始 PNG，无法撤销。继续？", "覆盖", "取消"))
            return;

        var written = new List<string>();
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var e in entries)
            {
                string outAsset = overwrite
                    ? e.assetPath
                    : Path.Combine(Path.GetDirectoryName(e.assetPath), Path.GetFileNameWithoutExtension(e.assetPath) + suffix + ".png").Replace('\\', '/');

                WriteUnified(e, outAsset);
                written.Add(outAsset);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            foreach (var p in written) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }
        Debug.Log($"[TextureAligner] 已导出 {written.Count} 张 {canvasW}×{canvasH} 的 PNG。");
    }

    void WriteUnified(Entry e, string outAsset)
    {
        var src = e.tex.GetPixels32();
        var dst = new Color32[canvasW * canvasH]; // 默认 (0,0,0,0) 全透明

        for (int y = 0; y < e.h; y++)
        {
            int dy = e.offset.y + y;
            if (dy < 0 || dy >= canvasH) continue;
            int srcRow = y * e.w;
            int dstRow = dy * canvasW;
            for (int x = 0; x < e.w; x++)
            {
                int dx = e.offset.x + x;
                if (dx < 0 || dx >= canvasW) continue;
                dst[dstRow + dx] = src[srcRow + x];
            }
        }

        var outTex = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false);
        try
        {
            outTex.SetPixels32(dst);
            outTex.Apply();
            File.WriteAllBytes(Path.GetFullPath(outAsset), outTex.EncodeToPNG());
        }
        finally { DestroyImmediate(outTex); }
    }
    #endregion

    static Texture2D Checker()
    {
        if (checker != null) return checker;
        const int s = 8;
        checker = new Texture2D(s * 2, s * 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point };
        var a = new Color32(64, 64, 64, 255);
        var b = new Color32(92, 92, 92, 255);
        for (int y = 0; y < s * 2; y++)
            for (int x = 0; x < s * 2; x++)
                checker.SetPixel(x, y, (x / s + y / s) % 2 == 0 ? a : b);
        checker.Apply();
        return checker;
    }

    void OnDisable()
    {
        foreach (var e in entries)
            if (e.tex != null) DestroyImmediate(e.tex);
        entries.Clear();
    }
}
