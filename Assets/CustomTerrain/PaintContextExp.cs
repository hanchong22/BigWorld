using SeasunTerrain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityEngine.Experimental.TerrainAPI
{
    public class PaintContextExp : UnityEngine.Experimental.TerrainAPI.PaintContext
    {
        public PaintContextExp(Terrain terrain, RectInt pixelRect, int targetTextureWidth, int targetTextureHeight, bool texelPadding = true) : base(terrain, pixelRect, targetTextureWidth, targetTextureHeight, texelPadding)
        {
        }

        public static PaintContextExp CreateExpFromBounds(Terrain terrain, Rect boundsInTerrainSpace, int inputTextureWidth, int inputTextureHeight, int extraBorderPixels = 0, bool texelPadding = true)
        {
            return new PaintContextExp(
                terrain,
                TerrainManager.CalcPixelRectFromBounds(terrain, boundsInTerrainSpace, inputTextureWidth, inputTextureHeight, extraBorderPixels, texelPadding),
                inputTextureWidth, inputTextureHeight, texelPadding);
        }

        public void GatherInitHeightmap(int layerIdx)
        {
            var blitMaterial = UnityEngine.Experimental.TerrainAPI.TerrainPaintUtility.GetHeightBlitMaterial();
            blitMaterial.SetFloat("_Height_Offset", 0.0f);
            blitMaterial.SetFloat("_Height_Scale", 1.0f);

            GatherInternalExp(
                t => TerrainManager.GetHeightMapByIdx(t, layerIdx), //t.terrainData.heightmapTexture, //
                new Color(0.0f, 0.0f, 0.0f, 0.0f),
                "PaintContext.GatherHeightmap",
                blitMaterial: blitMaterial,
                beforeBlit: t =>
                {
                    blitMaterial.SetFloat("_Height_Offset", (t.GetPosition().y - heightWorldSpaceMin) / heightWorldSpaceSize * kNormalizedHeightScale);
                    blitMaterial.SetFloat("_Height_Scale", t.terrainData.size.y / heightWorldSpaceSize);
                });
        }

        private void GatherInternalExp(
           Func<Terrain, Texture> terrainToTexture,
           Color defaultColor,
           string operationName,
           Material blitMaterial = null,
           int blitPass = 0,
           Action<Terrain> beforeBlit = null,
           Action<Terrain> afterBlit = null)
        {
            if (blitMaterial == null)
                blitMaterial = TerrainManager.GetHeightSubtractionMat();

            RenderTexture.active = sourceRenderTexture;
            GL.Clear(false, true, defaultColor);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, pixelRect.width, 0, pixelRect.height);
            for (int i = 0; i < terrainCount; i++)
            {
                var terrain = GetTerrain(i);

                Texture sourceTexture = terrainToTexture(terrain);
                if (!sourceTexture)
                    continue;

                beforeBlit?.Invoke(terrain);

                FilterMode oldFilterMode = sourceTexture.filterMode;
                sourceTexture.filterMode = FilterMode.Point;

                blitMaterial.SetTexture("_MainTex", sourceTexture);
                blitMaterial.SetPass(blitPass);
                TerrainManager.DrawQuad(GetClippedPixelRectInRenderTexturePixels(i), GetClippedPixelRectInTerrainPixels(i), sourceTexture);

                sourceTexture.filterMode = oldFilterMode;

                afterBlit?.Invoke(terrain);
            }
            GL.PopMatrix();
            RenderTexture.active = oldRenderTexture;
        }

        public void GatherHoles(int layerIdx)
        {
            GatherInternalExp(
                t => TerrainManager.GetHoleMapByIdx(t, layerIdx),
                new Color(0.0f, 0.0f, 0.0f, 0.0f),
                "PaintContext.GatherHoles");
        }

    }
}
