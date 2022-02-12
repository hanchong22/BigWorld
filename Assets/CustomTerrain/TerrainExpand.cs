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

        static float kMaxHeight = (32766.0f / 65535.0f);

        private List<Texture2D> heightMapList;
        private List<Texture2D> originMapList;

        private List<Texture2D> holeMapList;


        private Texture2D baseHeightMap;
        private Texture2D baseHoleMap;

        private TerrainData terrainData;
        private Terrain terrain;
        private string terrainDataPath;

        private string baseHeightMapPath;

        public bool DrawCenterFlag;


        public List<RenderTexture> rtHeightMapList { get; private set; } = new List<RenderTexture>();
        public List<RenderTexture> rtHoleMapList { get; private set; } = new List<RenderTexture>();

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

        private void OnDrawGizmos()
        {

        }

        public void InitHeightMaps()
        {
            this.changedIds.Clear();

            if (this.heightMapList == null)
            {
                this.heightMapList = new List<Texture2D>();
            }

            if (this.originMapList == null)
            {
                this.originMapList = new List<Texture2D>();
            }

            if (this.holeMapList == null)
            {
                this.holeMapList = new List<Texture2D>();
            }

            for (int i = 0; i < TerrainManager.HeightMapNumber; ++i)
            {
                RenderTexture rt = null;
                Texture2D tex = null;
                Texture2D texOrigin = null;

                RenderTexture rtHole = null;
                Texture2D holeTex = null;

                this.changedIds.Add(false);

                if (this.heightMapList.Count > i)
                {
                    tex = this.heightMapList[i];
                }

                if (this.holeMapList.Count > i)
                {
                    holeTex = this.holeMapList[i];
                }

                if (this.originMapList.Count > i)
                {
                    texOrigin = this.originMapList[i];
                }

                if (this.rtHeightMapList.Count > i)
                {
                    rt = this.rtHeightMapList[i];
                }

                if (this.rtHoleMapList.Count > i)
                {
                    rtHole = this.rtHoleMapList[i];
                }

                if (!rt)
                {
                    rt = RenderTexture.GetTemporary(this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height, 0, RenderTextureFormat.ARGBHalf);
                }

                if (!rtHole)
                {
                    rtHole = RenderTexture.GetTemporary(this.terrainData.holesResolution, this.terrainData.holesResolution, 0, Terrain.holesRenderTextureFormat);
                }

                if (!tex)
                {
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_heightmap{i}.asset"));

                    if (!tex)
                    {
                        tex = new Texture2D(this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height, TextureFormat.RGBAHalf, false, true);
                        Graphics.Blit(Texture2D.blackTexture, rt);
                        CopyRtToTexture2D(rt, tex);
                        tex.Apply();

                        AssetDatabase.CreateAsset(tex, System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_heightmap{i}.asset"));
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

                if (!holeTex)
                {
                    holeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_hole{i}.asset"));
                    if (!holeTex)
                    {
                        holeTex = new Texture2D(this.terrainData.holesResolution, this.terrainData.holesResolution, TextureFormat.R8, false);
                        Graphics.Blit(Texture2D.blackTexture, rtHole);
                        CopyRtToTexture2D(rtHole, holeTex);
                        holeTex.Apply();

                        AssetDatabase.CreateAsset(holeTex, System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_hole{i}.asset"));
                    }
                    else
                    {
                        Graphics.Blit(holeTex, rtHole);
                    }
                }
                else
                {
                    Graphics.Blit(holeTex, rtHole);
                }

                if (!texOrigin && File.Exists(System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_heightmap_origin{i}.asset")))
                {
                    texOrigin = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_heightmap_origin{i}.asset"));
                }


                if (this.rtHeightMapList.Count > i)
                {
                    this.rtHeightMapList[i] = rt;
                }
                else
                {
                    this.rtHeightMapList.Add(rt);
                }

                if (this.rtHoleMapList.Count > i)
                {
                    this.rtHoleMapList[i] = rtHole;
                }
                else
                {
                    rtHoleMapList.Add(rtHole);
                }

                if (this.heightMapList.Count > i)
                {
                    this.heightMapList[i] = tex;
                }
                else
                {
                    this.heightMapList.Add(tex);
                }

                if (this.holeMapList.Count > i)
                {
                    this.holeMapList[i] = holeTex;
                }
                else
                {
                    this.holeMapList.Add(holeTex);
                }


                if (this.originMapList.Count > i)
                {
                    this.originMapList[i] = texOrigin;
                }
                else
                {
                    this.originMapList.Add(texOrigin);
                }
            }

            this.baseHeightMapPath = System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_baseHeightMap.asset");

            if (!this.baseHeightMap)
            {
                this.baseHeightMap = AssetDatabase.LoadAssetAtPath<Texture2D>(this.baseHeightMapPath);
                if (!this.baseHeightMap)
                {
                    this.baseHeightMap = new Texture2D(this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height, TextureFormat.RGBAHalf, false, true);

                    float[,] baseHeights = this.terrainData.GetHeights(0, 0, this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height);

                    for (int y = 0; y < this.terrainData.heightmapTexture.height; ++y)
                    {
                        for (int x = 0; x < this.terrainData.heightmapTexture.width; ++x)
                        {
                            this.baseHeightMap.SetPixel(x, y, new Color(baseHeights[y, x], 0, 0, 0));
                        }
                    }

                    this.baseHeightMap.Apply();

                    AssetDatabase.CreateAsset(this.baseHeightMap, this.baseHeightMapPath);

                }
            }

            if (!this.baseHoleMap)
            {
                this.baseHoleMap = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_baseHoleMap.asset"));
                if (!this.baseHoleMap)
                {
                    this.baseHoleMap = new Texture2D(this.terrainData.holesResolution, this.terrainData.holesResolution, TextureFormat.R8, false, false);
                    bool[,] baseHoles = this.terrainData.GetHoles(0, 0, this.terrainData.holesResolution, this.terrainData.holesResolution);


                    for (int y = 0; y < this.terrainData.holesResolution; ++y)
                    {
                        for (int x = 0; x < this.terrainData.holesResolution; ++x)
                        {
                            this.baseHoleMap.SetPixel(x, y, new Color(baseHoles[y, x] ? 0 : 1, 0, 0, 0));
                        }
                    }

                    this.baseHoleMap.Apply();

                    AssetDatabase.CreateAsset(this.baseHoleMap, System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_baseHoleMap.asset"));
                }
            }

            AssetDatabase.SaveAssets();
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

            if (this.rtHoleMapList != null)
            {
                for (int i = 0; i < this.rtHoleMapList.Count; ++i)
                {
                    if (this.rtHoleMapList[i])
                    {
                        RenderTexture.ReleaseTemporary(this.rtHoleMapList[i]);
                    }
                }

                this.rtHoleMapList.Clear();
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

            if (this.holeMapList != null)
            {
                for (int i = 0; i < this.holeMapList.Count; ++i)
                {
                    if (this.holeMapList[i])
                    {
                        GameObject.DestroyImmediate(this.holeMapList[i]);
                    }
                }

                this.holeMapList.Clear();
            }

            if (this.originMapList != null)
            {
                for (int i = 0; i < this.originMapList.Count; ++i)
                {
                    if (this.originMapList[i])
                    {
                        GameObject.DestroyImmediate(this.originMapList[i]);
                    }
                }

                this.originMapList.Clear();
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

        public void OnPaint(int heightMapIdx, UnityEngine.Experimental.TerrainAPI.PaintContext editContext, int tileIndex, int heightNormal)
        {
            Material blitMaterial = TerrainManager.GetHeightSubtractionMat();

            this.CheckOrInitData();

            blitMaterial.SetFloat("_Height_Offset", (editContext.heightWorldSpaceMin - this.terrain.GetPosition().y) / this.terrain.terrainData.size.y * TerrainExpand.kNormalizedHeightScale);
            blitMaterial.SetFloat("_Height_Scale", editContext.heightWorldSpaceSize / this.terrain.terrainData.size.y);

            RenderTexture oldRT = RenderTexture.active;
            RenderTexture targetRt = null;
            RenderTexture sourceRt = editContext.destinationRenderTexture;      //已经绘制结果：原地型高度 + 笔刷   
                                                                                // RenderTexture oldTerrainHeight = editContext.sourceRenderTexture;   //原地型高度
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
                sourceRt.filterMode = FilterMode.Bilinear;

                blitMaterial.SetTexture("_MainTex", sourceRt);
                blitMaterial.SetInt("_HeightNormal", heightNormal);
                blitMaterial.SetPass(0);
                TerrainManager.DrawQuad(dstPixels, sourcePixels, sourceRt);

                sourceRt.filterMode = oldFilterMode;
            }
            GL.PopMatrix();

            targetTex.ReadPixels(new Rect(0, 0, targetRt.width, targetRt.height), 0, 0);
            targetTex.Apply();
            RenderTexture.active = oldRT;
        }

        public void OnPainHole(int heightMapIdx, UnityEngine.Experimental.TerrainAPI.PaintContext editContext, int tileIndex)
        {
            Material blitMaterial = TerrainManager.GetHeightSubtractionMat();

            this.CheckOrInitData();

            blitMaterial.SetFloat("_Height_Offset", (editContext.heightWorldSpaceMin - this.terrain.GetPosition().y) / this.terrain.terrainData.size.y * TerrainExpand.kNormalizedHeightScale);
            blitMaterial.SetFloat("_Height_Scale", editContext.heightWorldSpaceSize / this.terrain.terrainData.size.y);

            RenderTexture oldRT = RenderTexture.active;
            RenderTexture targetRt = null;
            RenderTexture sourceRt = editContext.destinationRenderTexture;      //已经绘制结果：原地型hole + 笔刷   
                                                                                // RenderTexture oldTerrainHeight = editContext.sourceRenderTexture;   //原地型高度
            Texture2D targetTex = null;

            this.CheckOrInitData();

            this.dstPixels = editContext.GetClippedPixelRectInTerrainPixels(tileIndex);         //画笔触及的区域（地型的相对坐标）
            this.sourcePixels = editContext.GetClippedPixelRectInRenderTexturePixels(tileIndex); //画笔与当前地型块重叠的区域 （相对于画笔图章）

            targetRt = this.rtHoleMapList[heightMapIdx];
            targetTex = this.holeMapList[heightMapIdx];
            this.changedIds[heightMapIdx] = true;


            RenderTexture.active = targetRt;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, targetRt.width, 0, targetRt.height);
            {
                FilterMode oldFilterMode = sourceRt.filterMode;
                sourceRt.filterMode = FilterMode.Bilinear;

                blitMaterial.SetTexture("_MainTex", sourceRt);
                blitMaterial.SetPass(1);
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

                    string holePath = System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_hole{i}.asset");

                    if (File.Exists(holePath))
                    {
                        AssetDatabase.SaveAssets();
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(this.holeMapList[i], holePath);
                        AssetDatabase.ImportAsset(holePath);
                        this.holeMapList[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(holePath);
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
            Texture2D waitToDelTex2 = this.originMapList[idx];
            RenderTexture waitToDelRt = this.rtHeightMapList[idx];
            Texture2D waitToDelHole = this.holeMapList[idx];
            RenderTexture waitToDelRtHole = this.rtHoleMapList[idx];

            string path = AssetDatabase.GetAssetPath(waitToDelTex);
            string path2 = waitToDelTex2 ? AssetDatabase.GetAssetPath(waitToDelTex2) : null;
            string path3 = waitToDelHole ? AssetDatabase.GetAssetPath(waitToDelHole) : null;

            AssetDatabase.DeleteAsset(path);
            if(File.Exists(path))
            {
                File.Delete(path);
            }

            GameObject.DestroyImmediate(waitToDelTex);
            RenderTexture.ReleaseTemporary(waitToDelRt);

            if (waitToDelRtHole)
            {
                RenderTexture.ReleaseTemporary(waitToDelRtHole);
            }

            if (waitToDelHole)
            {               
                AssetDatabase.DeleteAsset(path3);
                if (File.Exists(path3))
                {
                    File.Delete(path3);
                }

                GameObject.DestroyImmediate(waitToDelHole);
            }


            if (waitToDelTex2)
            {                
                AssetDatabase.DeleteAsset(path2);
                if (File.Exists(path2))
                {
                    File.Delete(path2);
                }

                GameObject.Destroy(waitToDelTex2);
            }


            this.heightMapList.RemoveAt(idx);
            this.rtHeightMapList.RemoveAt(idx);
            this.originMapList.RemoveAt(idx);
        }

        public void ReLoadLayer(float scale)
        {
            if (!this.baseHeightMap)
            {
                return;
            }

            this.CheckOrInitData();

            float targetHeight = TerrainManager.BrashTargetHeight / terrain.terrainData.size.y;

            var addMaterial = TerrainManager.GetHeightmapBlitExtMat();

            addMaterial.SetFloat("_Height_Offset", 0.0f);
            addMaterial.SetFloat("_Height_Scale", scale);
            addMaterial.SetFloat("_Target_Height", targetHeight);
            addMaterial.EnableKeyword("_HEIGHT_TYPE");
            addMaterial.DisableKeyword("_HOLE_TYPE");

            float[,] heights = new float[this.baseHeightMap.width, this.baseHeightMap.height];

            RenderTexture rtTmp1 = RenderTexture.GetTemporary(this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height, 0, RenderTextureFormat.ARGBHalf);
            RenderTexture rtTmp2 = RenderTexture.GetTemporary(this.terrainData.heightmapTexture.width, this.terrainData.heightmapTexture.height, 0, RenderTextureFormat.ARGBHalf);

            Graphics.Blit(Texture2D.blackTexture, rtTmp1);

            List<Texture2D> allHeightMap = ListPool<Texture2D>.Get();

            if (TerrainManager.IsBaseLayerEnable)
            {
                allHeightMap.Add(this.baseHeightMap);
            }

            for (int i = 0; i < this.heightMapList.Count; ++i)
            {
                if (!TerrainManager.SelectedLayer[i] || !this.heightMapList[i])
                {
                    continue;
                }

                allHeightMap.Add(this.heightMapList[i]);
            }

            for (int i = 0; i < allHeightMap.Count; ++i)
            {
                addMaterial.SetTexture("_Tex1", rtTmp1);
                addMaterial.SetTexture("_Tex2", allHeightMap[i]);
                int idx = this.heightMapList.IndexOf(allHeightMap[i]);

                addMaterial.SetFloat("_Overlay_Layer", idx >= 0 && TerrainManager.OverlayLayers[idx] ? 1 : 0.0f);

                Graphics.Blit(null, rtTmp2, addMaterial);
                Graphics.Blit(rtTmp2, rtTmp1);
            }

            ListPool<Texture2D>.Release(allHeightMap);

            Texture2D texTmp = new Texture2D(this.baseHeightMap.width, this.baseHeightMap.height, TextureFormat.RGBAHalf, false);
            CopyRtToTexture2D(rtTmp1, texTmp);
            texTmp.Apply();

            for (int y = 0; y < texTmp.height; ++y)
            {
                for (int x = 0; x < texTmp.width; ++x)
                {
                    Vector4 value = texTmp.GetPixel(x, y);

                    float height = value.x + value.y;

                    heights[y, x] = Mathf.Clamp(height, 0, 1);
                }
            }

            terrainData.SetHeights(0, 0, heights);

            RenderTexture.ReleaseTemporary(rtTmp1);
            RenderTexture.ReleaseTemporary(rtTmp2);
            Texture2D.DestroyImmediate(texTmp);

            rtTmp1 = RenderTexture.GetTemporary(this.terrainData.holesResolution, this.terrainData.holesResolution, 0, RenderTextureFormat.R8);
            rtTmp2 = RenderTexture.GetTemporary(this.terrainData.holesResolution, this.terrainData.holesResolution, 0, RenderTextureFormat.R8);

            addMaterial.SetFloat("_Overlay_Layer", 0.0f);
            addMaterial.DisableKeyword("_HEIGHT_TYPE");
            addMaterial.EnableKeyword("_HOLE_TYPE");

            List<Texture2D> allHoleMaps = ListPool<Texture2D>.Get();
            if (TerrainManager.IsBaseLayerEnable)
            {
                allHoleMaps.Add(this.baseHoleMap);
            }

            for (int i = 0; i < this.heightMapList.Count; ++i)
            {
                if (!TerrainManager.SelectedLayer[i] || !this.heightMapList[i])
                {
                    continue;
                }

                allHoleMaps.Add(this.holeMapList[i]);
            }

            for (int i = 1; i < allHoleMaps.Count; ++i)
            {
                Texture src = null;
                if (i == 1)
                {
                    src = allHoleMaps[0];
                }
                else
                {
                    src = rtTmp1;
                }

                addMaterial.SetTexture("_Tex1", src);
                addMaterial.SetTexture("_Tex2", allHoleMaps[i]);

                Graphics.Blit(null, rtTmp2, addMaterial);
                Graphics.Blit(rtTmp2, rtTmp1);
            }

            ListPool<Texture2D>.Release(allHoleMaps);

            texTmp = new Texture2D(this.terrainData.holesResolution, this.terrainData.holesResolution, TextureFormat.R8, false);
            CopyRtToTexture2D(rtTmp1, texTmp);
            texTmp.Apply();

            bool[,] holes = new bool[this.terrainData.holesResolution, this.terrainData.holesResolution];
            for (int y = 0; y < this.terrainData.holesResolution; ++y)
            {
                for (int x = 0; x < this.terrainData.holesResolution; ++x)
                {
                    bool hole = texTmp.GetPixel(x, y).r < 0.5f;

                    holes[y, x] = hole;
                }
            }

            terrainData.SetHoles(0, 0, holes);

            Texture2D.DestroyImmediate(texTmp);
            RenderTexture.ReleaseTemporary(rtTmp1);
            RenderTexture.ReleaseTemporary(rtTmp2);
        }

        public bool ReimportHeightmap(int heighMapID, Texture2D newTex, float scale, int limitType)
        {
            if (newTex.width != this.terrainData.heightmapTexture.width || newTex.height != this.terrainData.heightmapTexture.height)
            {
                Debug.LogError($"导入的高度图尺寸与地型不符，必须使用{ this.terrainData.heightmapTexture.width} * { this.terrainData.heightmapTexture.height}的高度图，当前导入的图为{newTex.width} * {newTex.height}");
                return false;
            }

            float maxValue = newTex.GetMaxValueFromTexture(false);
            float targetHeight = TerrainManager.BrashTargetHeight / terrain.terrainData.size.y;
            float s = maxValue > targetHeight && limitType == 1 ? targetHeight : 1;

            if (heighMapID < 0)
            {
                for (int y = 0; y < newTex.height; ++y)
                {
                    for (int x = 0; x < newTex.width; ++x)
                    {
                        Vector4 scolor = newTex.GetPixel(x, y);
                        Vector4 color = new Vector4(Mathf.Max(0, scolor.x) * s, Mathf.Max(0, scolor.y) * s, 0, 0);

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
                            float height = Mathf.Max(0, scolor.x) * s;
                            Vector4 color = new Vector4(height / 2f, height / 2f, 0, 0);

                            this.heightMapList[heighMapID].SetPixel(x, y, color);
                        }
                    }

                    this.heightMapList[heighMapID].Apply();

                    Graphics.Blit(this.heightMapList[heighMapID], this.rtHeightMapList[heighMapID]);

                    if (this.originMapList[heighMapID])
                    {
                        CopyRtToTexture2D(this.rtHeightMapList[heighMapID], this.originMapList[heighMapID]);
                    }

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

        public bool ReimportHeightData(int heighMapID, byte[] data, float scale, int resolution, int limitType)
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

                }
            }

            float maxValue = heights.GetMaxValueFromRawData(heightmapRes);
            float targetHeight = TerrainManager.BrashTargetHeight / terrain.terrainData.size.y;

            float s = maxValue > targetHeight && limitType == 1 ? targetHeight : 1;

            for (int y = 0; y < heightmapRes; ++y)
            {
                for (int x = 0; x < heightmapRes; ++x)
                {
                    float height = heights[y, x];
                    heights[y, x] = height;

                    height = Mathf.Max(0, height) * s;
                    Vector4 color = new Vector4(height / 2f, height / 2f, 0, 0);
                    tex.SetPixel(y, x, color);
                }
            }

            if (heighMapID > 0)
            {
                Graphics.Blit(this.heightMapList[heighMapID], this.rtHeightMapList[heighMapID]);

                if (this.originMapList[heighMapID])
                {
                    CopyRtToTexture2D(this.rtHeightMapList[heighMapID], this.originMapList[heighMapID]);
                }
            }

            AssetDatabase.SaveAssets();

            this.ReLoadLayer(scale);
            return true;
        }

        public void RotaitonLayer(int heighMapID, float angle, Vector4 pivot, float layerScale, float layerHeightScale, float scale)
        {
            RenderTexture rt;
            Texture2D tex;

            Texture2D originTex;

            if (this.rtHeightMapList.Count > heighMapID && this.heightMapList.Count > heighMapID)
            {
                if (!this.rtHeightMapList[heighMapID] || !this.heightMapList[heighMapID])
                {
                    this.InitHeightMaps();
                }

                rt = this.rtHeightMapList[heighMapID];
                tex = this.heightMapList[heighMapID];

                if (!this.originMapList[heighMapID])
                {
                    this.originMapList[heighMapID] = new Texture2D(tex.width, tex.height, tex.format, false);
                    CopyRtToTexture2D(rt, this.originMapList[heighMapID]);
                    this.originMapList[heighMapID].Apply();

                    AssetDatabase.CreateAsset(this.originMapList[heighMapID], System.IO.Path.Combine(this.terrainDataPath, $"{this.terrainData.name}_heightmap_origin{heighMapID}.asset"));
                }

                originTex = this.originMapList[heighMapID];
            }
            else
            {
                Debug.LogError($"{heighMapID} 不存在，地型是否没有初始化？");
                return;
            }

            if (!TerrainManager.RotationMaterial)
            {
                TerrainManager.RotationMaterial = CoreUtils.CreateEngineMaterial("Hidden/TerrainEngine/RotationLayer");
            }

            TerrainManager.RotationMaterial.SetTexture("_MainTex", originTex);
            TerrainManager.RotationMaterial.SetFloat("_Angle", angle);
            TerrainManager.RotationMaterial.SetFloat("_Scale", layerScale);
            TerrainManager.RotationMaterial.SetFloat("_HeightScale", layerHeightScale);
            TerrainManager.RotationMaterial.SetVector("_Pivot", pivot);

            Graphics.Blit(tex, rt, TerrainManager.RotationMaterial, 0);
            CopyRtToTexture2D(rt, tex);
            tex.Apply();

            AssetDatabase.SaveAssets();

            this.ReLoadLayer(scale);

        }

        public void MergeHeightMapWithUpper(int idx)
        {
            if(this.heightMapList.Count <= idx || this.originMapList.Count <= idx || this.rtHeightMapList.Count <= idx || this.holeMapList.Count <= idx || this.rtHoleMapList.Count <= idx)
            {
                this.CheckOrInitData();
            }

            Texture2D dstTex = this.heightMapList[idx - 1];
            Texture2D dstTex2 = this.originMapList[idx - 1];
            RenderTexture dstRt = this.rtHeightMapList[idx - 1];
            Texture2D dstHole = this.holeMapList[idx - 1];
            RenderTexture dstRtHole = this.rtHoleMapList[idx - 1];

            Texture2D sourceTex = this.heightMapList[idx];
            Texture2D sourceTex2 = this.originMapList[idx];
            RenderTexture sourceRt = this.rtHeightMapList[idx];
            Texture2D sourceHole = this.holeMapList[idx];
            RenderTexture sourceRtHole = this.rtHoleMapList[idx];

            var addMat = TerrainManager.GetHeightmapBlitExtMat();

            addMat.SetFloat("_Overlay_Layer", 0);
            addMat.SetTexture("_Tex1", dstTex);
            addMat.SetTexture("_Tex2", sourceTex);
            addMat.SetFloat("_Height_Offset", 0.0f);
            addMat.SetFloat("_Height_Scale", 1.0f);
            addMat.SetFloat("_Target_Height", 1.0f);
            addMat.SetFloat("_Overlay_Layer", 0.0f);
            addMat.EnableKeyword("_HEIGHT_TYPE");
            addMat.DisableKeyword("_HOLE_TYPE");

            Graphics.Blit(null, dstRt, addMat);
            CopyRtToTexture2D(dstRt, dstTex);
            dstTex.Apply();

            if (dstTex2 && sourceTex2)
            {
                var tmpTarget = RenderTexture.GetTemporary(dstTex2.width, dstTex2.height, 0, dstRt.format, RenderTextureReadWrite.Linear);
                addMat.SetTexture("_Tex1", dstTex2);
                addMat.SetTexture("_Tex2", sourceTex2);
                Graphics.Blit(null, tmpTarget, addMat);
                CopyRtToTexture2D(tmpTarget, dstTex2);
                dstTex2.Apply();
                RenderTexture.ReleaseTemporary(tmpTarget);
            }

            if (dstHole && sourceHole && dstRtHole)
            {
                addMat.SetTexture("_Tex1", dstHole);
                addMat.SetTexture("_Tex2", sourceHole);
                addMat.DisableKeyword("_HEIGHT_TYPE");
                addMat.EnableKeyword("_HOLE_TYPE");
                Graphics.Blit(null, dstRtHole, addMat);
                CopyRtToTexture2D(dstRtHole, dstHole);
                dstHole.Apply();
            }

            AssetDatabase.SaveAssets();
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

