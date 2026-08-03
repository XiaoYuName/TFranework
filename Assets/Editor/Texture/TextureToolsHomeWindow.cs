using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Texture Tools 主页中的一个可导航模块。</summary>
public sealed class TextureToolDescriptor
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Category { get; }
    public string Badge { get; }
    public Action Open { get; }

    public TextureToolDescriptor(string id, string title, string description, string category, string badge, Action open)
    {
        Id = id;
        Title = title;
        Description = description;
        Category = category;
        Badge = badge;
        Open = open;
    }
}

/// <summary>
/// Texture 工具注册表。新模块可在 InitializeOnLoad 初始化代码中调用 Register，
/// 无需修改主页窗口本身。
/// </summary>
public static class TextureToolRegistry
{
    private static readonly List<TextureToolDescriptor> ExtraTools = new();

    public static void Register(TextureToolDescriptor descriptor)
    {
        if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id) || descriptor.Open == null)
            throw new ArgumentException("Texture tool descriptor is invalid.", nameof(descriptor));

        ExtraTools.RemoveAll(tool => string.Equals(tool.Id, descriptor.Id, StringComparison.Ordinal));
        ExtraTools.Add(descriptor);
    }

    public static void Unregister(string id)
    {
        ExtraTools.RemoveAll(tool => string.Equals(tool.Id, id, StringComparison.Ordinal));
    }

    public static IReadOnlyList<TextureToolDescriptor> GetTools()
    {
        List<TextureToolDescriptor> tools = new()
        {
            new TextureToolDescriptor(
                "crop", "图片裁剪", "自由拖拽、固定比例、实时预览并导出 PNG/JPG。", "编辑", "CROP",
                () => TextureToolsHomeWindow.OpenTool("crop")),
            new TextureToolDescriptor(
                "align", "图片对齐", "统一画布尺寸，拖动并批量调整图片内容的位置。", "编辑", "ALIGN",
                () => TextureToolsHomeWindow.OpenTool("align")),
            new TextureToolDescriptor(
                "trim", "透明边裁切", "批量移除 PNG 四周透明空白，可设置边距与正方形补齐。", "优化", "TRIM",
                () => TextureToolsHomeWindow.OpenTool("trim")),
            new TextureToolDescriptor(
                "nine-slice", "九宫格收缩", "分析边框和纯色区域，生成紧凑的 9-Slice Sprite。", "优化", "9-SLICE",
                () => TextureToolsHomeWindow.OpenTool("nine-slice")),
            new TextureToolDescriptor(
                "convert", "格式转换", "批量统一 PNG/JPG 格式，并可保留 Meta GUID 与资源引用。", "批处理", "FORMAT",
                () => TextureToolsHomeWindow.OpenTool("convert"))
        };
        tools.AddRange(ExtraTools);
        return tools;
    }

}

/// <summary>所有 Texture/2D 可视化工具的统一入口与扩展导航页。</summary>
public class TextureToolsHomeWindow : EditorWindow
{
    private const float SidebarWidth = 180f;
    private readonly Color accent = new(0.25f, 0.56f, 0.92f);
    private readonly Dictionary<string, Button> navigationButtons = new();
    private VisualElement pageHost;
    private VisualElement cardsHost;
    private Label resultLabel;
    private string searchText = string.Empty;
    private TextureCropperWindow cropController;
    private TextureTrimmerModule trimmerModule;
    private TextureAlignerWindow alignerController;
    private TextureFormatConverterWindow converterController;
    private NineSliceShrinkerWindow nineSliceController;
    private string activeToolId = "home";

    [MenuItem(EditorMenuSet.Texture2D + "/Texture Tools Home", false, 0)]
    public static void OpenWindow()
    {
        TextureToolsHomeWindow window = GetWindow<TextureToolsHomeWindow>("Texture Tools");
        window.minSize = new Vector2(900f, 560f);
        window.Show();
        window.Focus();
        window.ShowHomePage();
    }

    public static void OpenTool(string toolId)
    {
        TextureToolsHomeWindow window = GetWindow<TextureToolsHomeWindow>("Texture Tools");
        window.minSize = new Vector2(900f, 560f);
        window.Show();
        window.Focus();
        window.NavigateTo(toolId);
    }

    private static Button CreateHomeButton(Action goHome)
    {
        Button button = new Button(goHome) { text = "←  工具主页" };
        button.tooltip = "返回 Texture Tools 导航主页";
        button.style.height = 25f;
        button.style.width = 92f;
        button.style.flexGrow = 0f;
        button.style.flexShrink = 0f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 6f;
        button.style.marginBottom = 6f;
        return button;
    }

    private void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.Clear();
        root.style.flexDirection = FlexDirection.Row;
        root.style.flexGrow = 1f;
        root.style.backgroundColor = new Color(0.16f, 0.16f, 0.175f);

        root.Add(BuildSidebar());
        pageHost = new VisualElement { name = "texture-tool-page-host" };
        pageHost.style.flexGrow = 1f;
        pageHost.style.minWidth = 0f;
        pageHost.style.overflow = Overflow.Hidden;
        root.Add(pageHost);

        if (activeToolId == "crop")
            ShowCropPage();
        else if (activeToolId == "trim")
            ShowTrimmerPage();
        else if (activeToolId == "align")
            ShowAlignerPage();
        else if (activeToolId == "convert")
            ShowConverterPage();
        else if (activeToolId == "nine-slice")
            ShowNineSlicePage();
        else
            ShowHomePage();
    }

    private VisualElement BuildSidebar()
    {
        VisualElement sidebar = new VisualElement();
        sidebar.style.width = SidebarWidth;
        sidebar.style.flexShrink = 0f;
        sidebar.style.paddingLeft = 12f;
        sidebar.style.paddingRight = 12f;
        sidebar.style.paddingTop = 16f;
        sidebar.style.paddingBottom = 12f;
        sidebar.style.backgroundColor = new Color(0.125f, 0.125f, 0.14f);

        Label brand = new Label("TEXTURE TOOLS");
        brand.style.fontSize = 15f;
        brand.style.unityFontStyleAndWeight = FontStyle.Bold;
        brand.style.color = Color.white;
        sidebar.Add(brand);

        Label version = new Label("2D 资源工作台");
        version.style.fontSize = 10f;
        version.style.marginTop = 2f;
        version.style.marginBottom = 16f;
        version.style.color = new Color(1f, 1f, 1f, 0.38f);
        sidebar.Add(version);

        navigationButtons.Clear();
        Button home = BuildNavigationButton("主页", ShowHomePage);
        navigationButtons["home"] = home;
        sidebar.Add(home);

        Label modules = new Label("模块");
        modules.style.marginTop = 16f;
        modules.style.marginBottom = 5f;
        modules.style.fontSize = 10f;
        modules.style.color = new Color(1f, 1f, 1f, 0.38f);
        sidebar.Add(modules);

        foreach (TextureToolDescriptor tool in TextureToolRegistry.GetTools())
        {
            string id = tool.Id;
            Button button = BuildNavigationButton(tool.Title, () => NavigateTo(id));
            navigationButtons[id] = button;
            sidebar.Add(button);
        }

        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        sidebar.Add(spacer);

        Label extensionHint = new Label("新工具通过\nTextureToolRegistry.Register\n即可加入此导航");
        extensionHint.style.whiteSpace = WhiteSpace.Normal;
        extensionHint.style.fontSize = 9f;
        extensionHint.style.color = new Color(1f, 1f, 1f, 0.28f);
        sidebar.Add(extensionHint);
        return sidebar;
    }

    private void NavigateTo(string toolId)
    {
        if (pageHost == null)
        {
            activeToolId = toolId;
            return;
        }

        if (toolId == "crop")
            ShowCropPage();
        else if (toolId == "trim")
            ShowTrimmerPage();
        else if (toolId == "align")
            ShowAlignerPage();
        else if (toolId == "convert")
            ShowConverterPage();
        else if (toolId == "nine-slice")
            ShowNineSlicePage();
        else if (toolId == "home")
            ShowHomePage();
        else
            ShowPendingPage(toolId);
    }

    private void ShowHomePage()
    {
        if (pageHost == null)
            return;
        DisposeActiveTool();
        activeToolId = "home";
        pageHost.Clear();
        pageHost.Add(BuildHomeContent());
        RefreshCards();
        RefreshNavigationSelection();
    }

    private void ShowCropPage()
    {
        if (pageHost == null)
        {
            activeToolId = "crop";
            return;
        }
        DisposeActiveTool();
        activeToolId = "crop";
        pageHost.Clear();
        cropController = CreateInstance<TextureCropperWindow>();
        cropController.hideFlags = HideFlags.HideAndDontSave;
        cropController.BuildEmbedded(pageHost, this);
        RefreshNavigationSelection();
    }

    private void ShowTrimmerPage()
    {
        if (pageHost == null)
        {
            activeToolId = "trim";
            return;
        }
        DisposeActiveTool();
        activeToolId = "trim";
        pageHost.Clear();
        trimmerModule = new TextureTrimmerModule(this);
        pageHost.Add(trimmerModule.Root);
        RefreshNavigationSelection();
    }

    private void ShowAlignerPage()
    {
        PreparePage("align");
        alignerController = CreateInstance<TextureAlignerWindow>();
        alignerController.hideFlags = HideFlags.HideAndDontSave;
        alignerController.BuildEmbedded(pageHost);
        RefreshNavigationSelection();
    }

    private void ShowConverterPage()
    {
        PreparePage("convert");
        converterController = CreateInstance<TextureFormatConverterWindow>();
        converterController.hideFlags = HideFlags.HideAndDontSave;
        converterController.BuildEmbedded(pageHost);
        RefreshNavigationSelection();
    }

    private void ShowNineSlicePage()
    {
        PreparePage("nine-slice");
        nineSliceController = CreateInstance<NineSliceShrinkerWindow>();
        nineSliceController.hideFlags = HideFlags.HideAndDontSave;
        nineSliceController.BuildEmbedded(pageHost);
        RefreshNavigationSelection();
    }

    private void PreparePage(string toolId)
    {
        if (pageHost == null)
        {
            activeToolId = toolId;
            return;
        }
        DisposeActiveTool();
        activeToolId = toolId;
        pageHost.Clear();
    }

    private void ShowPendingPage(string toolId)
    {
        DisposeActiveTool();
        activeToolId = toolId;
        pageHost.Clear();
        TextureToolDescriptor tool = null;
        foreach (TextureToolDescriptor item in TextureToolRegistry.GetTools())
        {
            if (item.Id == toolId)
            {
                tool = item;
                break;
            }
        }

        VisualElement pending = new VisualElement();
        pending.style.flexGrow = 1f;
        pending.style.justifyContent = Justify.Center;
        pending.style.alignItems = Align.Center;
        Label title = new Label(tool != null ? tool.Title : "未知模块");
        title.style.fontSize = 22f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        pending.Add(title);
        Label hint = new Label("此模块尚未迁入单窗口结构。\n当前不会打开额外窗口，后续会按相同布局逐项整合。");
        hint.style.marginTop = 8f;
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        hint.style.color = new Color(1f, 1f, 1f, 0.45f);
        pending.Add(hint);
        pending.Add(CreateHomeButton(ShowHomePage));
        pageHost.Add(pending);
        RefreshNavigationSelection();
    }

    private void RefreshNavigationSelection()
    {
        foreach (KeyValuePair<string, Button> pair in navigationButtons)
        {
            bool selected = pair.Key == activeToolId;
            pair.Value.style.backgroundColor = selected ? new Color(accent.r, accent.g, accent.b, 0.25f) : Color.clear;
            pair.Value.style.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.72f);
        }
    }

    private void DisposeActiveTool()
    {
        if (cropController != null)
        {
            DestroyImmediate(cropController);
            cropController = null;
        }
        if (trimmerModule != null)
        {
            trimmerModule.Dispose();
            trimmerModule = null;
        }
        if (alignerController != null)
        {
            DestroyImmediate(alignerController);
            alignerController = null;
        }
        if (converterController != null)
        {
            DestroyImmediate(converterController);
            converterController = null;
        }
        if (nineSliceController != null)
        {
            DestroyImmediate(nineSliceController);
            nineSliceController = null;
        }
    }

    private static Button BuildNavigationButton(string text, Action open)
    {
        Button button = new Button(open) { text = text };
        button.style.height = 30f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 1f;
        button.style.marginBottom = 1f;
        button.style.unityTextAlign = TextAnchor.MiddleLeft;
        button.style.paddingLeft = 12f;
        button.style.backgroundColor = Color.clear;
        button.style.borderLeftWidth = 0f;
        button.style.borderRightWidth = 0f;
        button.style.borderTopWidth = 0f;
        button.style.borderBottomWidth = 0f;
        return button;
    }

    private VisualElement BuildHomeContent()
    {
        VisualElement content = new VisualElement();
        content.style.flexGrow = 1f;
        content.style.minWidth = 0f;
        content.style.flexDirection = FlexDirection.Column;

        VisualElement header = new VisualElement();
        header.style.paddingLeft = 22f;
        header.style.paddingRight = 22f;
        header.style.paddingTop = 18f;
        header.style.paddingBottom = 14f;

        Label title = new Label("图片处理工作台");
        title.style.fontSize = 22f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.Add(title);
        Label subtitle = new Label("裁剪、对齐、透明边、九宫格与格式转换均在当前窗口内完成。");
        subtitle.style.fontSize = 11f;
        subtitle.style.marginTop = 3f;
        subtitle.style.color = new Color(1f, 1f, 1f, 0.45f);
        header.Add(subtitle);

        TextField search = new TextField("搜索");
        search.value = searchText;
        search.tooltip = "按名称、说明或分类筛选工具";
        search.style.marginTop = 14f;
        search.style.height = 28f;
        search.RegisterValueChangedCallback(evt =>
        {
            searchText = evt.newValue ?? string.Empty;
            RefreshCards();
        });
        search.SetValueWithoutNotify(searchText);
        header.Add(search);
        content.Add(header);

        ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1f;
        scroll.style.paddingLeft = 22f;
        scroll.style.paddingRight = 16f;
        scroll.style.paddingBottom = 18f;

        resultLabel = new Label();
        resultLabel.style.fontSize = 10f;
        resultLabel.style.marginBottom = 8f;
        resultLabel.style.color = new Color(1f, 1f, 1f, 0.38f);
        scroll.Add(resultLabel);

        cardsHost = new VisualElement();
        cardsHost.style.flexDirection = FlexDirection.Row;
        cardsHost.style.flexWrap = Wrap.Wrap;
        scroll.Add(cardsHost);
        content.Add(scroll);
        return content;
    }

    private void RefreshCards()
    {
        if (cardsHost == null)
            return;
        cardsHost.Clear();
        string filter = searchText.Trim();
        int count = 0;

        foreach (TextureToolDescriptor tool in TextureToolRegistry.GetTools())
        {
            if (!Matches(tool, filter))
                continue;
            cardsHost.Add(BuildToolCard(tool));
            count++;
        }
        resultLabel.text = string.IsNullOrEmpty(filter) ? $"全部工具 · {count}" : $"搜索结果 · {count}";

        if (count == 0)
        {
            Label empty = new Label("没有匹配的工具");
            empty.style.marginTop = 25f;
            empty.style.color = new Color(1f, 1f, 1f, 0.4f);
            cardsHost.Add(empty);
        }
    }

    private static bool Matches(TextureToolDescriptor tool, string filter)
    {
        if (string.IsNullOrEmpty(filter))
            return true;
        return tool.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               tool.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               tool.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               tool.Badge.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private VisualElement BuildToolCard(TextureToolDescriptor tool)
    {
        VisualElement card = new VisualElement();
        card.style.width = 270f;
        card.style.height = 162f;
        card.style.marginRight = 10f;
        card.style.marginBottom = 10f;
        card.style.paddingLeft = 14f;
        card.style.paddingRight = 14f;
        card.style.paddingTop = 12f;
        card.style.paddingBottom = 12f;
        card.style.backgroundColor = new Color(0.205f, 0.205f, 0.22f);
        SetRadius(card, 7f);

        VisualElement top = new VisualElement();
        top.style.flexDirection = FlexDirection.Row;
        top.style.alignItems = Align.Center;
        Label badge = new Label(tool.Badge);
        badge.style.fontSize = 9f;
        badge.style.unityFontStyleAndWeight = FontStyle.Bold;
        badge.style.color = accent;
        top.Add(badge);
        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        top.Add(spacer);
        Label category = new Label(tool.Category);
        category.style.fontSize = 9f;
        category.style.color = new Color(1f, 1f, 1f, 0.35f);
        top.Add(category);
        card.Add(top);

        Label title = new Label(tool.Title);
        title.style.fontSize = 15f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginTop = 9f;
        card.Add(title);

        Label description = new Label(tool.Description);
        description.style.whiteSpace = WhiteSpace.Normal;
        description.style.fontSize = 10f;
        description.style.marginTop = 5f;
        description.style.color = new Color(1f, 1f, 1f, 0.48f);
        card.Add(description);

        VisualElement grow = new VisualElement();
        grow.style.flexGrow = 1f;
        card.Add(grow);
        bool integrated = IsIntegrated(tool.Id);
        Button open = new Button(() => NavigateTo(tool.Id))
        {
            text = integrated ? "在当前窗口打开  →" : "待整合 · 查看说明"
        };
        open.style.height = 27f;
        open.style.marginLeft = 0f;
        open.style.marginRight = 0f;
        open.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.22f);
        open.style.color = Color.white;
        card.Add(open);
        return card;
    }

    private void OnDisable()
    {
        DisposeActiveTool();
    }

    private static bool IsIntegrated(string toolId)
    {
        return toolId == "crop" || toolId == "trim" || toolId == "align" ||
               toolId == "convert" || toolId == "nine-slice";
    }

    private static void SetRadius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }
}
