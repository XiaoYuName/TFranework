using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Texture Tools 单窗口中的透明边裁切模块。</summary>
internal sealed class TextureTrimmerModule : IDisposable
{
    internal VisualElement Root { get; }

    private readonly EditorWindow owner;
    private readonly VisualElement selectionList;
    private readonly Label selectionSummary;
    private readonly SliderInt alphaThreshold;
    private readonly IntegerField padding;
    private readonly Toggle keepSquare;
    private readonly Button trimButton;
    private readonly List<Texture2D> selectedTextures = new();

    internal TextureTrimmerModule(EditorWindow owner)
    {
        this.owner = owner;
        Root = new VisualElement { name = "texture-trimmer-module" };
        Root.style.flexGrow = 1f;
        Root.style.minWidth = 0f;
        Root.style.paddingLeft = 18f;
        Root.style.paddingRight = 18f;
        Root.style.paddingTop = 16f;
        Root.style.paddingBottom = 16f;
        Root.style.backgroundColor = new Color(0.16f, 0.16f, 0.175f);

        Label title = new Label("透明边裁切");
        title.style.fontSize = 20f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        Root.Add(title);
        Label subtitle = new Label("批量移除 PNG 四周透明像素；直接处理 Project 当前选中的图片。");
        subtitle.style.marginTop = 3f;
        subtitle.style.color = new Color(1f, 1f, 1f, 0.55f);
        Root.Add(subtitle);

        VisualElement body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        body.style.flexGrow = 1f;
        body.style.minHeight = 0f;
        body.style.marginTop = 14f;
        Root.Add(body);

        VisualElement sourceCard = BuildCard("Project 选中资源");
        sourceCard.style.flexGrow = 1f;
        sourceCard.style.minWidth = 280f;
        sourceCard.style.marginRight = 12f;
        selectionSummary = new Label();
        selectionSummary.style.marginBottom = 7f;
        selectionSummary.style.color = new Color(1f, 1f, 1f, 0.62f);
        sourceCard.Add(selectionSummary);

        selectionList = new ScrollView(ScrollViewMode.Vertical);
        selectionList.style.flexGrow = 1f;
        selectionList.style.minHeight = 220f;
        selectionList.style.paddingLeft = 5f;
        selectionList.style.paddingRight = 5f;
        selectionList.style.paddingTop = 4f;
        selectionList.style.paddingBottom = 4f;
        selectionList.style.backgroundColor = new Color(0.115f, 0.115f, 0.125f);
        SetRadius(selectionList, 5f);
        sourceCard.Add(selectionList);

        Button useSelection = new Button(RefreshSelection) { text = "刷新 Project 选中项" };
        useSelection.style.height = 28f;
        useSelection.style.marginTop = 8f;
        sourceCard.Add(useSelection);
        body.Add(sourceCard);

        VisualElement settingsCard = BuildCard("裁切参数");
        settingsCard.style.width = 330f;
        settingsCard.style.flexShrink = 0f;

        alphaThreshold = new SliderInt("Alpha 阈值", 0, 254)
        {
            value = 0,
            showInputField = true,
            tooltip = "Alpha 大于该值才视为有效内容。0 表示保留任何非全透明像素。"
        };
        settingsCard.Add(alphaThreshold);

        padding = new IntegerField("保留边距 (px)")
        {
            value = 0,
            tooltip = "裁切后在内容四周额外保留的透明像素。"
        };
        padding.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue < 0)
                padding.SetValueWithoutNotify(0);
        });
        settingsCard.Add(padding);

        keepSquare = new Toggle("补成正方形")
        {
            value = false,
            tooltip = "裁切后按较长边补成正方形，内容保持居中。"
        };
        settingsCard.Add(keepSquare);

        HelpBox warning = new HelpBox(
            "该工具会覆盖原 PNG，无法通过 Unity Undo 撤销。建议先提交 Git。",
            HelpBoxMessageType.Warning);
        warning.style.marginTop = 12f;
        settingsCard.Add(warning);

        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        settingsCard.Add(spacer);

        trimButton = new Button(ExecuteTrim) { text = "裁切选中的图片" };
        trimButton.style.height = 34f;
        trimButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        trimButton.style.backgroundColor = new Color(0.25f, 0.56f, 0.92f);
        trimButton.style.color = Color.white;
        settingsCard.Add(trimButton);
        body.Add(settingsCard);

        Selection.selectionChanged += RefreshSelection;
        RefreshSelection();
    }

    public void Dispose()
    {
        Selection.selectionChanged -= RefreshSelection;
    }

    private void RefreshSelection()
    {
        selectedTextures.Clear();
        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (selected is Texture2D texture)
                selectedTextures.Add(texture);
        }

        selectionList.Clear();
        int pngCount = 0;
        foreach (Texture2D texture in selectedTextures)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            bool supported = string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);
            if (supported)
                pngCount++;
            selectionList.Add(BuildTextureRow(texture, path, supported));
        }

        if (selectedTextures.Count == 0)
        {
            Label empty = new Label("请在 Project 窗口选择一张或多张 PNG");
            empty.style.marginTop = 18f;
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.color = new Color(1f, 1f, 1f, 0.38f);
            selectionList.Add(empty);
        }

        selectionSummary.text = $"已选 {selectedTextures.Count} 张图片 · 可处理 {pngCount} 张 PNG";
        trimButton.text = pngCount > 0 ? $"裁切 {pngCount} 张 PNG" : "裁切选中的图片";
        trimButton.SetEnabled(pngCount > 0);
    }

    private static VisualElement BuildTextureRow(Texture2D texture, string path, bool supported)
    {
        VisualElement row = new VisualElement();
        row.style.height = 42f;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 6f;
        row.style.paddingRight = 6f;
        row.style.marginBottom = 3f;
        row.style.backgroundColor = new Color(1f, 1f, 1f, 0.035f);
        SetRadius(row, 4f);

        Image thumbnail = new Image
        {
            image = AssetPreview.GetMiniThumbnail(texture),
            scaleMode = ScaleMode.ScaleToFit,
            pickingMode = PickingMode.Ignore
        };
        thumbnail.style.width = 32f;
        thumbnail.style.height = 32f;
        thumbnail.style.marginRight = 8f;
        row.Add(thumbnail);

        VisualElement text = new VisualElement();
        text.style.flexGrow = 1f;
        text.style.minWidth = 0f;
        Label name = new Label(texture.name);
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        text.Add(name);
        Label assetPath = new Label(path);
        assetPath.style.fontSize = 9f;
        assetPath.style.overflow = Overflow.Hidden;
        assetPath.style.textOverflow = TextOverflow.Ellipsis;
        assetPath.style.color = new Color(1f, 1f, 1f, 0.38f);
        text.Add(assetPath);
        row.Add(text);

        Label state = new Label(supported ? "PNG" : "不支持");
        state.style.fontSize = 9f;
        state.style.color = supported ? new Color(0.45f, 0.82f, 0.55f) : new Color(1f, 0.55f, 0.42f);
        row.Add(state);
        return row;
    }

    private void ExecuteTrim()
    {
        int pngCount = 0;
        foreach (Texture2D texture in selectedTextures)
        {
            if (string.Equals(Path.GetExtension(AssetDatabase.GetAssetPath(texture)), ".png", StringComparison.OrdinalIgnoreCase))
                pngCount++;
        }
        if (pngCount == 0)
            return;

        bool confirmed = EditorUtility.DisplayDialog(
            "批量裁切透明边",
            $"将覆盖当前选中的 {pngCount} 张 PNG。\n\nAlpha 阈值：{alphaThreshold.value}\n保留边距：{Mathf.Max(0, padding.value)} px",
            "开始裁切",
            "取消");
        if (!confirmed)
            return;

        TextureTrimmer.TrimSelection(new TextureTrimmer.Options
        {
            alphaThreshold = (byte)alphaThreshold.value,
            padding = Mathf.Max(0, padding.value),
            keepSquare = keepSquare.value
        });
        owner.ShowNotification(new GUIContent($"透明边裁切完成：{pngCount} 张"));
        RefreshSelection();
    }

    private static VisualElement BuildCard(string titleText)
    {
        VisualElement card = new VisualElement();
        card.style.paddingLeft = 12f;
        card.style.paddingRight = 12f;
        card.style.paddingTop = 10f;
        card.style.paddingBottom = 11f;
        card.style.backgroundColor = new Color(0.205f, 0.205f, 0.22f);
        SetRadius(card, 7f);
        Label title = new Label(titleText);
        title.style.fontSize = 13f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 8f;
        card.Add(title);
        return card;
    }

    private static void SetRadius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }
}
