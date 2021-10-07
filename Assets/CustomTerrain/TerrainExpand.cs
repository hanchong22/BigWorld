using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using UnityEngine.Experimental.TerrainAPI;
using System.IO;

namespace SeasunTerrain
{
    [RequireComponent(typeof(Terrain))]
    [ExecuteInEditMode]
    public class TerrainExpand : MonoBehaviour
    {

#if UNITY_EDITOR

        private List<Texture2D> heightMapList;

        private Texture2D baseHeightMap;

        private TerrainData terrainData;
        private Terrain terrain;
        private string terrainDataPath;

        private string baseHeightMapPath;


        public List<RenderTexture> rtHeightMapList { get; private set; } = new List<RenderTexture>();

        public Texture2D BaseHeightMap { get => this.baseHeightMap; }

        private static float kNormalizedHeightScale => 32766.0f / 65535.0f;
        private List<bool> changedIds = new List<bool>();

        private void Awake()
        {
            this.terrain = gameObject.GetComponent<Terrain>();
            this.terrainData = terrain.terrainData;
            string dataPath = AssetDatabase.GetAssetPath(this.terrainData);
            this.terrainDataPath = dataPath.Substring(0, dataPath.IndexOf(System.IO.Path.GetFileName(dataPath)));
        }

        private void OnEnable()
        {
            this.InitHeightMaps();
        }

        private void OnDestroy()
        {
            this.ClearHeightMaps();
        }

        private void OnValidate()
        {
            TerrainManager.AddTerrain(this.GetComponent<Terrain>());
        }

        public void InitHeightMaps()
        {
            this.changedIds.Clear();

            if (this.heightMapList == null)
            {
                this.heightMapList = new List<Texture2D>();
            }

            for (int i = 0; i < TerrainManager.HeightMapNumber; ++i)
            {
                RenderTexture rt = null;
                Texture2D tex = null;

                this.changedIds.Add(false);

                if (this.heightMapList.Count > i)
                {
                    tex = this.heightMapList[i];
                }

                if (this.rtHeightMapList.Count > i)
                {
                    rt = this.rtHeightMapList[i];
                }

                if (!rt)
                {
                    rt = RenderTexture.GetTemporary(this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height, 0, RenderTextureFormat.RGHalf);
                }

                if (!tex)
                {
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_heightmap{i}.asset"));

                    if (!tex)
                    {
                        tex = new Texture2D(this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height, TextureFormat.RGHalf, false);
                        Graphics.Blit(Texture2D.blackTexture, rt);
                    }
                    else
                    {
                        Graphics.Blit(tex, rt);
                    }
                }
                else
                {
                    Graphics.Blit(tex, rt);
                }

                if (this.rtHeightMapList.Count > i)
                {
                    this.rtHeightMapList[i] = rt;
                }
                else
                {
                    this.rtHeightMapList.Add(rt);
                }

                if (this.heightMapList.Count > i)
                {
                    this.heightMapList[i] = tex;
                }
                else
                {
                    this.heightMapList.Add(tex);
                }
            }

            this.baseHeightMapPath = System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_baseHeightMap.asset");

            if (!this.baseHeightMap)
            {
                this.baseHeightMap = AssetDatabase.LoadAssetAtPath<Texture2D>(this.baseHeightMapPath);
                if (!this.baseHeightMap)
                {
                    Debug.Log($"Save base heighmap:{baseHeightMapPath}");
                    this.baseHeightMap = new Texture2D(this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height, TextureFormat.R16, false);

                    CopyRtToTexture2D(this.terrainData.heightmapTexture, baseHeightMap);

                    AssetDatabase.CreateAsset(this.baseHeightMap, this.baseHeightMapPath);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private void ClearHeightMaps()
        {
            if (this.rtHeightMapList != null)
            {
                for (int i = 0; i < this.rtHeightMapList.Count; ++i)
                {
                    if (this.rtHeightMapList[i])
                    {
                        RenderTexture.ReleaseTemporary(this.rtHeightMapList[i]);
                    }
                }

                this.rtHeightMapList.Clear();
            }

            if (this.heightMapList != null)
            {
                for (int i = 0; i < this.heightMapList.Count; ++i)
                {
                    if (this.heightMapList[i])
                    {
                        GameObject.DestroyImmediate(this.heightMapList[i]);
                    }
                }

                this.heightMapList.Clear();
            }

            this.changedIds.Clear();
        }

        private void CheckOrInitData()
        {
            if (this.heightMapList == null || this.heightMapList.Count != TerrainManager.HeightMapNumber || this.rtHeightMapList.Count != TerrainManager.HeightMapNumber || this.changedIds.Count != TerrainManager.HeightMapNumber)
            {
                this.InitHeightMaps();
                return;
            }

            for (int i = 0; i < TerrainManager.HeightMapNumber; ++i)
            {
                if (!this.heightMapList[i])
                {
                    this.InitHeightMaps();
                    return;
                }

                if (!this.rtHeightMapList[i])
                {
                    this.InitHeightMaps();
                    return;
                }
            }
        }

        RectInt dstPixels;
        RectInt sourcePixels;

        public void OnPaint(int heightMapIdx, PaintContext editContext, int tileIndex, Material brushMat)
        {
            // Shader : Hidden/TerrainEngine/HeightBlitCopy
            Material blitMaterial = TerrainPaintUtility.GetHeightBlitMaterial();

            this.CheckOrInitData();

            blitMaterial.SetFloat("_Height_Offset", (editContext.heightWorldSpaceMin - this.terrain.GetPosition().y) / this.terrain.terrainData.size.y * TerrainExpand.kNormalizedHeightScale);
            blitMaterial.SetFloat("_Height_Scale", editContext.heightWorldSpaceSize / this.terrain.terrainData.size.y);

            RenderTexture oldRT = RenderTexture.active;
            RenderTexture targetRt = null;
            RenderTexture sourceRt = editContext.destinationRenderTexture;
            Texture2D targetTex = null;

            this.CheckOrInitData();

            this.dstPixels = editContext.GetClippedPixelRectInTerrainPixels(tileIndex);  //画笔触及的区域（地型的相对坐标）
            this.sourcePixels = editContext.GetClippedPixelRectInRenderTexturePixels(tileIndex); //画笔与当前地型块重叠的区域 （相对于画笔图章）

            targetRt = this.rtHeightMapList[heightMapIdx];
            targetTex = this.heightMapList[heightMapIdx];
            this.changedIds[heightMapIdx] = true;


            RenderTexture.active = targetRt;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, targetRt.width, 0, targetRt.height);
            {
                FilterMode oldFilterMode = sourceRt.filterMode;
                sourceRt.filterMode = FilterMode.Point;

                blitMaterial.SetTexture("_MainTex", sourceRt);
                blitMaterial.SetPass(0);
                TerrainExpand.DrawQuad(dstPixels, sourcePixels, sourceRt);

                sourceRt.filterMode = oldFilterMode;
            }
            GL.PopMatrix();

            //Rect readRect = new Rect(this.dstPixels.center.x - this.dstPixels.width / 2, this.dstPixels.center.y - this.dstPixels.height / 2, this.dstPixels.width, this.dstPixels.height);
            //targetTex.ReadPixels(readRect, (int)readRect.x, (int)readRect.y);
            targetTex.ReadPixels(new Rect(0, 0, targetRt.width, targetRt.height), 0, 0);
            targetTex.Apply();
            RenderTexture.active = oldRT;
        }

        public void SaveData()
        {
            for (int i = 0; i < this.changedIds.Count; ++i)
            {
                if (this.changedIds[i])
                {
                    string path = System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_heightmap{i}.asset");

                    if (File.Exists(path))
                    {
                        AssetDatabase.SaveAssets();
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(this.heightMapList[i], path);
                        AssetDatabase.ImportAsset(path);
                        this.heightMapList[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    }
                }
            }

        }

        public void DeleteLayer(int delIdx, float scale, LoadHeightMapType reloadType)
        {
            if (!this.baseHeightMap)
            {
                return;
            }

            this.CheckOrInitData();

            float[,] heights = new float[this.baseHeightMap.width, this.baseHeightMap.height];
            for (int y = 0; y < this.baseHeightMap.height; ++y)
            {
                for (int x = 0; x < this.baseHeightMap.width; ++x)
                {
                    heights[y, x] = this.baseHeightMap.GetPixel(x, y).r * scale;

                    float addHeight = 0;

                    for (int i = 0; i < this.heightMapList.Count; ++i)
                    {
                        if (TerrainManager.OnlyLoadSelectedLayer && !TerrainManager.SelectedLayer[i])
                        {
                            continue;
                        }

                        if (i != delIdx)
                        {
                            Vector4 value = this.heightMapList[i].GetPixel(x, y);
                            float height = value.x + value.y;
                            float v = Mathf.Max(0, height);

                            if (reloadType == LoadHeightMapType.HeightSum)
                            {
                                addHeight += v;
                            }
                            else if (reloadType == LoadHeightMapType.MaxHeight)
                            {
                                addHeight = Mathf.Max(v, addHeight);
                            }
                        }
                    }

                    heights[y, x] += addHeight * scale;
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }

        public void DeleteAllAddHeight(float scale, LoadHeightMapType reloadType)
        {
            if (!this.baseHeightMap)
            {
                return;
            }

            this.CheckOrInitData();

            float[,] heights = new float[this.baseHeightMap.width, this.baseHeightMap.height];

            for (int y = 0; y < this.baseHeightMap.height; ++y)
            {
                for (int x = 0; x < this.baseHeightMap.width; ++x)
                {
                    heights[y, x] = this.baseHeightMap.GetPixel(x, y).r * scale;
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }

        public void ReLoadLayer(float scale, LoadHeightMapType reloadType)
        {
            if (!this.baseHeightMap)
            {
                return;
            }

            this.CheckOrInitData();

            float[,] heights = new float[this.baseHeightMap.width, this.baseHeightMap.height];
            for (int y = 0; y < this.baseHeightMap.height; ++y)
            {
                for (int x = 0; x < this.baseHeightMap.width; ++x)
                {
                    heights[y, x] = this.baseHeightMap.GetPixel(x, y).r * scale;

                    float addHeight = 0;

                    for (int i = 0; i < this.heightMapList.Count; ++i)
                    {
                        if (TerrainManager.OnlyLoadSelectedLayer && !TerrainManager.SelectedLayer[i])
                        {
                            continue;
                        }

                        Vector4 value = this.heightMapList[i].GetPixel(x, y);
                        float height = value.x + value.y;
                        float v = Mathf.Max(0, height);

                        if (reloadType == LoadHeightMapType.HeightSum)
                        {
                            addHeight += v;
                        }
                        else if (reloadType == LoadHeightMapType.MaxHeight)
                        {
                            addHeight = Mathf.Max(v, addHeight);
                        }
                    }

                    heights[y, x] += addHeight * scale;
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }

        static void CopyRtToTexture2D(RenderTexture rt, Texture2D tex)
        {
            RenderTexture oldRT = RenderTexture.active;

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);

            RenderTexture.active = oldRT;
        }

        static void DrawQuad(RectInt destinationPixels, RectInt sourcePixels, Texture sourceTexture)
        {
            DrawQuad2(destinationPixels, sourcePixels, sourceTexture, sourcePixels, sourceTexture);
        }

        static void DrawQuad2(RectInt destinationPixels, RectInt sourcePixels, Texture sourceTexture, RectInt sourcePixels2, Texture sourceTexture2)
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

#endif
    }
}

