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
            Stamp = 3,
        }

        public static PaintTypeEnum CurrentPaintType;

        private string[] paintTypeNames = new string[] { "地洞", "设置高度", "平滑", "印章" };


        private void ApplyBrushHeightInternal(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform)
        {
            Material mat = TerrainPaintUtility.GetBuiltinPaintMaterial();

            brushStrength = Event.current.shift ? -brushStrength : brushStrength;
            Vector4 brushParams = new Vector4(0.01f * brushStrength, 0.0f, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.RaiseLowerHeight);
        }

        private Material ApplyBrushHeightFromBaseHeightInternal(PaintContextExp paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform, Terrain terrain)
        {
            Material mat = TerrainManager.GetPaintHeightExtMat();

            brushStrength = Event.current.shift ? -brushStrength : brushStrength;
            Vector4 brushParams = new Vector4(0.01f * brushStrength, 0.0f, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            //sourceRenderTexture:原地型高度图
            //destinationRenderTexture:目标
            //brushTexture: 笔刷
            //通过以下操作，将原地型高度图 + 笔刷，生成新的高度图到目标缓冲区destinationRenderTexture
            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.RaiseLowerHeight);

            return mat;
        }
    }
}
