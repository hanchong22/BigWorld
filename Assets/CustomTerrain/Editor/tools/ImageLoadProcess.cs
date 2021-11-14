using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ImageLoadProcess : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        TextureImporter importer = this.assetImporter as TextureImporter;
        if (importer == null)
        {
            return;
        }

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.textureType = TextureImporterType.DirectionalLightmap;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;

            TextureImporterPlatformSettings s = new TextureImporterPlatformSettings();
            s.format = TextureImporterFormat.R16;
            importer.SetPlatformTextureSettings(s);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = true;

            importer.SaveAndReimport();
        }

    }
}
