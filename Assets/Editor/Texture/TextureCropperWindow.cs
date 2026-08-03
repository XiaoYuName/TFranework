using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 可视化图片裁剪工具。源图片直接从磁盘读取，因此不要求开启 Read/Write。
/// 像素坐标以图片左下角为原点，与 Texture2D 的像素坐标保持一致。
/// </summary>
public class TextureCropperWindow : EditorWindow
{
    private const float MinZoom = 0.02f;
    private const float MaxZoom = 32f;
    private const float StagePadding = 36f;
    private const float HandleSize = 10f;
    private const float HandleHitSize = 16f;
    private const int MinCropPixels = 1;
    private const int PreviewMaxDimension = 512;

    private enum AspectMode
    {
        Free,
        Original,
        Square,
        Ratio4By3,
        Ratio16By9,
        Ratio9By16,
        Custom
    }

    private enum DragMode
    {
        None,
        MoveCrop,
        PanCanvas,
        Resize
    }

    private enum HandleDirection
    {
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>供项目内其他编辑器模块向右侧面板追加控件。</summary>
    public static event Action<TextureCropperWindow, VisualElement> ExtendInspector;

    public string SourceAssetPath => assetPath;
    public Vector2Int SourceSize => sourceSize;
    public RectInt CropRect => cropRect;

    private string assetPath;
    private Texture2D sourceTexture;
    private Color32[] sourcePixels;
    private Vector2Int sourceSize;
    private RectInt cropRect;

    private float zoom = 1f;
    private Vector2 panOffset;
    private AspectMode aspectMode = AspectMode.Free;
    private float customAspect = 1f;
    private bool clampToImage = true;
    private bool showGrid = true;
    private bool showPreview = true;

    private DragMode dragMode;
    private HandleDirection activeHandle;
    private Vector2 dragStartMouse;
    private RectInt dragStartCrop;
    private Vector2 dragStartPan;
    private bool spaceHeld;
    private bool fittedForCurrentTexture;

    private VisualElement stage;
    private VisualElement imageLayer;
    private VisualElement shadeLayer;
    private VisualElement cropFrame;
    private VisualElement guidesLayer;
    private VisualElement handlesLayer;
    private readonly VisualElement[] handles = new VisualElement[8];
    private VisualElement previewBox;
    private VisualElement previewImage;
    private Texture2D previewTexture;
    private RectInt previewedCropRect;
    private Label emptyHint;
    private Label infoLabel;
    private IntegerField cropX;
    private IntegerField cropY;
    private IntegerField cropWidth;
    private IntegerField cropHeight;
    private EnumField aspectField;
    private FloatField customAspectField;
    private Button overwriteButton;
    private Button saveAsButton;
    private EditorWindow notificationOwner;

    [MenuItem(EditorMenuSet.Texture2D + "/Texture Cropper")]
    private static void Open()
    {
        TextureToolsHomeWindow.OpenTool("crop");
    }

    private void CreateGUI()
    {
        BuildView(rootVisualElement, this);
    }

    /// <summary>将裁剪器作为模块装入 Texture Tools 的页面容器。</summary>
    internal void BuildEmbedded(VisualElement host, EditorWindow owner)
    {
        BuildView(host, owner);
    }

    private void BuildView(VisualElement root, EditorWindow owner)
    {
        notificationOwner = owner;
        root.Clear();
        root.style.flexDirection = FlexDirection.Row;
        root.style.flexGrow = 1f;
        root.style.minWidth = 0f;

        VisualElement workspace = BuildWorkspace();
        ScrollView inspector = BuildInspector();
        root.Add(workspace);
        root.Add(inspector);

        CreateHandles();
        RegisterStageEvents();
        RefreshAll();
        LoadSelectionIfAvailable();
    }

    private VisualElement BuildWorkspace()
    {
        VisualElement workspace = new VisualElement();
        workspace.style.flexGrow = 1f;
        workspace.style.minWidth = 360f;
        workspace.style.flexDirection = FlexDirection.Column;
        workspace.style.paddingLeft = 14f;
        workspace.style.paddingRight = 8f;
        workspace.style.paddingTop = 14f;
        workspace.style.paddingBottom = 10f;
        workspace.style.backgroundColor = new Color(0.16f, 0.16f, 0.175f);

        VisualElement header = BuildRow();
        header.style.alignItems = Align.FlexStart;
        VisualElement heading = BuildTitle("Texture Cropper", "拖动裁剪 · 实时预览 · 像素级导出");
        heading.style.flexGrow = 1f;
        header.Add(heading);
        workspace.Add(header);

        stage = new VisualElement();
        stage.name = "crop-stage";
        stage.focusable = true;
        stage.style.flexGrow = 1f;
        stage.style.marginTop = 10f;
        stage.style.overflow = Overflow.Hidden;
        stage.style.backgroundColor = new Color(0.085f, 0.085f, 0.095f);
        SetRadius(stage, 7f);

        imageLayer = AbsoluteLayer();
        imageLayer.pickingMode = PickingMode.Ignore;
        stage.Add(imageLayer);

        shadeLayer = AbsoluteFillLayer();
        shadeLayer.pickingMode = PickingMode.Ignore;
        stage.Add(shadeLayer);

        cropFrame = AbsoluteLayer();
        cropFrame.pickingMode = PickingMode.Ignore;
        SetBorder(cropFrame, 2f, Color.white);
        stage.Add(cropFrame);

        guidesLayer = AbsoluteFillLayer();
        guidesLayer.pickingMode = PickingMode.Ignore;
        stage.Add(guidesLayer);

        handlesLayer = AbsoluteFillLayer();
        handlesLayer.pickingMode = PickingMode.Position;
        stage.Add(handlesLayer);

        emptyHint = new Label("从右侧选择图片，或在 Project 中选中图片后点击“载入选中项”");
        emptyHint.pickingMode = PickingMode.Ignore;
        emptyHint.style.position = Position.Absolute;
        emptyHint.style.left = 20f;
        emptyHint.style.right = 20f;
        emptyHint.style.top = Length.Percent(48f);
        emptyHint.style.unityTextAlign = TextAnchor.MiddleCenter;
        emptyHint.style.whiteSpace = WhiteSpace.Normal;
        emptyHint.style.color = new Color(1f, 1f, 1f, 0.4f);
        stage.Add(emptyHint);
        workspace.Add(stage);

        infoLabel = new Label();
        infoLabel.style.marginTop = 7f;
        infoLabel.style.fontSize = 11f;
        infoLabel.style.color = new Color(1f, 1f, 1f, 0.6f);
        workspace.Add(infoLabel);

        Label help = new Label("滚轮缩放  |  中键/Alt/空格拖动平移  |  拖动框内移动  |  Shift 临时锁定当前比例  |  F 适配  |  R 重置");
        help.style.marginTop = 2f;
        help.style.fontSize = 10f;
        help.style.whiteSpace = WhiteSpace.Normal;
        help.style.color = new Color(1f, 1f, 1f, 0.35f);
        workspace.Add(help);
        return workspace;
    }

    private ScrollView BuildInspector()
    {
        ScrollView inspector = new ScrollView(ScrollViewMode.Vertical);
        inspector.name = "crop-inspector";
        inspector.style.width = 330f;
        inspector.style.flexShrink = 0f;
        inspector.style.paddingLeft = 8f;
        inspector.style.paddingRight = 14f;
        inspector.style.paddingTop = 14f;
        inspector.style.paddingBottom = 14f;
        inspector.style.backgroundColor = new Color(0.145f, 0.145f, 0.16f);

        VisualElement sourceCard = BuildCard("图片来源");
        ObjectField objectField = new ObjectField("图片")
        {
            objectType = typeof(Texture2D),
            allowSceneObjects = false
        };
        objectField.RegisterValueChangedCallback(evt =>
        {
            Texture2D texture = evt.newValue as Texture2D;
            if (texture != null)
                LoadTexture(AssetDatabase.GetAssetPath(texture));
            objectField.SetValueWithoutNotify(null);
        });
        sourceCard.Add(objectField);
        sourceCard.Add(BuildButton("载入 Project 选中图片", LoadSelection, false));
        inspector.Add(sourceCard);

        VisualElement cropCard = BuildCard("裁剪设置");
        aspectField = new EnumField("比例", aspectMode);
        aspectField.RegisterValueChangedCallback(evt =>
        {
            aspectMode = (AspectMode)evt.newValue;
            customAspectField.style.display = aspectMode == AspectMode.Custom ? DisplayStyle.Flex : DisplayStyle.None;
            ApplyAspectToCurrentCrop();
        });
        cropCard.Add(aspectField);

        customAspectField = new FloatField("自定义宽高比") { value = customAspect };
        customAspectField.style.display = DisplayStyle.None;
        customAspectField.RegisterValueChangedCallback(evt =>
        {
            customAspect = Mathf.Max(0.01f, evt.newValue);
            customAspectField.SetValueWithoutNotify(customAspect);
            if (aspectMode == AspectMode.Custom)
                ApplyAspectToCurrentCrop();
        });
        cropCard.Add(customAspectField);

        VisualElement xyRow = BuildRow();
        xyRow.Add(BuildIntegerControl("X", out cropX));
        xyRow.Add(BuildIntegerControl("Y", out cropY));
        cropCard.Add(xyRow);

        VisualElement sizeRow = BuildRow();
        sizeRow.Add(BuildIntegerControl("W", out cropWidth));
        sizeRow.Add(BuildIntegerControl("H", out cropHeight));
        cropCard.Add(sizeRow);

        cropX.RegisterValueChangedCallback(_ => SetCropFromFields(true));
        cropY.RegisterValueChangedCallback(_ => SetCropFromFields(true));
        cropWidth.RegisterValueChangedCallback(_ => SetCropFromFields(true));
        cropHeight.RegisterValueChangedCallback(_ => SetCropFromFields(false));

        VisualElement quickRow = BuildRow();
        quickRow.style.marginTop = 5f;
        quickRow.Add(BuildButton("全图", ResetCrop, false));
        quickRow.Add(BuildButton("居中", CenterCrop, false));
        quickRow.Add(BuildButton("适配视图", FitToStage, false));
        cropCard.Add(quickRow);

        Toggle clampToggle = new Toggle("限制在图片范围内") { value = clampToImage };
        clampToggle.tooltip = "关闭后，图片之外的区域会以透明像素导出。";
        clampToggle.RegisterValueChangedCallback(evt =>
        {
            clampToImage = evt.newValue;
            NormalizeCrop();
            RefreshAll();
        });
        cropCard.Add(clampToggle);

        Toggle gridToggle = new Toggle("显示三分线") { value = showGrid };
        gridToggle.RegisterValueChangedCallback(evt =>
        {
            showGrid = evt.newValue;
            RefreshGuides();
        });
        cropCard.Add(gridToggle);
        inspector.Add(cropCard);

        VisualElement previewCard = BuildCard("实时预览");
        Toggle previewToggle = new Toggle("启用预览") { value = showPreview };
        previewToggle.RegisterValueChangedCallback(evt =>
        {
            showPreview = evt.newValue;
            RefreshPreview();
        });
        previewCard.Add(previewToggle);

        previewBox = new VisualElement();
        previewBox.style.height = 250f;
        previewBox.style.marginTop = 7f;
        previewBox.style.overflow = Overflow.Hidden;
        previewBox.style.justifyContent = Justify.Center;
        previewBox.style.alignItems = Align.Center;
        previewBox.style.backgroundColor = new Color(0.065f, 0.065f, 0.075f);
        SetRadius(previewBox, 5f);
        previewImage = new VisualElement();
        previewImage.pickingMode = PickingMode.Ignore;
        previewBox.Add(previewImage);
        previewCard.Add(previewBox);
        inspector.Add(previewCard);

        VisualElement exportCard = BuildCard("导出");
        overwriteButton = BuildButton("覆盖原图片", ExportOverwrite, true);
        saveAsButton = BuildButton("另存为 PNG…", ExportSaveAs, false);
        exportCard.Add(overwriteButton);
        exportCard.Add(saveAsButton);
        inspector.Add(exportCard);

        VisualElement extensionHost = new VisualElement { name = "texture-cropper-extensions" };
        inspector.Add(extensionHost);
        ExtendInspector?.Invoke(this, extensionHost);
        return inspector;
    }

    private static VisualElement BuildTitle(string title, string subtitle)
    {
        VisualElement box = new VisualElement();
        Label main = new Label(title);
        main.style.fontSize = 16f;
        main.style.unityFontStyleAndWeight = FontStyle.Bold;
        Label sub = new Label(subtitle);
        sub.style.fontSize = 11f;
        sub.style.marginTop = 1f;
        sub.style.color = new Color(1f, 1f, 1f, 0.45f);
        box.Add(main);
        box.Add(sub);
        return box;
    }

    private static VisualElement BuildCard(string title)
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = 10f;
        card.style.paddingLeft = 10f;
        card.style.paddingRight = 10f;
        card.style.paddingTop = 8f;
        card.style.paddingBottom = 9f;
        card.style.backgroundColor = new Color(0.205f, 0.205f, 0.22f);
        SetRadius(card, 6f);
        Label label = new Label(title);
        label.style.fontSize = 12f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 6f;
        label.style.color = new Color(1f, 1f, 1f, 0.85f);
        card.Add(label);
        return card;
    }

    private static VisualElement BuildRow()
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        return row;
    }

    private static VisualElement BuildIntegerControl(string labelText, out IntegerField field)
    {
        VisualElement control = new VisualElement();
        control.style.flexDirection = FlexDirection.Row;
        control.style.alignItems = Align.Center;
        control.style.flexGrow = 1f;
        control.style.flexBasis = 0f;
        control.style.minWidth = 0f;
        control.style.marginRight = 6f;

        Label label = new Label(labelText);
        label.style.width = 20f;
        label.style.minWidth = 20f;
        label.style.flexShrink = 0f;

        field = new IntegerField();
        field.style.flexGrow = 1f;
        field.style.flexBasis = 0f;
        field.style.minWidth = 48f;
        control.Add(label);
        control.Add(field);
        return control;
    }

    private static Button BuildButton(string text, Action callback, bool primary)
    {
        Button button = new Button(callback) { text = text };
        button.style.height = primary ? 31f : 25f;
        button.style.flexGrow = 1f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 5f;
        button.style.marginTop = 2f;
        button.style.marginBottom = 2f;
        SetRadius(button, 4f);
        if (primary)
        {
            button.style.backgroundColor = new Color(0.22f, 0.52f, 0.88f);
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        return button;
    }

    private static VisualElement AbsoluteLayer()
    {
        VisualElement layer = new VisualElement();
        layer.style.position = Position.Absolute;
        return layer;
    }

    private static VisualElement AbsoluteFillLayer()
    {
        VisualElement layer = AbsoluteLayer();
        layer.style.left = 0f;
        layer.style.right = 0f;
        layer.style.top = 0f;
        layer.style.bottom = 0f;
        return layer;
    }

    private static void SetRadius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }

    private static void SetBorder(VisualElement element, float width, Color color)
    {
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
    }

    private void RegisterStageEvents()
    {
        stage.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
        // 使用冒泡阶段，让裁剪手柄优先处理按下事件。
        stage.RegisterCallback<PointerDownEvent>(OnPointerDown);
        stage.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        stage.RegisterCallback<PointerUpEvent>(OnPointerUp);
        stage.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
        stage.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        stage.RegisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);
        stage.RegisterCallback<GeometryChangedEvent>(OnStageGeometryChanged);
    }

    private void CreateHandles()
    {
        for (int i = 0; i < handles.Length; i++)
        {
            int index = i;
            VisualElement handle = new VisualElement();
            handle.style.position = Position.Absolute;
            handle.style.alignItems = Align.Center;
            handle.style.justifyContent = Justify.Center;
            handle.style.backgroundColor = Color.clear;
            handle.style.cursor = CursorForHandle((HandleDirection)index);
            handle.pickingMode = PickingMode.Position;

            VisualElement marker = new VisualElement();
            marker.style.width = HandleSize;
            marker.style.height = HandleSize;
            marker.style.flexShrink = 0f;
            marker.style.backgroundColor = Color.white;
            marker.pickingMode = PickingMode.Ignore;
            SetBorder(marker, 1f, Color.black);
            handle.Add(marker);

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || sourceTexture == null)
                    return;
                BeginResize((HandleDirection)index, evt.position);
                stage.CapturePointer(evt.pointerId);
                evt.StopImmediatePropagation();
            });
            handles[index] = handle;
            handlesLayer.Add(handle);
        }
    }

    private static StyleCursor CursorForHandle(HandleDirection direction)
    {
        switch (direction)
        {
            case HandleDirection.Left:
            case HandleDirection.Right:
                return MakeCursor(MouseCursor.ResizeHorizontal);
            case HandleDirection.Top:
            case HandleDirection.Bottom:
                return MakeCursor(MouseCursor.ResizeVertical);
            case HandleDirection.TopLeft:
            case HandleDirection.BottomRight:
                return MakeCursor(MouseCursor.ResizeUpLeft);
            default:
                return MakeCursor(MouseCursor.ResizeUpRight);
        }
    }

    // UI Toolkit 没有公开的系统光标 ID 设置入口，使用 Unity 编辑器内部字段映射 MouseCursor。
    private static StyleCursor MakeCursor(MouseCursor cursor)
    {
        try
        {
            object boxed = new UnityEngine.UIElements.Cursor();
            typeof(UnityEngine.UIElements.Cursor)
                .GetProperty("defaultCursorId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(boxed, (int)cursor);
            return new StyleCursor((UnityEngine.UIElements.Cursor)boxed);
        }
        catch
        {
            return new StyleCursor(StyleKeyword.Null);
        }
    }

    private void LoadSelectionIfAvailable()
    {
        if (sourceTexture == null)
            LoadSelection(false);
    }

    private void LoadSelection()
    {
        LoadSelection(true);
    }

    private void LoadSelection(bool warnWhenMissing)
    {
        foreach (UnityEngine.Object selected in Selection.objects)
        {
            Texture2D texture = selected as Texture2D;
            if (texture == null)
                continue;
            LoadTexture(AssetDatabase.GetAssetPath(texture));
            return;
        }

        if (warnWhenMissing)
            Notify("请先在 Project 中选择一张图片");
    }

    private void LoadTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
        {
            EditorUtility.DisplayDialog("无法载入", "目前支持 PNG、JPG 和 JPEG 图片。", "确定");
            return;
        }

        Texture2D loaded = LoadReadableTexture(path);
        if (loaded == null)
            return;

        DisposeSourceTexture();
        assetPath = path.Replace('\\', '/');
        sourceTexture = loaded;
        sourcePixels = sourceTexture.GetPixels32();
        sourceSize = new Vector2Int(sourceTexture.width, sourceTexture.height);
        imageLayer.style.backgroundImage = new StyleBackground(sourceTexture);
        imageLayer.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
        cropRect = new RectInt(0, 0, sourceSize.x, sourceSize.y);
        fittedForCurrentTexture = false;
        ApplyAspectToCurrentCrop();
        FitToStage();
        RefreshAll();
        Repaint();
    }

    private static Texture2D LoadReadableTexture(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(path));
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                name = Path.GetFileNameWithoutExtension(path) + " (Crop Preview)"
            };
            if (texture.LoadImage(bytes, false))
                return texture;
            DestroyImmediate(texture);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        EditorUtility.DisplayDialog("无法载入", "图片文件无法读取或解码。", "确定");
        return null;
    }

    private float? ActiveAspect
    {
        get
        {
            switch (aspectMode)
            {
                case AspectMode.Original: return sourceSize.y > 0 ? sourceSize.x / (float)sourceSize.y : 1f;
                case AspectMode.Square: return 1f;
                case AspectMode.Ratio4By3: return 4f / 3f;
                case AspectMode.Ratio16By9: return 16f / 9f;
                case AspectMode.Ratio9By16: return 9f / 16f;
                case AspectMode.Custom: return Mathf.Max(0.01f, customAspect);
                default: return null;
            }
        }
    }

    private void ResetCrop()
    {
        if (sourceTexture == null)
            return;
        cropRect = new RectInt(0, 0, sourceSize.x, sourceSize.y);
        ApplyAspectToCurrentCrop();
        RefreshAll();
    }

    private void CenterCrop()
    {
        if (sourceTexture == null)
            return;
        cropRect.x = Mathf.RoundToInt((sourceSize.x - cropRect.width) * 0.5f);
        cropRect.y = Mathf.RoundToInt((sourceSize.y - cropRect.height) * 0.5f);
        NormalizeCrop();
        RefreshAll();
    }

    private void ApplyAspectToCurrentCrop()
    {
        if (sourceTexture == null || !ActiveAspect.HasValue)
        {
            RefreshAll();
            return;
        }

        float aspect = ActiveAspect.Value;
        Vector2 center = cropRect.center;
        int width = Mathf.Max(MinCropPixels, cropRect.width);
        int height = Mathf.Max(MinCropPixels, Mathf.RoundToInt(width / aspect));
        if (clampToImage && height > sourceSize.y)
        {
            height = sourceSize.y;
            width = Mathf.Max(MinCropPixels, Mathf.RoundToInt(height * aspect));
        }

        cropRect = new RectInt(
            Mathf.RoundToInt(center.x - width * 0.5f),
            Mathf.RoundToInt(center.y - height * 0.5f),
            width,
            height);
        NormalizeCrop();
        RefreshAll();
    }

    private void SetCropFromFields(bool widthIsDriver)
    {
        if (sourceTexture == null)
            return;

        int width = Mathf.Max(MinCropPixels, cropWidth.value);
        int height = Mathf.Max(MinCropPixels, cropHeight.value);
        if (ActiveAspect.HasValue)
        {
            if (widthIsDriver)
                height = Mathf.Max(MinCropPixels, Mathf.RoundToInt(width / ActiveAspect.Value));
            else
                width = Mathf.Max(MinCropPixels, Mathf.RoundToInt(height * ActiveAspect.Value));
        }

        cropRect = new RectInt(cropX.value, cropY.value, width, height);
        NormalizeCrop();
        RefreshAll();
    }

    private void NormalizeCrop()
    {
        cropRect.width = Mathf.Max(MinCropPixels, cropRect.width);
        cropRect.height = Mathf.Max(MinCropPixels, cropRect.height);
        if (!clampToImage || sourceTexture == null)
            return;

        if (ActiveAspect.HasValue && (cropRect.width > sourceSize.x || cropRect.height > sourceSize.y))
        {
            float scale = Mathf.Min(sourceSize.x / (float)cropRect.width, sourceSize.y / (float)cropRect.height);
            cropRect.width = Mathf.Max(MinCropPixels, Mathf.RoundToInt(cropRect.width * scale));
            cropRect.height = Mathf.Max(MinCropPixels, Mathf.RoundToInt(cropRect.width / ActiveAspect.Value));
            if (cropRect.height > sourceSize.y)
            {
                cropRect.height = sourceSize.y;
                cropRect.width = Mathf.Max(MinCropPixels, Mathf.RoundToInt(cropRect.height * ActiveAspect.Value));
            }
        }
        else
        {
            cropRect.width = Mathf.Min(cropRect.width, sourceSize.x);
            cropRect.height = Mathf.Min(cropRect.height, sourceSize.y);
        }
        cropRect.x = Mathf.Clamp(cropRect.x, 0, sourceSize.x - cropRect.width);
        cropRect.y = Mathf.Clamp(cropRect.y, 0, sourceSize.y - cropRect.height);
    }

    private void RefreshAll()
    {
        if (stage == null)
            return;

        bool loaded = sourceTexture != null;
        imageLayer.style.display = loaded ? DisplayStyle.Flex : DisplayStyle.None;
        shadeLayer.style.display = loaded ? DisplayStyle.Flex : DisplayStyle.None;
        cropFrame.style.display = loaded ? DisplayStyle.Flex : DisplayStyle.None;
        guidesLayer.style.display = loaded ? DisplayStyle.Flex : DisplayStyle.None;
        handlesLayer.style.display = loaded ? DisplayStyle.Flex : DisplayStyle.None;
        emptyHint.style.display = loaded ? DisplayStyle.None : DisplayStyle.Flex;
        overwriteButton?.SetEnabled(loaded);
        saveAsButton?.SetEnabled(loaded);

        if (!loaded)
        {
            infoLabel.text = "未载入图片";
            RefreshPreview();
            return;
        }

        Vector2 imagePosition = GetImagePosition();
        Vector2 imageSize = GetImageSize();
        SetRect(imageLayer, new Rect(imagePosition, imageSize));

        Rect cropScreen = CropToScreen(cropRect);
        SetRect(cropFrame, cropScreen);
        RefreshShade(cropScreen);
        RefreshGuides(cropScreen);
        RefreshHandles(cropScreen);
        RefreshFields();
        RefreshPreview();
        infoLabel.text = $"{Path.GetFileName(assetPath)}  |  原图 {sourceSize.x} × {sourceSize.y}  |  裁剪 {cropRect.width} × {cropRect.height}  |  {zoom * 100f:0.#}%";
    }

    private void RefreshFields()
    {
        cropX?.SetValueWithoutNotify(cropRect.x);
        cropY?.SetValueWithoutNotify(cropRect.y);
        cropWidth?.SetValueWithoutNotify(cropRect.width);
        cropHeight?.SetValueWithoutNotify(cropRect.height);
    }

    private Vector2 GetStageCenter()
    {
        return new Vector2(stage.contentRect.width * 0.5f, stage.contentRect.height * 0.5f);
    }

    private Vector2 GetImageSize()
    {
        return new Vector2(sourceSize.x * zoom, sourceSize.y * zoom);
    }

    private Vector2 GetImagePosition()
    {
        return GetStageCenter() + panOffset - GetImageSize() * 0.5f;
    }

    private Rect CropToScreen(RectInt rect)
    {
        Vector2 imagePosition = GetImagePosition();
        return new Rect(
            imagePosition.x + rect.x * zoom,
            imagePosition.y + (sourceSize.y - rect.y - rect.height) * zoom,
            rect.width * zoom,
            rect.height * zoom);
    }

    private static void SetRect(VisualElement element, Rect rect)
    {
        element.style.left = rect.x;
        element.style.top = rect.y;
        element.style.width = Mathf.Max(0f, rect.width);
        element.style.height = Mathf.Max(0f, rect.height);
    }

    private void RefreshShade(Rect cropScreen)
    {
        shadeLayer.Clear();
        float width = stage.contentRect.width;
        float height = stage.contentRect.height;
        float left = Mathf.Clamp(cropScreen.xMin, 0f, width);
        float right = Mathf.Clamp(cropScreen.xMax, 0f, width);
        float top = Mathf.Clamp(cropScreen.yMin, 0f, height);
        float bottom = Mathf.Clamp(cropScreen.yMax, 0f, height);
        Color shade = new Color(0f, 0f, 0f, 0.58f);

        AddRectangle(shadeLayer, new Rect(0f, 0f, width, top), shade);
        AddRectangle(shadeLayer, new Rect(0f, bottom, width, height - bottom), shade);
        AddRectangle(shadeLayer, new Rect(0f, top, left, bottom - top), shade);
        AddRectangle(shadeLayer, new Rect(right, top, width - right, bottom - top), shade);
    }

    private void RefreshGuides()
    {
        if (sourceTexture != null)
            RefreshGuides(CropToScreen(cropRect));
    }

    private void RefreshGuides(Rect cropScreen)
    {
        guidesLayer.Clear();
        if (!showGrid)
            return;

        Color color = new Color(1f, 1f, 1f, 0.33f);
        for (int i = 1; i <= 2; i++)
        {
            float factor = i / 3f;
            AddRectangle(guidesLayer, new Rect(cropScreen.x + cropScreen.width * factor, cropScreen.y, 1f, cropScreen.height), color);
            AddRectangle(guidesLayer, new Rect(cropScreen.x, cropScreen.y + cropScreen.height * factor, cropScreen.width, 1f), color);
        }
    }

    private static void AddRectangle(VisualElement parent, Rect rect, Color color)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;
        VisualElement element = AbsoluteLayer();
        element.pickingMode = PickingMode.Ignore;
        element.style.backgroundColor = color;
        SetRect(element, rect);
        parent.Add(element);
    }

    private void RefreshHandles(Rect cropScreen)
    {
        float halfHit = HandleHitSize * 0.5f;
        Rect[] hitRects =
        {
            // 四条边本身都是可拖拽热区，不再只依赖中间的小方块。
            new Rect(cropScreen.xMin - halfHit, cropScreen.yMin, HandleHitSize, cropScreen.height),
            new Rect(cropScreen.xMax - halfHit, cropScreen.yMin, HandleHitSize, cropScreen.height),
            new Rect(cropScreen.xMin, cropScreen.yMin - halfHit, cropScreen.width, HandleHitSize),
            new Rect(cropScreen.xMin, cropScreen.yMax - halfHit, cropScreen.width, HandleHitSize),
            new Rect(cropScreen.xMin - halfHit, cropScreen.yMin - halfHit, HandleHitSize, HandleHitSize),
            new Rect(cropScreen.xMax - halfHit, cropScreen.yMin - halfHit, HandleHitSize, HandleHitSize),
            new Rect(cropScreen.xMin - halfHit, cropScreen.yMax - halfHit, HandleHitSize, HandleHitSize),
            new Rect(cropScreen.xMax - halfHit, cropScreen.yMax - halfHit, HandleHitSize, HandleHitSize)
        };
        for (int i = 0; i < handles.Length; i++)
            SetRect(handles[i], hitRects[i]);
    }

    private void RefreshPreview()
    {
        if (previewImage == null)
            return;
        if (!showPreview || sourceTexture == null || cropRect.width <= 0 || cropRect.height <= 0)
        {
            previewImage.style.display = DisplayStyle.None;
            DisposePreviewTexture();
            return;
        }

        previewImage.style.display = DisplayStyle.Flex;
        if (previewTexture == null || previewedCropRect != cropRect)
        {
            Color32[] pixels = BuildPreviewPixels(out int previewWidth, out int previewHeight);
            DisposePreviewTexture();
            previewTexture = new Texture2D(previewWidth, previewHeight, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                name = "Texture Cropper Preview"
            };
            previewTexture.SetPixels32(pixels);
            previewTexture.Apply(false, false);
            previewedCropRect = cropRect;
        }

        float availableWidth = Mathf.Max(1f, previewBox.contentRect.width - 12f);
        float availableHeight = Mathf.Max(1f, previewBox.contentRect.height - 12f);
        float scale = Mathf.Min(availableWidth / cropRect.width, availableHeight / cropRect.height);
        previewImage.style.width = Mathf.Max(1f, cropRect.width * scale);
        previewImage.style.height = Mathf.Max(1f, cropRect.height * scale);
        previewImage.style.backgroundImage = new StyleBackground(previewTexture);
        previewImage.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
    }

    private Color32[] BuildPreviewPixels(out int previewWidth, out int previewHeight)
    {
        float scale = Mathf.Min(1f, PreviewMaxDimension / (float)Mathf.Max(cropRect.width, cropRect.height));
        previewWidth = Mathf.Max(1, Mathf.RoundToInt(cropRect.width * scale));
        previewHeight = Mathf.Max(1, Mathf.RoundToInt(cropRect.height * scale));
        Color32[] result = new Color32[previewWidth * previewHeight];

        for (int y = 0; y < previewHeight; y++)
        {
            int sourceY = cropRect.y + Mathf.Min(cropRect.height - 1, Mathf.FloorToInt((y + 0.5f) / scale));
            if (sourceY < 0 || sourceY >= sourceSize.y)
                continue;
            int targetRow = y * previewWidth;
            int sourceRow = sourceY * sourceSize.x;
            for (int x = 0; x < previewWidth; x++)
            {
                int sourceX = cropRect.x + Mathf.Min(cropRect.width - 1, Mathf.FloorToInt((x + 0.5f) / scale));
                if (sourceX >= 0 && sourceX < sourceSize.x)
                    result[targetRow + x] = sourcePixels[sourceRow + sourceX];
            }
        }
        return result;
    }

    private Color32[] CopyCropPixels()
    {
        Color32[] result = new Color32[cropRect.width * cropRect.height];
        int copyStartX = Mathf.Max(0, cropRect.x);
        int copyEndX = Mathf.Min(sourceSize.x, cropRect.xMax);
        int copyStartY = Mathf.Max(0, cropRect.y);
        int copyEndY = Mathf.Min(sourceSize.y, cropRect.yMax);
        int copyWidth = copyEndX - copyStartX;
        if (copyWidth <= 0 || copyEndY <= copyStartY)
            return result;

        for (int sourceY = copyStartY; sourceY < copyEndY; sourceY++)
        {
            int sourceIndex = sourceY * sourceSize.x + copyStartX;
            int targetX = copyStartX - cropRect.x;
            int targetY = sourceY - cropRect.y;
            int targetIndex = targetY * cropRect.width + targetX;
            Array.Copy(sourcePixels, sourceIndex, result, targetIndex, copyWidth);
        }
        return result;
    }

    private void OnWheel(WheelEvent evt)
    {
        if (sourceTexture == null)
            return;
        float oldZoom = zoom;
        float multiplier = Mathf.Exp(-evt.delta.y * 0.035f);
        zoom = Mathf.Clamp(zoom * multiplier, MinZoom, MaxZoom);

        Vector2 mouse = evt.localMousePosition;
        Vector2 oldSize = new Vector2(sourceSize.x * oldZoom, sourceSize.y * oldZoom);
        Vector2 oldPosition = GetStageCenter() + panOffset - oldSize * 0.5f;
        Vector2 normalizedPixel = (mouse - oldPosition) / oldZoom;
        Vector2 newSize = GetImageSize();
        Vector2 newPosition = GetStageCenter() + panOffset - newSize * 0.5f;
        panOffset += mouse - normalizedPixel * zoom - newPosition;
        RefreshAll();
        evt.StopPropagation();
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (sourceTexture == null)
            return;
        stage.Focus();
        bool pan = evt.button == 2 || evt.button == 1 || spaceHeld || (evt.modifiers & EventModifiers.Alt) != 0;
        if (pan)
        {
            BeginPan(evt.position);
            stage.CapturePointer(evt.pointerId);
            evt.StopPropagation();
            return;
        }

        if (evt.button != 0)
            return;
        Vector2 local = stage.WorldToLocal(evt.position);
        if (CropToScreen(cropRect).Contains(local))
            BeginMoveCrop(evt.position);
        else
            BeginPan(evt.position);
        stage.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        switch (dragMode)
        {
            case DragMode.MoveCrop:
                MoveCrop(evt.position);
                break;
            case DragMode.PanCanvas:
                PanCanvas(evt.position);
                break;
            case DragMode.Resize:
                ResizeCrop(evt.position, (evt.modifiers & EventModifiers.Shift) != 0);
                break;
            default:
                return;
        }
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (dragMode == DragMode.None)
            return;
        EndDrag();
        if (stage.HasPointerCapture(evt.pointerId))
            stage.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Space)
        {
            spaceHeld = true;
            evt.StopPropagation();
            return;
        }
        if (sourceTexture == null)
            return;
        if (evt.keyCode == KeyCode.F)
        {
            FitToStage();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.R)
        {
            ResetCrop();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            if (dragMode != DragMode.None)
            {
                cropRect = dragStartCrop;
                panOffset = dragStartPan;
                EndDrag();
                RefreshAll();
            }
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow ||
                 evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow)
        {
            int step = (evt.modifiers & EventModifiers.Shift) != 0 ? 10 : 1;
            if (evt.keyCode == KeyCode.LeftArrow) cropRect.x -= step;
            if (evt.keyCode == KeyCode.RightArrow) cropRect.x += step;
            if (evt.keyCode == KeyCode.DownArrow) cropRect.y -= step;
            if (evt.keyCode == KeyCode.UpArrow) cropRect.y += step;
            NormalizeCrop();
            RefreshAll();
            evt.StopPropagation();
        }
    }

    private void OnKeyUp(KeyUpEvent evt)
    {
        if (evt.keyCode != KeyCode.Space)
            return;
        spaceHeld = false;
        evt.StopPropagation();
    }

    private void OnStageGeometryChanged(GeometryChangedEvent evt)
    {
        if (sourceTexture != null && !fittedForCurrentTexture && evt.newRect.width > 10f && evt.newRect.height > 10f)
            FitToStage();
        else
            RefreshAll();
    }

    private void BeginMoveCrop(Vector2 mouse)
    {
        dragMode = DragMode.MoveCrop;
        dragStartMouse = mouse;
        dragStartCrop = cropRect;
        dragStartPan = panOffset;
    }

    private void MoveCrop(Vector2 mouse)
    {
        Vector2 delta = (mouse - dragStartMouse) / zoom;
        cropRect.x = dragStartCrop.x + Mathf.RoundToInt(delta.x);
        cropRect.y = dragStartCrop.y - Mathf.RoundToInt(delta.y);
        NormalizeCrop();
        RefreshAll();
    }

    private void BeginPan(Vector2 mouse)
    {
        dragMode = DragMode.PanCanvas;
        dragStartMouse = mouse;
        dragStartCrop = cropRect;
        dragStartPan = panOffset;
    }

    private void PanCanvas(Vector2 mouse)
    {
        panOffset = dragStartPan + mouse - dragStartMouse;
        RefreshAll();
    }

    private void BeginResize(HandleDirection direction, Vector2 mouse)
    {
        dragMode = DragMode.Resize;
        activeHandle = direction;
        dragStartMouse = mouse;
        dragStartCrop = cropRect;
        dragStartPan = panOffset;
    }

    private void ResizeCrop(Vector2 mouse, bool shiftHeld)
    {
        Vector2 delta = (mouse - dragStartMouse) / zoom;
        float left = dragStartCrop.xMin;
        float right = dragStartCrop.xMax;
        float bottom = dragStartCrop.yMin;
        float top = dragStartCrop.yMax;

        bool moveLeft = activeHandle == HandleDirection.Left || activeHandle == HandleDirection.TopLeft || activeHandle == HandleDirection.BottomLeft;
        bool moveRight = activeHandle == HandleDirection.Right || activeHandle == HandleDirection.TopRight || activeHandle == HandleDirection.BottomRight;
        bool moveTop = activeHandle == HandleDirection.Top || activeHandle == HandleDirection.TopLeft || activeHandle == HandleDirection.TopRight;
        bool moveBottom = activeHandle == HandleDirection.Bottom || activeHandle == HandleDirection.BottomLeft || activeHandle == HandleDirection.BottomRight;
        if (moveLeft) left += delta.x;
        if (moveRight) right += delta.x;
        if (moveTop) top -= delta.y;
        if (moveBottom) bottom -= delta.y;

        float? aspect = ActiveAspect;
        if (!aspect.HasValue && shiftHeld)
            aspect = dragStartCrop.width / (float)Mathf.Max(1, dragStartCrop.height);
        if (aspect.HasValue)
            ApplyResizeAspect(ref left, ref right, ref bottom, ref top, moveLeft, moveRight, moveBottom, moveTop, aspect.Value);

        if (right - left < MinCropPixels)
        {
            if (moveLeft) left = right - MinCropPixels;
            else right = left + MinCropPixels;
        }
        if (top - bottom < MinCropPixels)
        {
            if (moveBottom) bottom = top - MinCropPixels;
            else top = bottom + MinCropPixels;
        }

        cropRect = new RectInt(
            Mathf.RoundToInt(left),
            Mathf.RoundToInt(bottom),
            Mathf.Max(MinCropPixels, Mathf.RoundToInt(right - left)),
            Mathf.Max(MinCropPixels, Mathf.RoundToInt(top - bottom)));
        NormalizeCrop();
        RefreshAll();
    }

    private static void ApplyResizeAspect(ref float left, ref float right, ref float bottom, ref float top,
        bool moveLeft, bool moveRight, bool moveBottom, bool moveTop, float aspect)
    {
        float width = Mathf.Max(MinCropPixels, right - left);
        float height = Mathf.Max(MinCropPixels, top - bottom);
        bool horizontalOnly = (moveLeft || moveRight) && !moveTop && !moveBottom;
        bool verticalOnly = (moveTop || moveBottom) && !moveLeft && !moveRight;
        float widthDrivenError = Mathf.Abs(height - width / aspect);
        float heightDrivenError = Mathf.Abs(width - height * aspect) / aspect;
        bool widthDrives = horizontalOnly || (!verticalOnly && widthDrivenError <= heightDrivenError);

        if (widthDrives)
        {
            float targetHeight = width / aspect;
            if (moveBottom) bottom = top - targetHeight;
            else if (moveTop) top = bottom + targetHeight;
            else
            {
                float center = (top + bottom) * 0.5f;
                bottom = center - targetHeight * 0.5f;
                top = center + targetHeight * 0.5f;
            }
        }
        else
        {
            float targetWidth = height * aspect;
            if (moveLeft) left = right - targetWidth;
            else if (moveRight) right = left + targetWidth;
            else
            {
                float center = (left + right) * 0.5f;
                left = center - targetWidth * 0.5f;
                right = center + targetWidth * 0.5f;
            }
        }
    }

    private void EndDrag()
    {
        dragMode = DragMode.None;
    }

    private void FitToStage()
    {
        if (sourceTexture == null || stage == null || stage.contentRect.width <= 1f || stage.contentRect.height <= 1f)
            return;
        float availableWidth = Mathf.Max(1f, stage.contentRect.width - StagePadding * 2f);
        float availableHeight = Mathf.Max(1f, stage.contentRect.height - StagePadding * 2f);
        zoom = Mathf.Clamp(Mathf.Min(availableWidth / sourceSize.x, availableHeight / sourceSize.y), MinZoom, MaxZoom);
        panOffset = Vector2.zero;
        fittedForCurrentTexture = true;
        RefreshAll();
    }

    private void ExportOverwrite()
    {
        if (!CanExport())
            return;
        bool confirmed = EditorUtility.DisplayDialog(
            "覆盖原图片",
            $"将用 {cropRect.width} × {cropRect.height} 的裁剪结果覆盖：\n{assetPath}\n\n此操作不能通过 Undo 撤销。",
            "覆盖",
            "取消");
        if (confirmed)
            ExportTo(assetPath);
    }

    private void ExportSaveAs()
    {
        if (!CanExport())
            return;
        string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
        string defaultName = Path.GetFileNameWithoutExtension(assetPath) + "_Cropped";
        string outputPath = EditorUtility.SaveFilePanelInProject("另存裁剪图片", defaultName, "png", "选择导出位置", directory);
        if (!string.IsNullOrEmpty(outputPath))
            ExportTo(outputPath);
    }

    private bool CanExport()
    {
        return sourceTexture != null && sourcePixels != null && cropRect.width > 0 && cropRect.height > 0;
    }

    private void ExportTo(string outputAssetPath)
    {
        Texture2D output = null;
        try
        {
            output = new Texture2D(cropRect.width, cropRect.height, TextureFormat.RGBA32, false);
            output.SetPixels32(CopyCropPixels());
            output.Apply(false, false);
            string extension = Path.GetExtension(outputAssetPath).ToLowerInvariant();
            byte[] bytes = extension == ".jpg" || extension == ".jpeg" ? output.EncodeToJPG(95) : output.EncodeToPNG();
            File.WriteAllBytes(Path.GetFullPath(outputAssetPath), bytes);
            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Notify($"已导出 {cropRect.width} × {cropRect.height}");
            Debug.Log($"[TextureCropper] 已导出：{outputAssetPath} ({cropRect.width}x{cropRect.height})");

            if (string.Equals(outputAssetPath, assetPath, StringComparison.OrdinalIgnoreCase))
                LoadTexture(assetPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("导出失败", exception.Message, "确定");
        }
        finally
        {
            if (output != null)
                DestroyImmediate(output);
        }
    }

    private void DisposeSourceTexture()
    {
        if (sourceTexture != null)
            DestroyImmediate(sourceTexture);
        sourceTexture = null;
        sourcePixels = null;
        assetPath = null;
        DisposePreviewTexture();
    }

    private void Notify(string message)
    {
        (notificationOwner != null ? notificationOwner : this).ShowNotification(new GUIContent(message));
    }

    private void DisposePreviewTexture()
    {
        if (previewTexture != null)
            DestroyImmediate(previewTexture);
        previewTexture = null;
        previewedCropRect = default;
        if (previewImage != null)
            previewImage.style.backgroundImage = StyleKeyword.None;
    }

    private void OnDisable()
    {
        DisposeSourceTexture();
    }
}
