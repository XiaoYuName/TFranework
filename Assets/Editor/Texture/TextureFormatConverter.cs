using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements; // ObjectField / EnumField
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 把某个文件夹内的所有图片素材统一成 PNG 或 JPG。
///
/// 注意：不是简单改后缀（那样会得到无效文件），而是读原图字节解码后按目标格式重新编码。
/// 默认把原图的 .meta 一并改名到新文件，从而保留 GUID 与导入设置，避免场景/预制体上的引用丢失。
///
/// 用法：菜单 Tools/2D/Texture Format Converter 打开窗口，拖入文件夹、选目标格式、点转换。
///
/// 已知限制：
///   - JPG 不含 Alpha，PNG→JPG 会丢失透明通道（窗口会提示）。
///   - 仅处理源为 .png / .jpg / .jpeg 的图片。
/// </summary>
public static class TextureFormatConverter
{
    public enum TargetFormat { PNG, JPG }

    static readonly string[] SourceExtensions = { ".png", ".jpg", ".jpeg" };

    public struct Options
    {
        public TargetFormat format;
        public bool recursive;     // 是否递归子文件夹
        public int jpgQuality;     // JPG 编码质量 1..100（PNG 无损，忽略）
        public bool preserveGuid;  // 改后缀时把 .meta 一并改名以保留 GUID/引用

        public static Options Default => new()
        {
            format = TargetFormat.PNG,
            recursive = true,
            jpgQuality = 90,
            preserveGuid = true,
        };
    }

    public struct Result
    {
        public int converted;  // 实际重编码并改名的数量
        public int skipped;    // 已是目标格式、跳过
        public int failed;     // 解码/写入失败
        public List<string> messages;   // 汇总/跳过提示（一般信息）
        public List<FailureEntry> failures; // 具体失败的文件及原因
    }

    public struct FailureEntry
    {
        public string path;
        public string reason;
    }

    enum ConvertStatus { Converted, SkippedExists, Failed }

    static string TargetExtension(TargetFormat f) => f == TargetFormat.PNG ? ".png" : ".jpg";

    static bool AlreadyTarget(string ext, TargetFormat f)
    {
        ext = ext.ToLowerInvariant();
        return f == TargetFormat.PNG ? ext == ".png" : (ext == ".jpg" || ext == ".jpeg");
    }

    /// <summary>收集文件夹（asset 相对路径，如 "Assets/Foo"）下的所有图片资源路径。</summary>
    public static List<string> CollectImages(string folderAssetPath, bool recursive)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(folderAssetPath) || !Directory.Exists(folderAssetPath))
            return list;

        var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var path in Directory.GetFiles(folderAssetPath, "*.*", opt))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            foreach (var s in SourceExtensions)
                if (ext == s)
                {
                    list.Add(path.Replace('\\', '/'));
                    break;
                }
        }
        return list;
    }

    /// <summary>转换整个文件夹，返回统计结果。</summary>
    public static Result Convert(string folderAssetPath, Options options)
    {
        var result = new Result { messages = new List<string>(), failures = new List<FailureEntry>() };

        var images = CollectImages(folderAssetPath, options.recursive);
        if (images.Count == 0)
        {
            result.messages.Add($"文件夹内未找到图片：{folderAssetPath}");
            return result;
        }

        string targetExt = TargetExtension(options.format);

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < images.Count; i++)
            {
                string oldPath = images[i];
                EditorUtility.DisplayProgressBar("Texture Format Converter",
                    Path.GetFileName(oldPath), (float)i / images.Count);

                if (AlreadyTarget(Path.GetExtension(oldPath), options.format))
                {
                    result.skipped++;
                    continue;
                }

                switch (ConvertOne(oldPath, targetExt, options, out string reason))
                {
                    case ConvertStatus.Converted:
                        result.converted++;
                        break;
                    case ConvertStatus.SkippedExists:
                        result.skipped++;
                        result.messages.Add($"跳过（{reason}）：{oldPath}");
                        break;
                    case ConvertStatus.Failed:
                        result.failed++;
                        result.failures.Add(new FailureEntry { path = oldPath, reason = reason });
                        break;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        result.messages.Insert(0,
            $"完成：转换 {result.converted}，跳过(已是目标格式) {result.skipped}，失败 {result.failed}。");
        return result;
    }

    static ConvertStatus ConvertOne(string oldPath, string targetExt, Options options, out string reason)
    {
        reason = null;
        string newPath = Path.ChangeExtension(oldPath, targetExt).Replace('\\', '/');

        // 目标文件已存在且不是原文件本身 → 不覆盖，避免误删。
        if (!PathEquals(oldPath, newPath) && File.Exists(newPath))
        {
            reason = $"目标已存在：{newPath}";
            return ConvertStatus.SkippedExists;
        }

        Texture2D tex = null;
        try
        {
            byte[] src = File.ReadAllBytes(oldPath);
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(src)) // ImageConversion：解码 PNG/JPG
            {
                reason = "解码失败（不是有效的图片文件，或格式不受支持）";
                return ConvertStatus.Failed;
            }

            byte[] encoded = options.format == TargetFormat.PNG
                ? tex.EncodeToPNG()
                : tex.EncodeToJPG(Mathf.Clamp(options.jpgQuality, 1, 100));

            File.WriteAllBytes(newPath, encoded);

            if (!PathEquals(oldPath, newPath))
            {
                string oldMeta = oldPath + ".meta";
                string newMeta = newPath + ".meta";
                if (options.preserveGuid && File.Exists(oldMeta) && !File.Exists(newMeta))
                    File.Move(oldMeta, newMeta); // 保留 GUID 与导入设置
                else if (File.Exists(oldMeta))
                    File.Delete(oldMeta);

                File.Delete(oldPath);
            }
            return ConvertStatus.Converted;
        }
        catch (System.Exception e)
        {
            reason = $"{e.GetType().Name}: {e.Message}";
            return ConvertStatus.Failed;
        }
        finally
        {
            if (tex != null) Object.DestroyImmediate(tex);
        }
    }

    static bool PathEquals(string a, string b) =>
        string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), System.StringComparison.OrdinalIgnoreCase);
}

public class TextureFormatConverterWindow : EditorWindow
{
    Object folder;
    EnumField format;
    Toggle recursive;
    SliderInt jpgQuality;
    Toggle preserveGuid;
    HelpBox alphaWarn;
    Label summary;
    Button convertBtn;

    [MenuItem(EditorMenuSet.Texture2D + "/Texture Format Converter")]
    static void Open()
    {
        TextureToolsHomeWindow.OpenTool("convert");
    }

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
        root.style.flexGrow = 1f;
        root.style.backgroundColor = new Color(0.16f, 0.16f, 0.175f);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1f;
        SetPadding(scroll, 18f);
        root.Add(scroll);

        var content = new VisualElement();
        content.style.width = Length.Percent(100f);
        content.style.maxWidth = 680f;
        content.style.alignSelf = Align.Center;
        scroll.Add(content);

        var title = new Label("图片格式转换");
        title.style.fontSize = 20f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        content.Add(title);
        var subtitle = new Label("批量统一文件夹内的 PNG/JPG，同时保护 Meta GUID 与资源引用。");
        subtitle.style.marginTop = 3f;
        subtitle.style.marginBottom = 14f;
        subtitle.style.color = new Color(1f, 1f, 1f, 0.55f);
        content.Add(subtitle);

        var card = new VisualElement();
        SetPadding(card, 14f);
        card.style.backgroundColor = new Color(0.205f, 0.205f, 0.22f);
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 7f;
        card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 7f;
        content.Add(card);

        card.Add(new HelpBox(
            "把文件夹内所有图片统一成 PNG 或 JPG。会重新编码图片（非简单改后缀），\n" +
            "并把 .meta 一并改名以保留 GUID/引用。仅处理 .png / .jpg / .jpeg。",
            HelpBoxMessageType.Info));

        folder = DefaultFolderFromSelection();
        var folderField = new ObjectField("目标文件夹")
        {
            objectType = typeof(DefaultAsset),
            value = folder,
            tooltip = "拖入 Project 里的文件夹，或先在 Project 选中文件夹再打开本窗口",
        };
        folderField.RegisterValueChangedCallback(e => { folder = e.newValue; Refresh(); });
        card.Add(folderField);

        format = new EnumField("目标格式", TextureFormatConverter.TargetFormat.PNG);
        format.RegisterValueChangedCallback(_ => Refresh());
        card.Add(format);

        recursive = new Toggle("递归子文件夹") { value = true };
        recursive.RegisterValueChangedCallback(_ => Refresh());
        card.Add(recursive);

        jpgQuality = new SliderInt("JPG 质量", 1, 100) { value = 90, showInputField = true };
        card.Add(jpgQuality);

        preserveGuid = new Toggle("保留 GUID（改名 .meta）")
        {
            value = true,
            tooltip = "关闭则新文件会分配新 GUID，原有引用会断开",
        };
        card.Add(preserveGuid);

        alphaWarn = new HelpBox("JPG 不支持透明通道，PNG→JPG 会丢失 Alpha。", HelpBoxMessageType.Warning);
        card.Add(alphaWarn);

        card.Add(Divider());
        summary = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6f } };
        card.Add(summary);

        convertBtn = new Button(Apply) { style = { height = 32f } };
        card.Add(convertBtn);

        Selection.selectionChanged -= OnSelectionChanged;
        Selection.selectionChanged += OnSelectionChanged;
        Refresh();
    }

    void OnDisable() => Selection.selectionChanged -= OnSelectionChanged;

    void OnSelectionChanged()
    {
        // 若用户还没指定文件夹，跟随 Project 里选中的文件夹。
        if (folder == null)
        {
            var f = DefaultFolderFromSelection();
            if (f != null) { folder = f; Refresh(); }
        }
    }

    static Object DefaultFolderFromSelection()
    {
        foreach (var obj in Selection.objects)
        {
            var p = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p))
                return obj;
        }
        return null;
    }

    TextureFormatConverter.Options CurrentOptions() => new()
    {
        format = (TextureFormatConverter.TargetFormat)format.value,
        recursive = recursive.value,
        jpgQuality = jpgQuality.value,
        preserveGuid = preserveGuid.value,
    };

    string FolderPath() => folder != null ? AssetDatabase.GetAssetPath(folder) : null;

    void Refresh()
    {
        var fmt = (TextureFormatConverter.TargetFormat)format.value;
        bool isJpg = fmt == TextureFormatConverter.TargetFormat.JPG;
        jpgQuality.style.display = isJpg ? DisplayStyle.Flex : DisplayStyle.None;
        alphaWarn.style.display = isJpg ? DisplayStyle.Flex : DisplayStyle.None;

        string path = FolderPath();
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
        {
            summary.text = "请指定一个有效的文件夹。";
            convertBtn.SetEnabled(false);
            convertBtn.text = "转换（未选文件夹）";
            return;
        }

        var images = TextureFormatConverter.CollectImages(path, recursive.value);
        int need = 0;
        foreach (var img in images)
        {
            var ext = Path.GetExtension(img).ToLowerInvariant();
            bool already = isJpg ? (ext == ".jpg" || ext == ".jpeg") : ext == ".png";
            if (!already) need++;
        }

        summary.text = $"{path}\n共 {images.Count} 张图片，需转换 {need} 张（其余已是目标格式）。";
        convertBtn.SetEnabled(images.Count > 0);
        convertBtn.text = need > 0 ? $"转换 {need} 张为 {fmt}" : "无需转换（都已是目标格式）";
    }

    void Apply()
    {
        string path = FolderPath();
        if (string.IsNullOrEmpty(path)) return;

        if (!EditorUtility.DisplayDialog("图片格式统一",
                $"将把\n{path}\n下的图片统一转换为 {format.value}，此操作会覆盖原图。是否继续？",
                "转换", "取消"))
            return;

        var r = TextureFormatConverter.Convert(path, CurrentOptions());
        foreach (var m in r.messages) Debug.Log($"[TextureFormatConverter] {m}");
        foreach (var f in r.failures)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(f.path);
            Debug.LogError($"[TextureFormatConverter] 转换失败：{f.path} — {f.reason}", asset);
        }
        Refresh();
    }

    static VisualElement Divider() => new()
    {
        style = { height = 1f, marginTop = 8f, marginBottom = 8f, backgroundColor = new Color(1f, 1f, 1f, 0.08f) },
    };

    static void SetPadding(VisualElement e, float p)
    {
        e.style.paddingTop = p; e.style.paddingBottom = p;
        e.style.paddingLeft = p; e.style.paddingRight = p;
    }
}
