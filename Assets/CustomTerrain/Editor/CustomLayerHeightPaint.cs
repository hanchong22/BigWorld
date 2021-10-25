using UnityEngine;
using UnityEngine.Experimental.TerrainAPI;
using UnityEditor.ShortcutManagement;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using System.Collections.Generic;

namespace SeasunTerrain
{
    [FilePath("Library/TerrainTools/CustomLayerHeight", FilePathAttribute.Location.ProjectFolder)]
    class CustomLayerHeightPaint : TerrainPaintTool<CustomLayerHeightPaint>
    {
        private string paintName = "CustomLayerPaint";

        [SerializeField] float m_TargetHeight;
        [SerializeField] float m_HeightScale = 1;
        [SerializeField] int m_HeightMapNumber = 1;
        [SerializeField] int m_CurrentHeightMapIdx = 0;
        [SerializeField] bool[] m_selectedLyaers = new bool[] { true };
        [SerializeField] string[] m_heightMapTitles;


        class Styles
        {
            public readonly GUIContent description = EditorGUIUtility.TrTextContent("地型高度编辑器，按左键编辑高度，按Shift + 左键擦除高度。");
            public readonly GUIContent height = EditorGUIUtility.TrTextContent("画笔高度", "可以直接设置画笔高度，也可以在地形上按住shift和鼠标滚轮进行调整");
            public readonly GUIContent heightValueScale = EditorGUIUtility.TrTextContent("高度值缩放");
            public readonly GUIContent save = EditorGUIUtility.TrTextContent("保存", "保存所修改");            
        }

        [Shortcut("Terrain/Custom Layers", typeof(TerrainToolShortcutContext), KeyCode.F10)]
        static void SelectShortcut(ShortcutArguments args)
        {
            TerrainToolShortcutContext context = (TerrainToolShortcutContext)args.context;
            context.SelectPaintTool<CustomLayerHeightPaint>();
        }

        private List<TerrainExpand> waitToSaveTerrains = new List<TerrainExpand>();

        private static Styles m_styles;
        private Styles GetStyles()
        {
            if (m_styles == null)
            {
                m_styles = new Styles();
            }

            return m_styles;
        }

        public override string GetDesc()
        {
            return this.GetStyles().description.text;
        }

        public override string GetName()
        {
            return this.paintName;
        }

        public override void OnEnable()
        {
            TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx);
        }

        private Material ApplyBrushInternal(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform, Terrain terrain)
        {
            //shader :  Hidden/TerrainEngine/PaintHeight
            Material mat = TerrainPaintUtility.GetBuiltinPaintMaterial();

            float brushTargetHeight = (Event.current.shift ? -1 : 1) * Mathf.Clamp01((m_TargetHeight - paintContext.heightWorldSpaceMin) / paintContext.heightWorldSpaceSize);

            Vector4 brushParams = new Vector4(brushStrength * 0.01f, PaintContext.kNormalizedHeightScale * brushTargetHeight, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            //sourceRenderTexture:原地型高度图
            //destinationRenderTexture:目标
            //brushTexture: 笔刷
            //通过以下操作，将原地型高度图 + 笔刷，生成新的高度图到目标缓冲区destinationRenderTexture
            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.SetHeights);

            return mat;
        }

        private Material ApplyBrushFromBaseHeightInternal(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform, Terrain terrain)
        {
            Material mat = TerrainManager.GetPaintHeightExtMat();

            float brushTargetHeight = Mathf.Clamp01((m_TargetHeight - paintContext.heightWorldSpaceMin) / paintContext.heightWorldSpaceSize);

            Vector4 brushParams = new Vector4(brushStrength * 0.01f, PaintContext.kNormalizedHeightScale * brushTargetHeight, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            //sourceRenderTexture:原地型高度图
            //destinationRenderTexture:目标
            //brushTexture: 笔刷
            //通过以下操作，将原地型高度图 + 笔刷，生成新的高度图到目标缓冲区destinationRenderTexture
            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.SetHeights);

            return mat;
        }

        public override bool OnPaint(Terrain terrain, IOnPaint editContext)
        {
            BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.uv, editContext.brushSize, 0.0f);

            PaintContextExp paintContextTmp = TerrainManager.BeginPaintHeightMapLyaer(terrain, brushXform.GetBrushXYBounds(), this.m_CurrentHeightMapIdx);
            var matTmp = ApplyBrushFromBaseHeightInternal(paintContextTmp, editContext.brushStrength, editContext.brushTexture, brushXform, terrain);
            for (int i = 0; i < paintContextTmp.terrainCount; ++i)
            {
                TerrainExpand terrainExpandData = paintContextTmp.GetTerrain(i).gameObject.GetComponent<TerrainExpand>();
                if (!terrainExpandData)
                {
                    terrainExpandData = paintContextTmp.GetTerrain(i).gameObject.AddComponent<TerrainExpand>();
                }

                terrainExpandData.OnPaint(this.m_CurrentHeightMapIdx, paintContextTmp, i, matTmp);

                if (!this.waitToSaveTerrains.Contains(terrainExpandData))
                {
                    this.waitToSaveTerrains.Add(terrainExpandData);
                }
            }

            paintContextTmp.Cleanup();

            PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds());
            var mat = ApplyBrushInternal(paintContext, editContext.brushStrength, editContext.brushTexture, brushXform, terrain);

            TerrainPaintUtility.EndPaintHeightmap(paintContext, "Terrain Paint - CustomLayerHeight");

            return true;
        }

        public override void OnSceneGUI(Terrain terrain, IOnSceneGUI editContext)
        {
            Event evt = Event.current;
            if (evt.control && (evt.type == EventType.ScrollWheel))
            {

                evt.Use();
                editContext.Repaint();
            }

            if (evt.type != EventType.Repaint)
                return;

            if (editContext.hitValidTerrain)
            {
                BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.raycastHit.textureCoord, editContext.brushSize, 0.0f);
                PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds(), 1);

                Material material = TerrainPaintUtilityEditor.GetDefaultBrushPreviewMaterial();

                TerrainPaintUtilityEditor.DrawBrushPreview(
                    paintContext, TerrainPaintUtilityEditor.BrushPreview.SourceRenderTexture, editContext.brushTexture, brushXform, material, 0);

                // 显示预览
                {
                    ApplyBrushInternal(paintContext, editContext.brushStrength, editContext.brushTexture, brushXform, terrain);

                    RenderTexture.active = paintContext.oldRenderTexture;

                    material.SetTexture("_HeightmapOrig", paintContext.sourceRenderTexture);

                    TerrainPaintUtilityEditor.DrawBrushPreview(
                        paintContext, TerrainPaintUtilityEditor.BrushPreview.DestinationRenderTexture, editContext.brushTexture, brushXform, material, 1);
                }

                TerrainPaintUtility.ReleaseContextResources(paintContext);
            }
        }

        private void SaveAllHeightmapToFile()
        {
            foreach (var te in this.waitToSaveTerrains)
            {
                te?.SaveData();
            }

            this.waitToSaveTerrains.Clear();
        }

        public override void OnInspectorGUI(Terrain terrain, IOnInspectorGUI editContext)
        {
            Styles styles = GetStyles();

            EditorGUI.BeginChangeCheck();
            {
                EditorGUI.BeginChangeCheck();
                m_TargetHeight = Mathf.Clamp(m_TargetHeight, terrain.GetPosition().y, terrain.terrainData.size.y + terrain.GetPosition().y);
                m_TargetHeight = EditorGUILayout.Slider(styles.height, m_TargetHeight - terrain.GetPosition().y, 0, terrain.terrainData.size.y) + terrain.GetPosition().y;

                if (EditorGUI.EndChangeCheck())
                    Save(true);
            }


            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(styles.heightValueScale);
            EditorGUI.BeginChangeCheck();
            this.m_HeightScale = EditorGUILayout.Slider(this.m_HeightScale, 0.01f, 10f);
            if (EditorGUI.EndChangeCheck())
                Save(true);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);

            this.DrawLayers();

            if (this.waitToSaveTerrains.Count > 0)
            {
                if (GUILayout.Button(styles.save))
                {
                    this.SaveAllHeightmapToFile();
                }
            }

            // 引擎内置画笔功能
            int textureRez = terrain.terrainData.heightmapResolution;
            editContext.ShowBrushesGUI(5, BrushGUIEditFlags.All, textureRez);
            base.OnInspectorGUI(terrain, editContext);
        }

        #region 图层

        int titleEditorIdx = -1;

        private void DrawLayers()
        {
            if (this.m_heightMapTitles == null || this.m_heightMapTitles.Length != this.m_HeightMapNumber)
            {
                this.InitHeightMapTitles();
            }

            if (this.m_selectedLyaers.Length != this.m_HeightMapNumber)
            {
                this.InitLayerNumberString();
            }

            EditorGUILayout.BeginVertical("sv_iconselector_back");
            {
                for (int i = 0; i < this.m_HeightMapNumber; ++i)
                {
                    EditorGUILayout.BeginHorizontal(i == this.m_CurrentHeightMapIdx ? "LightmapEditorSelectedHighlight" : "SelectionRect");
                    {
                        EditorGUI.BeginChangeCheck();
                        {
                            if (GUILayout.Toggle(this.m_CurrentHeightMapIdx == i, "", "Radio", GUILayout.Width(20)))
                            {
                                this.m_CurrentHeightMapIdx = i;
                            }
                        }
                        if (EditorGUI.EndChangeCheck())
                        {
                            TerrainManager.CurrentHeightMapIdx = this.m_CurrentHeightMapIdx;
                            this.titleEditorIdx = -1;
                        }

                        GUILayout.Space(2);
                        if (this.titleEditorIdx == i)
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUI.BeginChangeCheck();
                                {
                                    this.m_heightMapTitles[i] = GUILayout.TextField(this.m_heightMapTitles[i], "ToolbarTextField");
                                }
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Save(true);
                                }

                                if (GUILayout.Button("", "AC RightArrow", GUILayout.Width(20)))
                                {
                                    this.titleEditorIdx = -1;
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            if (GUILayout.Button(this.m_heightMapTitles[i], "BoldLabel"))
                            {
                                this.titleEditorIdx = i;
                            }
                        }

                        EditorGUI.BeginChangeCheck();
                        {
                            this.m_selectedLyaers[i] = GUILayout.Toggle(this.m_selectedLyaers[i], "", "OL ToggleWhite", GUILayout.Width(20));
                        }
                        if (EditorGUI.EndChangeCheck())
                        {
                            this.titleEditorIdx = -1;
                            TerrainManager.SelectedLayer = this.m_selectedLyaers;
                            Save(true);

                            this.ReloadSelectedLayers();
                        }

                        EditorGUI.BeginChangeCheck();
                        {
                            if (GUILayout.Button("", "OL Minus", GUILayout.Width(20)))
                            {
                                this.titleEditorIdx = -1;
                                this.RemoveHeightLayer(i);
                            }
                        }
                        if (EditorGUI.EndChangeCheck())
                        {
                            Save(true);
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.BeginChangeCheck();
            {
                if (GUILayout.Button("", "OL Plus", GUILayout.Width(20)))
                {
                    this.titleEditorIdx = -1;
                    this.AddHeightLayer();
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Save(true);
            }

            EditorGUILayout.EndVertical();
        }

        private void InitHeightMapTitles()
        {
            int startIdx = 0;
            if (this.m_heightMapTitles == null)
            {
                this.m_heightMapTitles = new string[this.m_HeightMapNumber];
                startIdx = 0;
            }
            else
            {
                var old = this.m_heightMapTitles;
                this.m_heightMapTitles = new string[this.m_HeightMapNumber];
                for (int i = 0; i < this.m_HeightMapNumber && i < old.Length; ++i)
                {
                    this.m_heightMapTitles[i] = old[i];
                    startIdx = i;
                }
            }

            for (int i = startIdx; i < this.m_heightMapTitles.Length; ++i)
            {
                this.m_heightMapTitles[i] = $"第{i + 1}层";
            }
        }

        private void InitLayerNumberString()
        {
            bool[] tmp = this.m_selectedLyaers;
            this.m_selectedLyaers = new bool[this.m_HeightMapNumber];

            for (int i = 0; i < this.m_HeightMapNumber; ++i)
            {
                this.m_selectedLyaers[i] = tmp.Length > i ? tmp[i] : true;
            }

            TerrainManager.SelectedLayer = this.m_selectedLyaers;
        }

        private void ReloadSelectedLayers()
        {
            if (TerrainManager.AllTerrain.Count == 0)
            {
                TerrainManager.InitAllTerrain(this.m_CurrentHeightMapIdx, this.m_CurrentHeightMapIdx);
            }

            for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
            {
                TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>()?.ReLoadLayer(this.m_HeightScale);
            }
        }

        private void AddHeightLayer()
        {
            this.m_HeightMapNumber++;
            this.InitHeightMapTitles();
            this.InitLayerNumberString();

            TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx);
        }

        private void RemoveHeightLayer(int idx)
        {
            if (EditorUtility.DisplayDialog("确认删除", $"真的删除{ this.m_heightMapTitles[idx]}吗？", "删除", "不删"))
            {
                if (this.m_HeightMapNumber <= 0)
                {
                    return;
                }

                for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
                {
                    TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>()?.RemoveLayer(idx);
                }

                this.m_HeightMapNumber--;

                if (this.m_CurrentHeightMapIdx <= this.m_HeightMapNumber)
                {
                    this.m_CurrentHeightMapIdx = this.m_HeightMapNumber - 1;
                }

                if (this.m_heightMapTitles.Length > idx)
                {
                    for (int i = idx; i < this.m_heightMapTitles.Length - 1; ++i)
                    {
                        this.m_heightMapTitles[i] = this.m_heightMapTitles[i + 1];
                    }

                    var old = this.m_heightMapTitles;
                    this.m_heightMapTitles = new string[this.m_HeightMapNumber];
                    for (int i = 0; i < this.m_HeightMapNumber && i < old.Length; ++i)
                    {
                        this.m_heightMapTitles[i] = old[i];
                    }
                }

                if (this.m_selectedLyaers.Length > idx)
                {
                    for (int i = idx; i < this.m_selectedLyaers.Length - 1; ++i)
                    {
                        this.m_selectedLyaers[i] = this.m_selectedLyaers[i + 1];
                    }

                    var old = this.m_selectedLyaers;
                    this.m_selectedLyaers = new bool[this.m_HeightMapNumber];
                    for (int i = 0; i < this.m_HeightMapNumber && i < old.Length; ++i)
                    {
                        this.m_selectedLyaers[i] = old[i];
                    }
                }

                TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx);

                this.ReloadSelectedLayers();
            }
        }

        #endregion
    }
}
