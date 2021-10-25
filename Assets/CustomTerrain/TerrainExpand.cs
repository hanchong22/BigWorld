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
            Material blitMaterial = TerrainManager.GetHeightSubtractionMat();

            this.CheckOrInitData();

            blitMaterial.SetFloat("_Height_Offset", (editContext.heightWorldSpaceMin - this.terrain.GetPosition().y) / this.terrain.terrainData.size.y * TerrainExpand.kNormalizedHeightScale);
            blitMaterial.SetFloat("_Height_Scale", editContext.heightWorldSpaceSize / this.terrain.terrainData.size.y);

            RenderTexture oldRT = RenderTexture.active;
            RenderTexture targetRt = null;
            RenderTexture sourceRt = editContext.destinationRenderTexture;      //绘制结果：原地型高度 + 笔刷   
            RenderTexture oldTerrainHeight = editContext.sourceRenderTexture;   //原地型高度
            Texture2D targetTex = null;

            this.CheckOrInitData();

            this.dstPixels = editContext.GetClippedPixelRectInTerrainPixels(tileIndex);         //画笔触及的区域（地型的相对坐标）
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
                blitMaterial.SetTexture("_OldHeightMap", oldTerrainHeight);
                blitMaterial.SetPass(0);
                TerrainManager.DrawQuad(dstPixels, sourcePixels, sourceRt);

                sourceRt.filterMode = oldFilterMode;
            }
            GL.PopMatrix();

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

        public void RemoveLayer(int idx)
        {
            if(idx >= this.heightMapList.Count)
            {
                return;
            }

            Texture2D waitToDelTex = this.heightMapList[idx];
            RenderTexture waitToDelRt = this.rtHeightMapList[idx];

            string path = AssetDatabase.GetAssetPath(waitToDelTex);

            AssetDatabase.DeleteAsset(path);
            GameObject.DestroyImmediate(waitToDelTex);
            RenderTexture.ReleaseTemporary(waitToDelRt);

            
            for(int i = idx + 1; i < this.heightMapList.Count; ++i)
            {
                string tmpPath = AssetDatabase.GetAssetPath(this.heightMapList[i]);
                AssetDatabase.RenameAsset(tmpPath, path);
                this.heightMapList[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                path = tmpPath;
            }

            this.heightMapList.RemoveAt(idx);
            this.rtHeightMapList.RemoveAt(idx);            
        }

        public void ReLoadLayer(float scale)
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
                    float addHeight = this.baseHeightMap.GetPixel(x, y).r;

                    for (int i = 0; i < this.heightMapList.Count; ++i)
                    {
                        if (!TerrainManager.SelectedLayer[i])
                        {
                            continue;
                        }

                        Vector4 value = this.heightMapList[i].GetPixel(x, y);
                        float height = value.x + value.y;
                        float v = Mathf.Max(0, height);
                        addHeight += v;
                    }

                    heights[y, x] = addHeight * scale;
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
#endif
    }
}

