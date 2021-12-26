using UnityEngine;
using UnityEngine.Experimental.TerrainAPI;
using UnityEditor.ShortcutManagement;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using System.Collections.Generic;
using System.IO;

namespace SeasunTerrain
{
    partial class CustomLayerHeightPaint
    {
        public enum PaintTypeEnum
        {
            PaintHoles = 0,
            SetHeight = 1,
            SmoothHeight = 2,
        }

        public static PaintTypeEnum CurrentPaintType;

        private string[] paintTypeNames = new string[] { "地洞", "设置高度", "平滑" };


        private Material ApplyBrushHoleFromBaseInternal(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform)
        {
            Material mat = TerrainPaintUtility.GetBuiltinPaintMaterial();

            brushStrength = Event.current.shift ? brushStrength : -brushStrength;
            Vector4 brushParams = new Vector4(brushStrength, 0.0f, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.PaintHoles);

            return mat;
        }
    }
}
