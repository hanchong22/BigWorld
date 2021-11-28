using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
#endif

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

        public void OnPaint(int heightMapIdx, UnityEngine.Experimental.TerrainAPI.PaintContext editContext, int tileIndex, Material brushMat)
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
            if (idx >= this.heightMapList.Count)
            {
                return;
            }

            Texture2D waitToDelTex = this.heightMapList[idx];
            RenderTexture waitToDelRt = this.rtHeightMapList[idx];

            string path = AssetDatabase.GetAssetPath(waitToDelTex);

            AssetDatabase.DeleteAsset(path);
            GameObject.DestroyImmediate(waitToDelTex);
            RenderTexture.ReleaseTemporary(waitToDelRt);


            for (int i = idx + 1; i < this.heightMapList.Count; ++i)
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

            float targetHeight = TerrainManager.BrashTargetHeight / terrain.terrainData.size.y;

            float[,] heights = new float[this.baseHeightMap.width, this.baseHeightMap.height];
            for (int y = 0; y < this.baseHeightMap.height; ++y)
            {
                for (int x = 0; x < this.baseHeightMap.width; ++x)
                {
                    float addHeight = TerrainManager.IsBaseLayerEnable ? this.baseHeightMap.GetPixel(x, y).r : 0;

                    for (int i = 0; i < this.heightMapList.Count; ++i)
                    {
                        if (!TerrainManager.SelectedLayer[i])
                        {
                            continue;
                        }

                        Vector4 value = this.heightMapList[i].GetPixel(x, y);
                        float height = value.x + value.y; 
                        float v = Mathf.Clamp01(height);
                        addHeight += v;
                    }

                    heights[y, x] = Mathf.Clamp(addHeight * scale, 0, targetHeight);
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }

        public bool ReimportHeightmap(int heighMapID, Texture2D newTex, float scale)
        {
            if (newTex.width != this.terrainData.heightmapTexture.width || newTex.height != this.terrainData.heightmapTexture.height)
            {
                Debug.LogError($"导入的高度图尺寸与地型不符，必须使用{ this.terrainData.heightmapTexture.width} * { this.terrainData.heightmapTexture.height}的高度图，当前导入的图为{newTex.width} * {newTex.height}");
                return false;
            }

            if (heighMapID < 0)
            {
                for (int y = 0; y < newTex.height; ++y)
                {
                    for (int x = 0; x < newTex.width; ++x)
                    {
                        Vector4 scolor = newTex.GetPixel(x, y);
                        Vector4 color = new Vector4(Mathf.Max(0, scolor.x), 0, 0, 0);

                        this.baseHeightMap.SetPixel(x, y, color);
                    }
                }

                this.baseHeightMap.Apply();

                AssetDatabase.SaveAssets();
            }
            else
            {
                if (this.heightMapList.Count > heighMapID)
                {
                    if (!this.heightMapList[heighMapID])
                    {
                        this.InitHeightMaps();
                    }

                    for (int y = 0; y < newTex.height; ++y)
                    {
                        for (int x = 0; x < newTex.width; ++x)
                        {
                            Vector4 scolor = newTex.GetPixel(x, y);
                            Vector4 color = new Vector4(Mathf.Max(0, scolor.x), 0, 0, 0);

                            this.heightMapList[heighMapID].SetPixel(x, y, color);
                        }
                    }

                    this.heightMapList[heighMapID].Apply();

                    Graphics.Blit(this.heightMapList[heighMapID], this.rtHeightMapList[heighMapID]);

                    AssetDatabase.SaveAssets();
                }
                else
                {
                    Debug.LogError($"{heighMapID} 不存在，地型是否没有初始化？");
                }
            }

            this.ReLoadLayer(scale);
            return true;
        }

        public bool ReimportHeightData(int heighMapID, byte[] data, float scale, int resolution)
        {
            if (resolution != this.terrainData.heightmapResolution)
            {
                Debug.LogError($"导入数据尺寸为{resolution}，地型尺寸为{this.terrainData.heightmapResolution}，两者不一至，无法导入");
                return false;
            }

            this.CheckOrInitData();

            Texture2D tex;
            if (heighMapID < 0)
            {
                tex = this.baseHeightMap;
            }
            else
            {
                if (this.heightMapList.Count > heighMapID)
                {
                    if (!this.heightMapList[heighMapID])
                    {
                        this.InitHeightMaps();
                    }

                    tex = this.heightMapList[heighMapID];

                    Graphics.Blit(this.heightMapList[heighMapID], this.rtHeightMapList[heighMapID]);

                }
                else
                {
                    Debug.LogError($"{heighMapID} 不存在，地型是否没有初始化？");
                    return false;
                }
            }

            int heightmapRes = terrainData.heightmapResolution;
            float[,] heights = new float[heightmapRes, heightmapRes];

            float normalize = 1.0F / (1 << 16);
            for (int y = 0; y < heightmapRes; ++y)
            {
                for (int x = 0; x < heightmapRes; ++x)
                {
                    int index = Mathf.Clamp(x, 0, heightmapRes - 1) + Mathf.Clamp(y, 0, heightmapRes - 1) * heightmapRes;

                    ushort compressedHeight = System.BitConverter.ToUInt16(data, index * 2);

                    float height = compressedHeight * normalize;
                    heights[y, x] = height;

                    Vector4 color = new Vector4(Mathf.Max(0, height), 0, 0, 0);
                    tex.SetPixel(y, x, color);
                }
            }

            AssetDatabase.SaveAssets();
            if (heighMapID > 0)
            {
                Graphics.Blit(this.heightMapList[heighMapID], this.rtHeightMapList[heighMapID]);
            }

            this.ReLoadLayer(scale);
            return true;
        }


        public Texture2D GetMergedTexture(List<int> ids)
        {
            Texture2D tex = new Texture2D(this.baseHeightMap.width, this.baseHeightMap.height, TextureFormat.RHalf, false);

            for (int y = 0; y < tex.height; ++y)
            {
                for (int x = 0; x < tex.width; ++x)
                {
                    Vector4 color = Vector4.zero;

                    int startIdx = 0;
                    if (ids[0] == -1)
                    {
                        startIdx = 1;
                        Vector4 scolor = this.baseHeightMap.GetPixel(x, y);
                        color = new Vector4(Mathf.Max(0, scolor.x), 0, 0, 0);
                    }

                    for (int i = startIdx; i < ids.Count; ++i)
                    {
                        int id = ids[i];
                        if (id < 0)
                        {
                            continue;
                        }

                        if (this.heightMapList.Count < id || !this.heightMapList[id])
                        {
                            Debug.LogError($"layer {id} is not exists in {this.name}");
                            this.InitHeightMaps();
                        }

                        Vector4 scolor = this.heightMapList[id].GetPixel(x, y);

                        color = new Vector4(Mathf.Clamp01(color.x + Mathf.Max(0, scolor.x) + Mathf.Max(0, scolor.y)), 0, 0, 0);
                    }

                    tex.SetPixel(x, y, color);
                }
            }

            tex.Apply();

            return tex;
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

