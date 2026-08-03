using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 裁剪贴图四周的透明空白。
/// 直接读取磁盘上的 PNG 字节
///
/// 用法：
///   1. 在 Project 窗口选中一张或多张 PNG，菜单 Tools/2D/Trim Transparent Border（用默认参数）。
///   2. 或菜单 Tools/2D/Texture Trimmer 打开窗口，调整阈值/边距后批量裁剪。
/// </summary>
public static class TextureTrimmer
{
    const string MenuItemName = EditorMenuSet.Texture2D + "/Trim Transparent Border";
    public struct Options
    {
        public byte alphaThreshold; // alpha 大于该值才算“内容”，默认 0（任何非全透明像素都保留）
        public int padding;         // 裁剪后四周保留的透明边距（像素）
        public bool keepSquare;     // 裁剪结果是否补成正方形（取较长边）

        public static Options Default => new () { alphaThreshold = 0, padding = 0, keepSquare = false };
    }

    [MenuItem(MenuItemName, true)]
    private static bool TrimSelectedValidate()
    {
        foreach (var obj in Selection.objects)
            if (obj is Texture2D) return true;
        return false;
    }

    [MenuItem(MenuItemName)]
    private static void TrimSelectedMenu()
    {
        TrimSelection(Options.Default);
    }

    /// <summary>裁剪当前 Selection 中的所有贴图。</summary>
    public static void TrimSelection(Options options)
    {
        List<string> paths = new();

        foreach (var obj in Selection.objects)
        {
            if (obj is Texture2D)
            {
                string p = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(p))
                    paths.Add(p);
            }
        }

        if (paths.Count == 0)
        {
            Debug.LogWarning("[TextureTrimmer] 未选中任何贴图。");
            return;
        }

        int changed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < paths.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Texture Trimmer",
                    Path.GetFileName(paths[i]), (float)i / paths.Count);
                if (TrimAsset(paths[i], options)) changed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[TextureTrimmer] 完成：{changed}/{paths.Count} 张被裁剪。");
    }

    /// <summary>裁剪单个资源路径下的贴图，返回是否实际发生了改变。</summary>
    public static bool TrimAsset(string assetPath, Options options)
    {
        string ext = Path.GetExtension(assetPath).ToLowerInvariant();
        if (ext != ".png")
        {
            Debug.LogWarning($"[TextureTrimmer] 跳过非 PNG 文件：{assetPath}");
            return false;
        }

        string fullPath = Path.GetFullPath(assetPath);
        byte[] raw = File.ReadAllBytes(fullPath);

        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!src.LoadImage(raw))
            {
                Debug.LogError($"[TextureTrimmer] 无法解码：{assetPath}");
                return false;
            }

            int w = src.width, h = src.height;
            Color32[] pixels = src.GetPixels32(); // 行优先，左下角为原点

            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[row + x].a > options.alphaThreshold)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0)
            {
                Debug.LogWarning($"[TextureTrimmer] 整张全透明，跳过：{assetPath}");
                return false;
            }

            // 加上边距并夹紧到原图范围
            int p = Mathf.Max(0, options.padding);
            minX = Mathf.Max(0, minX - p);
            minY = Mathf.Max(0, minY - p);
            maxX = Mathf.Min(w - 1, maxX + p);
            maxY = Mathf.Min(h - 1, maxY + p);

            int newW = maxX - minX + 1;
            int newH = maxY - minY + 1;

            int offsetX = 0, offsetY = 0, canvasW = newW, canvasH = newH;
            if (options.keepSquare)
            {
                canvasW = canvasH = Mathf.Max(newW, newH);
                offsetX = (canvasW - newW) / 2;
                offsetY = (canvasH - newH) / 2;
            }

            if (canvasW == w && canvasH == h)
            {
                Debug.Log($"[TextureTrimmer] 无可裁剪空白，跳过：{assetPath}");
                return false;
            }

            // 透明画布 + 拷贝内容区
            var dstPixels = new Color32[canvasW * canvasH];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < dstPixels.Length; i++) dstPixels[i] = clear;

            for (int y = 0; y < newH; y++)
            {
                int srcRow = (minY + y) * w + minX;
                int dstRow = (offsetY + y) * canvasW + offsetX;
                System.Array.Copy(pixels, srcRow, dstPixels, dstRow, newW);
            }

            var dst = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false);
            try
            {
                dst.SetPixels32(dstPixels);
                dst.Apply();
                File.WriteAllBytes(fullPath, dst.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(dst);
            }

            Debug.Log($"[TextureTrimmer] {Path.GetFileName(assetPath)}: {w}x{h} → {canvasW}x{canvasH}");
            return true;
        }
        finally
        {
            Object.DestroyImmediate(src);
        }
    }
}

/// <summary>带参数的批量裁剪窗口。</summary>
public class TextureTrimmerWindow : EditorWindow
{
    int alphaThreshold = 0;
    int padding = 0;
    bool keepSquare = false;

    [MenuItem(EditorMenuSet.Texture2D + "/Texture Trimmer")]
    static void Open()
    {
        TextureToolsHomeWindow.OpenTool("trim");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("裁剪选中 PNG 四周的透明空白，就地覆盖原文件。", MessageType.Info);

        alphaThreshold = EditorGUILayout.IntSlider(
            new GUIContent("Alpha 阈值", "alpha 大于该值才算内容；0 表示保留任何非全透明像素"),
            alphaThreshold, 0, 254);
        padding = EditorGUILayout.IntField(
            new GUIContent("边距(px)", "裁剪后四周额外保留的透明像素"), padding);
        padding = Mathf.Max(0, padding);
        keepSquare = EditorGUILayout.Toggle(
            new GUIContent("补成正方形", "裁剪后按较长边补成正方形（居中）"), keepSquare);

        int count = 0;
        foreach (var obj in Selection.objects)
            if (obj is Texture2D)
                count++;

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(count == 0))
        {
            if (GUILayout.Button($"裁剪选中的 {count} 张贴图", GUILayout.Height(30)))
            {
                TextureTrimmer.TrimSelection(new TextureTrimmer.Options
                {
                    alphaThreshold = (byte)alphaThreshold,
                    padding = padding,
                    keepSquare = keepSquare,
                });
            }
        }

        if (count == 0)
            EditorGUILayout.HelpBox("请先在 Project 窗口选中至少一张贴图。", MessageType.Warning);
    }
}
