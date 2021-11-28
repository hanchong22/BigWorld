using UnityEngine;
using UnityEngine.Experimental.TerrainAPI;
using UnityEditor.ShortcutManagement;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using System.Collections.Generic;

namespace SeasunTerrain
{
    public static class TerrainManager
    {
        public static Terrain CurrentSelectedTerrain { get; set; }
        public static List<Terrain> AllTerrain { get; private set; } = new List<Terrain>();

        public static int HeightMapNumber { get; private set; }
        public static int CurrentHeightMapIdx { get; set; }
        public static bool OnlyLoadSelectedLayer { get; set; }
        public static bool[] SelectedLayer { get; set; }
        public static bool IsBaseLayerEnable { get; set; }
        public static float BrashTargetHeight { get; set; }

        public static void InitAllTerrain(int heightMapNumber, int curEditorIdx, float targetHeight)
        {
            TerrainManager.HeightMapNumber = heightMapNumber;
            TerrainManager.CurrentHeightMapIdx = curEditorIdx;
            TerrainManager.BrashTargetHeight = targetHeight;

            Terrain[] allTerrains = GameObject.FindObjectsOfType<Terrain>();
            TerrainManager.AllTerrain.Clear();
            TerrainManager.AllTerrain.AddRange(allTerrains);

            for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
            {
                if (!TerrainManager.AllTerrain[i].gameObject.GetComponent<TerrainExpand>())
                {
                    TerrainManager.AllTerrain[i].gameObject.AddComponent<TerrainExpand>();
                }
            }
        }

        public static void AddTerrain(Terrain t)
        {
            if (!TerrainManager.AllTerrain.Contains(t))
            {
                TerrainManager.AllTerrain.Add(t);
            }
        }

        public static RenderTexture GetHeightMapByIdx(Terrain t, int layerIdx)
        {
            for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
            {
                if (TerrainManager.AllTerrain[i] == t)
                {
                    TerrainExpand te = TerrainManager.AllTerrain[i].gameObject.GetComponent<TerrainExpand>();
                    if(te.rtHeightMapList == null || te.rtHeightMapList.Count <= layerIdx)
                    {
                       // Debug.LogError($"{te.gameObject} : InitHeightMaps");
                        te.InitHeightMaps();
                    }

                    return te.rtHeightMapList[layerIdx];
                }
            }

            return GetDefaultHeightMap(t);
        }


        private static RenderTexture defaultHeightMap = null;

        public static RenderTexture GetDefaultHeightMap(Terrain t)
        {
            if (!TerrainManager.defaultHeightMap || TerrainManager.defaultHeightMap.width != t.terrainData.heightmapTexture.width || TerrainManager.defaultHeightMap.height != t.terrainData.heightmapTexture.height)
            {
                if (TerrainManager.defaultHeightMap)
                {
                    RenderTexture.ReleaseTemporary(TerrainManager.defaultHeightMap);
                }

                TerrainManager.defaultHeightMap = RenderTexture.GetTemporary(t.terrainData.heightmapTexture.width, t.terrainData.heightmapTexture.height, 0, RenderTextureFormat.R16);
            }

            return TerrainManager.defaultHeightMap;
        }

        private static Material heightSubtractionMat = null;
        public static Material GetHeightSubtractionMat()
        {
            if (!TerrainManager.heightSubtractionMat)
            {
                TerrainManager.heightSubtractionMat = new Material(Shader.Find("Hidden/TerrainEngine/HeightSubtraction"));
            }

            return TerrainManager.heightSubtractionMat;
        }

        private static Material paintHeightExtMat = null;
        public static Material GetPaintHeightExtMat()
        {
            if (!TerrainManager.paintHeightExtMat)
            {
                TerrainManager.paintHeightExtMat = new Material(Shader.Find("Hidden/TerrainEngine/PaintHeightExt"));
            }

            return TerrainManager.paintHeightExtMat;
        }

        internal static PaintContextExp InitializePaintContext(Terrain terrain, int targetWidth, int targetHeight, RenderTextureFormat pcFormat, Rect boundsInTerrainSpace, int extraBorderPixels = 0, bool texelPadding = true)
        {
            PaintContextExp ctx = PaintContextExp.CreateExpFromBounds(terrain, boundsInTerrainSpace, targetWidth, targetHeight, extraBorderPixels, texelPadding);
            ctx.CreateRenderTargets(pcFormat);
            return ctx;
        }

        internal static RectInt CalcPixelRectFromBounds(Terrain terrain, Rect boundsInTerrainSpace, int textureWidth, int textureHeight, int extraBorderPixels, bool texelPadding)
        {
            float scaleX = (textureWidth - (texelPadding ? 1.0f : 0.0f)) / terrain.terrainData.size.x;
            float scaleY = (textureHeight - (texelPadding ? 1.0f : 0.0f)) / terrain.terrainData.size.z;
            int xMin = Mathf.FloorToInt(boundsInTerrainSpace.xMin * scaleX) - extraBorderPixels;
            int yMin = Mathf.FloorToInt(boundsInTerrainSpace.yMin * scaleY) - extraBorderPixels;
            int xMax = Mathf.CeilToInt(boundsInTerrainSpace.xMax * scaleX) + extraBorderPixels;
            int yMax = Mathf.CeilToInt(boundsInTerrainSpace.yMax * scaleY) + extraBorderPixels;
            return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        public static PaintContextExp BeginPaintHeightMapLyaer(Terrain terrain, Rect boundsInTerrainSpace, int currentLayer, int extraBorderPixels = 0)
        {
            int heightmapResolution = terrain.terrainData.heightmapResolution;
            PaintContextExp ctx = InitializePaintContext(terrain, heightmapResolution, heightmapResolution, Terrain.heightmapRenderTextureFormat, boundsInTerrainSpace, extraBorderPixels);

            //将地型高度图与笔刷相交的区域blit到笔刷中，用于后续的高度计算
            ctx.GatherInitHeightmap(currentLayer);
            return ctx;
        }

        internal static void DrawQuad(RectInt destinationPixels, RectInt sourcePixels, Texture sourceTexture)
        {
            DrawQuad2(destinationPixels, sourcePixels, sourceTexture, sourcePixels, sourceTexture);
        }

        internal static void DrawQuad2(RectInt destinationPixels, RectInt sourcePixels, Texture sourceTexture, RectInt sourcePixels2, Texture sourceTexture2)
        {
            if ((destinationPixels.width > 0) && (destinationPixels.height > 0))
            {
                Rect sourceUVs = new Rect(
                    (sourcePixels.x) / (float)sourceTexture.width,
                    (sourcePixels.y) / (float)sourceTexture.height,
                    (sourcePixels.width) / (float)sourceTexture.width,
                    (sourcePixels.height) / (float)sourceTexture.height);

                Rect sourceUVs2 = new Rect(
                    (sourcePixels2.x) / (float)sourceTexture2.width,
                    (sourcePixels2.y) / (float)sourceTexture2.height,
                    (sourcePixels2.width) / (float)sourceTexture2.width,
                    (sourcePixels2.height) / (float)sourceTexture2.height);

                GL.Begin(GL.QUADS);
                GL.Color(new Color(1.0f, 1.0f, 1.0f, 1.0f));
                GL.MultiTexCoord2(0, sourceUVs.x, sourceUVs.y);
                GL.MultiTexCoord2(1, sourceUVs2.x, sourceUVs2.y);
                GL.Vertex3(destinationPixels.x, destinationPixels.y, 0.0f);
                GL.MultiTexCoord2(0, sourceUVs.x, sourceUVs.yMax);
                GL.MultiTexCoord2(1, sourceUVs2.x, sourceUVs2.yMax);
                GL.Vertex3(destinationPixels.x, destinationPixels.yMax, 0.0f);
                GL.MultiTexCoord2(0, sourceUVs.xMax, sourceUVs.yMax);
                GL.MultiTexCoord2(1, sourceUVs2.xMax, sourceUVs2.yMax);
                GL.Vertex3(destinationPixels.xMax, destinationPixels.yMax, 0.0f);
                GL.MultiTexCoord2(0, sourceUVs.xMax, sourceUVs.y);
                GL.MultiTexCoord2(1, sourceUVs2.xMax, sourceUVs2.y);
                GL.Vertex3(destinationPixels.xMax, destinationPixels.y, 0.0f);
                GL.End();
            }
        }

    }


}

