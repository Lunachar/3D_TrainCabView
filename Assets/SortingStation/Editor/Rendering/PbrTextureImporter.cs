using UnityEditor;

namespace SortingStation.EditorTools
{
    /// <summary>
    /// Import rules for the repacked ambientCG textures in Resources/Pbr: normal maps as normal
    /// maps, the metal/AO/smoothness mask as linear data, everything capped at 1K for the tablet.
    /// </summary>
    public sealed class PbrTextureImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/SortingStation/Resources/Pbr/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            if (assetPath.EndsWith("_Normal.jpg"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (assetPath.EndsWith("_Mask.png"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
            }
        }
    }
}
