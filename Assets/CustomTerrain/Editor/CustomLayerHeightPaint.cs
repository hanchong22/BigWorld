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
        [SerializeField] bool[] m_lockedLyaers = new bool[] { false };
        [SerializeField] string[] m_heightMapTitles;
        [SerializeField] bool m_IsBaseLayerEnable = true;

        bool isCreateTerrainMode = false;
        private static TileTerrainManagerTool m_CreateTool = null;

        enum ExpImportStatus
        {
            None = 0,
            Export = 1,
            Import = 2,
        };

        //=0 无 =1导出状态 =2导入状态
        private ExpImportStatus expImportStatus = 0;

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
            TerrainManager.IsBaseLayerEnable = this.m_IsBaseLayerEnable;
            if (CustomLayerHeightPaint.m_CreateTool == null)
            {
                CustomLayerHeightPaint.m_CreateTool = TileTerrainManagerTool.instance;
            }
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
            if (this.m_CurrentHeightMapIdx < 0)
            {
                return false;
            }

            if (this.m_lockedLyaers[this.m_CurrentHeightMapIdx] || this.m_CurrentHeightMapIdx == -1)
            {
                return false;
            }

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
            if (this.isCreateTerrainMode && m_CreateTool != null)
            {
                m_CreateTool.OnSceneGUI(terrain, editContext);
                return;
            }

            Event evt = Event.current;
            if (evt.control && (evt.type == EventType.ScrollWheel))
            {
                evt.Use();
                editContext.Repaint();
            }

            if (evt.type != EventType.Repaint)
                return;

            if (this.m_CurrentHeightMapIdx < 0 || this.m_lockedLyaers[this.m_CurrentHeightMapIdx])
            {
                return;
            }

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
            if (this.isCreateTerrainMode && m_CreateTool != null)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);

                GUILayout.Label(m_CreateTool.GetName());
                if (!string.IsNullOrEmpty(m_CreateTool.GetDesc()))
                    GUILayout.Label(m_CreateTool.GetDesc(), EditorStyles.wordWrappedMiniLabel);

                GUILayout.EndVertical();

                m_CreateTool.OnInspectorGUI(terrain, editContext);
            }

            if (GUILayout.Button(this.isCreateTerrainMode ? "关闭创建" : "创建邻接地型"))
            {
                this.isCreateTerrainMode = !this.isCreateTerrainMode;
            }

            GUILayout.Space(3);

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

            GUILayout.Space(3);

            this.ExportImportLayers();

            GUILayout.Space(3);

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
        int importType = 0; //=0 current Terraon; =1 all Terrains
        int exportLayersType = 0;
        int exportTerrainsType = 0;

        private void DrawLayers()
        {
            if (this.m_heightMapTitles == null || this.m_heightMapTitles.Length != this.m_HeightMapNumber)
            {
                this.InitHeightMapTitles();
            }

            if (this.m_selectedLyaers.Length != this.m_HeightMapNumber || this.m_lockedLyaers.Length != this.m_HeightMapNumber)
            {
                this.InitLayerNumberString();
            }

            EditorGUILayout.BeginVertical("sv_iconselector_back");
            {
                if (this.m_CurrentHeightMapIdx < 0)
                {
                    GUILayout.Space(5);
                }

                EditorGUILayout.BeginHorizontal(this.m_CurrentHeightMapIdx < 0 ? "LightmapEditorSelectedHighlight" : "SelectionRect");
                {
                    GUILayout.Space(5);
                    EditorGUI.BeginChangeCheck();
                    {
                        if (GUILayout.Button("Base Layer", "BoldLabel", GUILayout.Width(150)))
                        {
                            this.m_CurrentHeightMapIdx = -1;
                        }
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        TerrainManager.CurrentHeightMapIdx = this.m_CurrentHeightMapIdx;
                        this.titleEditorIdx = -1;
                    }

                    EditorGUI.BeginChangeCheck();
                    {
                        this.m_IsBaseLayerEnable = GUILayout.Toggle(this.m_IsBaseLayerEnable, "", "OL ToggleWhite", GUILayout.Width(20));
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        TerrainManager.SelectedLayer = this.m_selectedLyaers;
                        TerrainManager.IsBaseLayerEnable = this.m_IsBaseLayerEnable;
                        this.ReloadSelectedLayers();
                        Save(true);
                    }

                    GUILayout.Label(EditorGUIUtility.IconContent("IN LockButton on"));

                    GUILayout.Space(5);
                }
                EditorGUILayout.EndHorizontal();

                if (this.m_CurrentHeightMapIdx < 0)
                {
                    GUILayout.Space(5);
                }

                for (int i = 0; i < this.m_HeightMapNumber; ++i)
                {
                    if (i == this.m_CurrentHeightMapIdx)
                    {
                        GUILayout.Space(5);
                    }

                    EditorGUILayout.BeginHorizontal(i == this.m_CurrentHeightMapIdx ? "LightmapEditorSelectedHighlight" : "SelectionRect");
                    {
                        GUILayout.Space(5);
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

                                if (GUILayout.Button(EditorGUIUtility.IconContent("AvatarInspector/DotSelection"), GUILayout.Width(25)))
                                {
                                    this.titleEditorIdx = -1;
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUI.BeginChangeCheck();
                            {
                                if (GUILayout.Button(this.m_heightMapTitles[i], "BoldLabel"))
                                {
                                    this.m_CurrentHeightMapIdx = i;
                                }
                            }
                            if (EditorGUI.EndChangeCheck())
                            {
                                TerrainManager.CurrentHeightMapIdx = this.m_CurrentHeightMapIdx;
                                this.titleEditorIdx = -1;
                            }

                            if (GUILayout.Button(EditorGUIUtility.IconContent("editicon.sml"), GUILayout.Width(25)))
                            {
                                this.titleEditorIdx = i;
                            }
                        }

                        EditorGUI.BeginChangeCheck();
                        {
                            if (this.m_lockedLyaers[i])
                            {
                                if (GUILayout.Button(EditorGUIUtility.IconContent("IN LockButton on"), GUILayout.Width(25)))
                                {
                                    this.m_lockedLyaers[i] = false;
                                }
                            }
                            else
                            {
                                if (GUILayout.Button(EditorGUIUtility.IconContent("IN LockButton"), GUILayout.Width(25)))
                                {
                                    this.m_lockedLyaers[i] = true;
                                }
                            }
                        }

                        if (EditorGUI.EndChangeCheck())
                        {
                            Save(true);
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
                            if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), GUILayout.Width(25)))
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

                    GUILayout.Space(5);
                    EditorGUILayout.EndHorizontal();

                    if (i == this.m_CurrentHeightMapIdx)
                    {
                        GUILayout.Space(5);
                    }
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

        private void ExportImportLayers()
        {
            if (this.expImportStatus == ExpImportStatus.None)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("导入数据"))
                    {
                        this.expImportStatus = ExpImportStatus.Import;
                    }

                    if (GUILayout.Button("导出数据"))
                    {
                        this.expImportStatus = ExpImportStatus.Export;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                if (this.expImportStatus == ExpImportStatus.Import)
                {
                    EditorGUILayout.BeginVertical();
                    {
                        //EditorGUILayout.BeginHorizontal();
                        //{
                        //    GUILayout.Label("导入地型：");
                        //    this.importType = EditorGUILayout.Popup(this.importType, new string[] { "当前地块", "所有地块" });
                        //}
                        //EditorGUILayout.EndHorizontal();
                        if (this.m_CurrentHeightMapIdx >= 0)
                        {
                            if (this.m_lockedLyaers[this.m_CurrentHeightMapIdx])
                            {
                                EditorGUILayout.HelpBox("导入数据将覆盖当前选中图层，当前图层正处于锁定状态", MessageType.Warning);
                            }
                            else
                            {
                                EditorGUILayout.HelpBox("导入数据将覆盖当前选中图层", MessageType.Info);
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("导入数据将覆盖基础图层", MessageType.Warning);
                        }

                        if (GUILayout.Button("导入文件..."))
                        {
                            string importFilePath = EditorUtility.OpenFilePanelWithFilters("选择地型高度图", System.IO.Directory.GetCurrentDirectory(), new string[] { "Image files", "png,jpg,jpeg,bmp", "Data files", "data,asset,byte", "All files", "*" });
                            if (!string.IsNullOrEmpty(importFilePath))
                            {
                                string AssetPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets").Replace("\\", "/");
                                Texture2D loadedTex = null;
                                if (importFilePath.StartsWith(AssetPath))
                                {
                                    loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(importFilePath.Replace(AssetPath, ""));
                                }
                                else
                                {
                                    byte[] data = System.IO.File.ReadAllBytes(importFilePath);
                                    loadedTex.LoadImage(data);
                                }

                                if (loadedTex && TerrainManager.CurrentSelectedTerrain)
                                {
                                    TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>()?.ReimportHeightmap(this.m_CurrentHeightMapIdx, loadedTex);
                                }
                            }
                        }
                    }
                    EditorGUILayout.EndVertical();

                    if (GUILayout.Button("关闭导入"))
                    {
                        this.expImportStatus = ExpImportStatus.None;
                    }
                }
                else if (this.expImportStatus == ExpImportStatus.Export)
                {
                    EditorGUILayout.BeginVertical();
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("导出图层：");
                            this.exportLayersType = EditorGUILayout.Popup(this.exportLayersType, new string[] { "当前图层", "可见图层合并导出", "所有图层合并导出" });
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("导出地型：");
                            this.exportTerrainsType = EditorGUILayout.Popup(this.exportTerrainsType, new string[] { "当前地块", "所有地块" });
                        }
                        EditorGUILayout.EndHorizontal();

                        if (GUILayout.Button("导出文件..."))
                        {
                            string exportPath = EditorUtility.OpenFolderPanel("选择目标文件夹", System.IO.Directory.GetCurrentDirectory(), "打开文件夹");

                            if (!string.IsNullOrEmpty(exportPath))
                            {
                                List<int> expLayers = new List<int>();

                                switch (this.exportLayersType)
                                {
                                    case 0:
                                        expLayers.Add(this.m_CurrentHeightMapIdx);
                                        break;
                                    case 1:
                                        if (this.m_IsBaseLayerEnable)
                                        {
                                            expLayers.Add(-1);
                                        }
                                        for (int n = 0; n < this.m_HeightMapNumber; ++n)
                                        {
                                            if (this.m_selectedLyaers[n])
                                            {
                                                expLayers.Add(n);
                                            }
                                        }
                                        break;
                                    case 2:
                                        expLayers.Add(-1);
                                        for (int n = 0; n < this.m_HeightMapNumber; ++n)
                                        {
                                            expLayers.Add(n);
                                        }
                                        break;
                                }

                                if (this.exportTerrainsType == 0)
                                {
                                    if (TerrainManager.CurrentSelectedTerrain)
                                    {
                                        string exportFile = System.IO.Path.Combine(exportPath, $"{TerrainManager.CurrentSelectedTerrain.name}_heightMap.exr");
                                        var expTexture = TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>()?.GetMergedTexture(expLayers);
                                        AssetDatabase.CreateAsset(expTexture,$"Assets/{TerrainManager.CurrentSelectedTerrain.name}_heightMap.asset");
                                        this.WriteImageDataToFile(expTexture, exportFile);
                                    }
                                    else
                                    {
                                        Debug.LogError("当前没有选择地块");
                                    }
                                }
                                else
                                {
                                    for (int n = 0; n < TerrainManager.AllTerrain.Count; ++n)
                                    {
                                        var t = TerrainManager.AllTerrain[n];
                                        string exportFile = System.IO.Path.Combine(exportPath, $"{t.name}_heightMap.exr");
                                        var expTexture = t.GetComponent<TerrainExpand>()?.GetMergedTexture(expLayers);
                                        AssetDatabase.CreateAsset(expTexture, $"Assets/{t.name}_heightMap.asset");
                                        this.WriteImageDataToFile(expTexture, exportFile);
                                    }
                                }

                                AssetDatabase.SaveAssets();
                            }
                        }
                    }
                    EditorGUILayout.EndVertical();

                    if (GUILayout.Button("关闭导出"))
                    {
                        this.expImportStatus = ExpImportStatus.None;
                    }
                }

            }
        }

        private void WriteImageDataToFile(Texture2D tex, string path)
        {
            byte[] data = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
            var fl = System.IO.File.OpenWrite(path);
            fl.Write(data, 0, data.Length);
            fl.Close();
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

            tmp = this.m_lockedLyaers;
            this.m_lockedLyaers = new bool[this.m_HeightMapNumber];

            for (int i = 0; i < this.m_HeightMapNumber; ++i)
            {
                this.m_lockedLyaers[i] = tmp.Length > i ? tmp[i] : true;
            }
        }

        private void ReloadSelectedLayers()
        {
            if (TerrainManager.AllTerrain.Count == 0)
            {
                TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx);
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
            if (this.m_lockedLyaers[idx])
            {
                EditorUtility.DisplayDialog("错误", "无法删除已锁定的图层", "确定");
                return;
            }

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
