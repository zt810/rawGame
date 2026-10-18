using UnityEditor;
using UnityEngine;

namespace MiCay.EditorTools
{
    class PixelArtImporter : AssetPostprocessor
    {
        const string ArtFolder = "Assets/Resources/Art/";

        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 25;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = assetPath.Contains("_tile") || assetPath.EndsWith("floor.png") || assetPath.EndsWith("city.png")
                ? TextureWrapMode.Repeat
                : TextureWrapMode.Clamp;
        }

        [MenuItem("Mì Cay/Nhập lại đồ họa pixel")]
        static void Reimport()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Art", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            Debug.Log("[MiCay] Đã nhập lại toàn bộ ảnh pixel art.");
        }
    }
}
