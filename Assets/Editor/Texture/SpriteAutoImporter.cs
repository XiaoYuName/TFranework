using System.IO;
using UnityEditor;

public class SpriteAutoImporter : AssetPostprocessor
{
    // 需要自动设为 Sprite 的目录（以 "Assets/" 开头，结尾带 "/"）
    static readonly string[] SpriteFolders =
    {
        "Assets/0 Core/3 Art/Scene/",
        "Assets/0 Core/3 Art/UI/",
    };

    void OnPreprocessTexture()
    {
        // 仅对首次导入的新图生效：首次导入时 .meta 尚未生成，重导入时已存在，
        // 以此避免覆盖已手动调整过的图
        if (File.Exists(assetPath + ".meta")) return;

        foreach (string folder in SpriteFolders)
        {
            if (assetPath.StartsWith(folder))
            {
                ((TextureImporter)assetImporter).textureType = TextureImporterType.Sprite;
                break;
            }
        }
    }
}
