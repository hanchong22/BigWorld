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
            Material mat = TerrainManager.GetPaintHeightExtMat();

            brushStrength = Event.current.shift ? -brushStrength : brushStrength;
            Vector4 brushParams = new Vector4(brushStrength, 0.0f, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            //sourceRenderTexture:原地型Hole图的拷贝(画笔触碰到的区域)
            //destinationRenderTexture:新创建的临时目标
            //brushTexture: 笔刷，已经包含在材质中
            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.PaintHoles);
            //执行Blit后，paintContext.destinationRenderTexture为笔刷操作后的结果
            return mat;
        }

        private  void ApplyBrushHole(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform)
        {
            Material mat = TerrainPaintUtility.GetBuiltinPaintMaterial();
            brushStrength = Event.current.shift ? brushStrength : -brushStrength;
            Vector4 brushParams = new Vector4(brushStrength, 0.0f, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.PaintHoles);
        }

        public Material ApplyBrushSmoothHeightFromBaseInternal(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform)
        {
            Material mat = TerrainManager.GetPaintHeightExtMat();

            Vector4 brushParams = new Vector4(brushStrength, 0.0f, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);
            Vector4 smoothWeights = new Vector4(
                Mathf.Clamp01(1.0f - Mathf.Abs(m_direction)),   // centered
                Mathf.Clamp01(-m_direction),                    // min
                Mathf.Clamp01(m_direction),                     // max
                0.0f);                                          // unused
            mat.SetVector("_SmoothWeights", smoothWeights);
            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.SmoothHeights);

            return mat;
        }

    }
}
