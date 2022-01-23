using UnityEngine;
using UnityEngine.Experimental.TerrainAPI;
using UnityEditor.ShortcutManagement;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using System.Collections.Generic;
using System.IO;

namespace SeasunTerrain
{
    [FilePath("Library/TerrainTools/CustomLayerHeight", FilePathAttribute.Location.ProjectFolder)]
    partial class CustomLayerHeightPaint : TerrainPaintTool<CustomLayerHeightPaint>
    {
        private string paintName = "CustomLayerPaint";

        [SerializeField] float m_TargetHeight;
        [SerializeField] float m_HeightScale = 1;
        [SerializeField] int m_HeightMapNumber = 1;
        [SerializeField] int m_CurrentHeightMapIdx = 0;
        [SerializeField] bool[] m_selectedLyaers = new bool[] { true };
        [SerializeField] bool[] m_lockedLyaers = new bool[] { false };
        [SerializeField] bool[] m_overlayLayers = new bool[] { false };
        [SerializeField] string[] m_heightMapTitles;
        [SerializeField] bool m_IsBaseLayerEnable = true;
        [SerializeField] float m_direction = 0.0f;

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
        private bool isRotationLayer = false;

        class Styles
        {
            public readonly GUIContent description = EditorGUIUtility.TrTextContent("地型高度编辑器，按左键编辑高度，按Shift + 左键擦除高度。");
            public readonly GUIContent height = EditorGUIUtility.TrTextContent("画笔高度", "可以直接设置画笔高度，也可以在地形上按住shift和鼠标滚轮进行调整");
            public readonly GUIContent heightValueScale = EditorGUIUtility.TrTextContent("高度值缩放");
            public readonly GUIContent direction = EditorGUIUtility.TrTextContent("模糊方向", "向上模糊(1.0), 向下模糊 (-1.0) 或双向 (0.0)");
            public readonly GUIContent SetOverlay = EditorGUIUtility.TrTextContent("设置为覆盖层");
            public readonly GUIContent CancleOverlay = EditorGUIUtility.TrTextContent("设置为普通层");
            public readonly GUIContent save = EditorGUIUtility.TrTextContent("保存", "保存所修改");
            public readonly GUIStyle redTitle = new GUIStyle()
            {
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState()
                {
                    textColor = Color.red,
                },
                alignment = TextAnchor.MiddleLeft,
            };
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
            switch (CustomLayerHeightPaint.CurrentPaintType)
            {
                case PaintTypeEnum.PaintHoles:
                    return "左键画洞.\n\n按住Shift + 左键擦除";
                case PaintTypeEnum.SetHeight:
                    return this.GetStyles().description.text;
                case PaintTypeEnum.SmoothHeight:
                    return "平滑地面高度";
            }

            return "请选择一种笔刷";

        }

        public override string GetName()
        {
            return this.paintName;
        }

        public override void OnEnable()
        {
            TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx, this.m_TargetHeight);
            TerrainManager.IsBaseLayerEnable = this.m_IsBaseLayerEnable;
            TerrainManager.SelectedLayer = this.m_selectedLyaers;
            TerrainManager.OverlayLayers = this.m_overlayLayers;
            if (CustomLayerHeightPaint.m_CreateTool == null)
            {
                CustomLayerHeightPaint.m_CreateTool = TileTerrainManagerTool.instance;
            }
        }

        private Material ApplyBrushInternal(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform)
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

        private Material ApplyBrushFromBaseHeightInternal(PaintContextExp paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform, Terrain terrain)
        {
            Material mat = TerrainManager.GetPaintHeightExtMat();

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

            if (CurrentPaintType == PaintTypeEnum.SetHeight)
            {
                BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.uv, editContext.brushSize, 0.0f);
                PaintContextExp paintContextTmp = TerrainManager.BeginPaintHeightMapLyaer(terrain, brushXform.GetBrushXYBounds(), this.m_CurrentHeightMapIdx);
                ApplyBrushFromBaseHeightInternal(paintContextTmp, editContext.brushStrength, editContext.brushTexture, brushXform, terrain);

                for (int i = 0; i < paintContextTmp.terrainCount; ++i)
                {
                    TerrainExpand terrainExpandData = paintContextTmp.GetTerrain(i).gameObject.GetComponent<TerrainExpand>();
                    if (!terrainExpandData)
                    {
                        terrainExpandData = paintContextTmp.GetTerrain(i).gameObject.AddComponent<TerrainExpand>();
                    }

                    terrainExpandData.OnPaint(this.m_CurrentHeightMapIdx, paintContextTmp, i, (Event.current.shift ? -1 : 1));

                    if (!this.waitToSaveTerrains.Contains(terrainExpandData))
                    {
                        this.waitToSaveTerrains.Add(terrainExpandData);
                    }
                }

                paintContextTmp.Cleanup();

                PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds());
                ApplyBrushInternal(paintContext, editContext.brushStrength, editContext.brushTexture, brushXform);
                TerrainPaintUtility.EndPaintHeightmap(paintContext, "Terrain Paint - CustomLayerHeight");
            }
            else if (CurrentPaintType == PaintTypeEnum.PaintHoles)
            {
                Vector2 halfTexelOffset = new Vector2(0.5f / terrain.terrainData.holesResolution, 0.5f / terrain.terrainData.holesResolution);
                BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.uv - halfTexelOffset, editContext.brushSize, 0.0f);

                PaintContextExp paintContextTmp = TerrainManager.BeginPaintHolesMapLayer(terrain, brushXform.GetBrushXYBounds(), this.m_CurrentHeightMapIdx);
                Material mat = ApplyBrushHoleFromBaseInternal(paintContextTmp, editContext.brushStrength, editContext.brushTexture, brushXform);

                for (int i = 0; i < paintContextTmp.terrainCount; ++i)
                {
                    TerrainExpand terrainExpandData = paintContextTmp.GetTerrain(i).gameObject.GetComponent<TerrainExpand>();
                    if (!terrainExpandData)
                    {
                        terrainExpandData = paintContextTmp.GetTerrain(i).gameObject.AddComponent<TerrainExpand>();
                    }

                    terrainExpandData.OnPainHole(this.m_CurrentHeightMapIdx, paintContextTmp, i);

                    if (!this.waitToSaveTerrains.Contains(terrainExpandData))
                    {
                        this.waitToSaveTerrains.Add(terrainExpandData);
                    }
                }

                paintContextTmp.Cleanup();

                PaintContext paintContext = TerrainPaintUtility.BeginPaintHoles(terrain, brushXform.GetBrushXYBounds());
                ApplyBrushHole(paintContext, editContext.brushStrength, editContext.brushTexture, brushXform);
                TerrainPaintUtility.EndPaintHoles(paintContext, "Terrain Paint - Paint Holes");
            }
            else if (CurrentPaintType == PaintTypeEnum.SmoothHeight)
            {
                BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.uv, editContext.brushSize, 0.0f);
                PaintContextExp paintContextTmp = TerrainManager.BeginPaintHeightMapLyaer(terrain, brushXform.GetBrushXYBounds(), this.m_CurrentHeightMapIdx);
                ApplyBrushSmoothHeightFromBaseInternal(paintContextTmp, editContext.brushStrength, editContext.brushTexture, brushXform);

                for (int i = 0; i < paintContextTmp.terrainCount; ++i)
                {
                    TerrainExpand terrainExpandData = paintContextTmp.GetTerrain(i).gameObject.GetComponent<TerrainExpand>();
                    if (!terrainExpandData)
                    {
                        terrainExpandData = paintContextTmp.GetTerrain(i).gameObject.AddComponent<TerrainExpand>();
                    }

                    terrainExpandData.OnPaint(this.m_CurrentHeightMapIdx, paintContextTmp, i, 0);

                    if (!this.waitToSaveTerrains.Contains(terrainExpandData))
                    {
                        this.waitToSaveTerrains.Add(terrainExpandData);
                    }
                }

                List<int> leftLayers = new List<int>();
                for (int i = 0; i < TerrainManager.SelectedLayer.Length; ++i)
                {
                    if (TerrainManager.SelectedLayer[i] && i != this.m_CurrentHeightMapIdx)
                    {
                        leftLayers.Add(i);
                    }
                }

                TerrainManager.AddLeftHeightMapsToPainContex(paintContextTmp, leftLayers.ToArray());

                TerrainPaintUtility.EndPaintHeightmap(paintContextTmp, "Terrain Paint - Smooth Height");
            }


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

                if (CurrentPaintType == PaintTypeEnum.SetHeight)

                // 显示预览
                {
                    //if(CurrentPaintType == PaintTypeEnum.PaintHeight)
                    //{
                    //    this.ApplyBrushHeightInternal(paintContext, editContext.brushStrength, editContext.brushTexture, brushXform);
                    //}
                    //else 
                    if (CurrentPaintType == PaintTypeEnum.SetHeight)
                    {
                        this.ApplyBrushInternal(paintContext, editContext.brushStrength, editContext.brushTexture, brushXform);
                    }

                    RenderTexture.active = paintContext.oldRenderTexture;

                    material.SetTexture("_HeightmapOrig", paintContext.sourceRenderTexture);

                    TerrainPaintUtilityEditor.DrawBrushPreview(
                        paintContext, TerrainPaintUtilityEditor.BrushPreview.DestinationRenderTexture, editContext.brushTexture, brushXform, material, 1);
                }
                else if (CurrentPaintType == PaintTypeEnum.PaintHoles)
                {

                }
                else if (CurrentPaintType == PaintTypeEnum.SmoothHeight)
                {
                    TerrainPaintUtilityEditor.DrawBrushPreview(paintContext, TerrainPaintUtilityEditor.BrushPreview.SourceRenderTexture, editContext.brushTexture, brushXform, TerrainPaintUtilityEditor.GetDefaultBrushPreviewMaterial(), 0);
                    TerrainPaintUtility.ReleaseContextResources(paintContext);
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
            int textureRez = terrain.terrainData.heightmapResolution;

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

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("笔刷类型");
            CustomLayerHeightPaint.CurrentPaintType = (PaintTypeEnum)EditorGUILayout.Popup((int)CustomLayerHeightPaint.CurrentPaintType, this.paintTypeNames);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);
            EditorGUILayout.BeginVertical("sv_iconselector_back");
            if (CustomLayerHeightPaint.CurrentPaintType == PaintTypeEnum.SetHeight)
            {
                EditorGUI.BeginChangeCheck();
                {
                    EditorGUI.BeginChangeCheck();
                    m_TargetHeight = Mathf.Clamp(m_TargetHeight, terrain.GetPosition().y, terrain.terrainData.size.y + terrain.GetPosition().y);
                    m_TargetHeight = EditorGUILayout.Slider(styles.height, m_TargetHeight - terrain.GetPosition().y, 0, terrain.terrainData.size.y) + terrain.GetPosition().y;

                    if (EditorGUI.EndChangeCheck())
                    {
                        TerrainManager.BrashTargetHeight = m_TargetHeight;
                        Save(true);
                    }
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(styles.heightValueScale);
                EditorGUI.BeginChangeCheck();
                this.m_HeightScale = EditorGUILayout.Slider(this.m_HeightScale, 0.01f, 10f);
                if (EditorGUI.EndChangeCheck())
                    Save(true);
                EditorGUILayout.EndHorizontal();
            }
            else if (CustomLayerHeightPaint.CurrentPaintType == PaintTypeEnum.SmoothHeight)
            {
                EditorGUI.BeginChangeCheck();
                m_direction = EditorGUILayout.Slider(styles.direction, m_direction, -1.0f, 1.0f);
                if (EditorGUI.EndChangeCheck())
                    Save(true);
            }

            else if (CustomLayerHeightPaint.CurrentPaintType == PaintTypeEnum.PaintHoles)
            {

            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(3);


            GUILayout.Space(2);

            this.DrawLayers();

            GUILayout.Space(3);

            this.ExportImportLayers();

            GUILayout.Space(3);

            this.RotationLayerUI();

            GUILayout.Space(3);

            this.SetOverlayLayer();

            GUILayout.Space(3);

            if (this.waitToSaveTerrains.Count > 0)
            {
                if (GUILayout.Button(styles.save))
                {
                    this.SaveAllHeightmapToFile();
                }
            }

            // 引擎内置画笔功能

            editContext.ShowBrushesGUI(5, BrushGUIEditFlags.All, textureRez);
            base.OnInspectorGUI(terrain, editContext);
        }


        #region 图层

        int titleEditorIdx = -1;
        int importType = 0; //=0 current Terraon; =1 all Terrains
        int importLimitHeightType = 0;      //=0 limitHeight, =1 scale
        int exportLayersType = 0;
        int exportTerrainsType = 0;
        int exportFileType = 0; //0: image file, 1 data file

        private void DrawLayers()
        {
            if (this.m_heightMapTitles == null || this.m_heightMapTitles.Length != this.m_HeightMapNumber)
            {
                this.InitHeightMapTitles();
            }

            if (this.m_selectedLyaers.Length != this.m_HeightMapNumber || this.m_lockedLyaers.Length != this.m_HeightMapNumber || this.m_overlayLayers.Length != this.m_HeightMapNumber)
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
                                if (GUILayout.Button(this.m_heightMapTitles[i], this.m_overlayLayers[i] ? GetStyles().redTitle : "BoldLabel"))
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
            }
            EditorGUILayout.EndVertical();
        }

        float rotationAngle = 0f;
        Vector4 rotationPivot = new Vector4(0.5f, 0.5f, 0, 0);
        float layerScale = 1f;
        int rotationTileType = 0;
        float layerHeightScale = 1f;

        private void RotationLayerUI()
        {
            if (!this.isRotationLayer)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("旋转图层"))
                    {
                        this.isRotationLayer = true;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("完成旋转"))
                    {
                        this.isRotationLayer = false;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginVertical();
                {
                    if (this.m_CurrentHeightMapIdx >= 0)
                    {
                        if (this.m_lockedLyaers[this.m_CurrentHeightMapIdx])
                        {
                            EditorGUILayout.HelpBox("当前图层正处于锁定状态", MessageType.Error);
                        }
                        else
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("旋转地块：");
                            this.rotationTileType = EditorGUILayout.Popup(this.rotationTileType, new string[] { "当前地块", "所有地块" });
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("旋转角度：");
                            float oldAngle = this.rotationAngle;
                            this.rotationAngle = EditorGUILayout.Slider(this.rotationAngle, -5f, 5f);
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("旋转中心(x,y)：");
                            float rotationPivotX = this.rotationPivot.x;
                            float rotationPivotY = this.rotationPivot.y;
                            rotationPivotX = EditorGUILayout.Slider(rotationPivotX, 0, 1);
                            rotationPivotY = EditorGUILayout.Slider(rotationPivotY, 0, 1);
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("位移(x,y)：");
                            float oldOffsetX = this.rotationPivot.z;
                            float oldOffsetY = this.rotationPivot.w;
                            float offsetX = EditorGUILayout.Slider(this.rotationPivot.z, -1, 1);
                            float offsetY = EditorGUILayout.Slider(this.rotationPivot.w, -1, 1);
                            EditorGUILayout.EndHorizontal();

                            this.rotationPivot = new Vector4(rotationPivotX, rotationPivotY, offsetX, offsetY);

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("缩放(面积，高度)：");
                            float oldScale = this.layerScale;
                            this.layerScale = EditorGUILayout.Slider(this.layerScale, 0, 3);
                            float oldLayerScale = this.layerHeightScale;
                            this.layerHeightScale = EditorGUILayout.Slider(this.layerHeightScale, 0, 10);
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.BeginHorizontal();
                            if (this.rotationAngle != oldAngle || oldScale != this.layerScale || (oldOffsetX != this.rotationPivot.z || oldOffsetY != this.rotationPivot.w) || this.layerHeightScale != oldLayerScale)
                            {
                                if (TerrainManager.AllTerrain.Count == 0)
                                {
                                    TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx, this.m_TargetHeight);
                                }

                                if (this.rotationTileType == 1)
                                {
                                    for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
                                    {
                                        TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>()?.RotaitonLayer(this.m_CurrentHeightMapIdx, this.rotationAngle, this.rotationPivot, this.layerScale, this.layerHeightScale, 1);

                                        if (!this.waitToSaveTerrains.Contains(TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>()))
                                        {
                                            this.waitToSaveTerrains.Add(TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>());
                                        }
                                    }
                                }
                                else
                                {
                                    if (TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>())
                                    {
                                        TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>()?.RotaitonLayer(this.m_CurrentHeightMapIdx, this.rotationAngle, this.rotationPivot, this.layerScale, this.layerHeightScale, 1);
                                    }

                                    if (!this.waitToSaveTerrains.Contains(TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>()))
                                    {
                                        this.waitToSaveTerrains.Add(TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>());
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("基础图层不能旋转", MessageType.Warning);
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void SetOverlayLayer()
        {
            if (this.m_CurrentHeightMapIdx < 0)
            {
                return;
            }

            EditorGUILayout.BeginVertical();
            if (this.m_overlayLayers[this.m_CurrentHeightMapIdx])
            {
                if (GUILayout.Button(GetStyles().CancleOverlay))
                {
                    this.m_overlayLayers[this.m_CurrentHeightMapIdx] = false;
                    TerrainManager.OverlayLayers = this.m_overlayLayers;
                }
            }
            else
            {
                if (GUILayout.Button(GetStyles().SetOverlay))
                {
                    this.m_overlayLayers[this.m_CurrentHeightMapIdx] = true;
                    TerrainManager.OverlayLayers = this.m_overlayLayers;
                }
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

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label(this.GetStyles().heightValueScale);
                        EditorGUI.BeginChangeCheck();
                        this.m_HeightScale = EditorGUILayout.Slider(this.m_HeightScale, 0.01f, 10f);
                        if (EditorGUI.EndChangeCheck())
                            Save(true);
                        EditorGUILayout.EndHorizontal();


                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("超出笔刷高度时：");
                        this.importLimitHeightType = EditorGUILayout.Popup(this.importLimitHeightType, new string[] { "截取有效值", "整体按比例缩小" });
                        EditorGUILayout.EndHorizontal();


                        if (GUILayout.Button("导入文件..."))
                        {
                            string waitDeleteFile = "";
                            string importFilePath = EditorUtility.OpenFilePanelWithFilters("选择地型高度图", System.IO.Directory.GetCurrentDirectory(), new string[] { "高度纹理", "png,jpg,jpeg,bmp,exr", "地型数据", "data,asset,byte,raw", "All files", "*" });
                            if (!string.IsNullOrEmpty(importFilePath))
                            {
                                Texture2D loadedTex = null;

                                bool isDataFile = this.IsDataFile(importFilePath);
                                if (!isDataFile)
                                {
                                    string AssetPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets/").Replace("\\", "/");
                                    if (importFilePath.StartsWith(AssetPath))
                                    {
                                        loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine("Assets", importFilePath.Replace(AssetPath, "")));
                                    }
                                    else
                                    {
                                        string tmpFileName = GUID.Generate().ToString() + System.IO.Path.GetExtension(importFilePath);
                                        System.IO.File.Copy(importFilePath, System.IO.Path.Combine(AssetPath, tmpFileName));
                                        AssetDatabase.Refresh();
                                        loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/{tmpFileName}");
                                        waitDeleteFile = AssetDatabase.GetAssetPath(loadedTex);
                                    }

                                    if (loadedTex && TerrainManager.CurrentSelectedTerrain)
                                    {
                                        TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>()?.ReimportHeightmap(this.m_CurrentHeightMapIdx, loadedTex, this.m_HeightScale, this.importLimitHeightType);
                                    }

                                    if (!string.IsNullOrEmpty(waitDeleteFile))
                                    {
                                        AssetDatabase.DeleteAsset(waitDeleteFile);
                                        AssetDatabase.Refresh();
                                    }
                                }
                                else
                                {
                                    FileStream file = File.Open(importFilePath, FileMode.Open, FileAccess.Read);
                                    int fileSize = (int)file.Length;
                                    file.Close();

                                    int pixels = fileSize / 2;
                                    int resolution = Mathf.RoundToInt(Mathf.Sqrt(pixels));

                                    byte[] data;
                                    using (BinaryReader br = new BinaryReader(File.Open(importFilePath, FileMode.Open, FileAccess.Read)))
                                    {
                                        data = br.ReadBytes(resolution * resolution * 2);
                                        br.Close();
                                    }

                                    if (TerrainManager.CurrentSelectedTerrain)
                                    {
                                        TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>()?.ReimportHeightData(this.m_CurrentHeightMapIdx, data, this.m_HeightScale, resolution, this.importLimitHeightType);
                                    }
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

                        EditorGUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("数据类型：");
                            this.exportFileType = EditorGUILayout.Popup(this.exportFileType, new string[] { "纹理数据", "地型数据" });
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
                                        string exportFile = System.IO.Path.Combine(exportPath, $"{TerrainManager.CurrentSelectedTerrain.name}_heightMap.{(this.exportFileType == 0 ? "exr" : "data")}");
                                        var expTexture = TerrainManager.CurrentSelectedTerrain.GetComponent<TerrainExpand>()?.GetMergedTexture(expLayers);
                                        AssetDatabase.CreateAsset(expTexture, $"Assets/{TerrainManager.CurrentSelectedTerrain.name}_heightMap.asset");
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
                                        string exportFile = System.IO.Path.Combine(exportPath, $"{t.name}_heightMap.{(this.exportFileType == 0 ? "exr" : "data")}");
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

        private bool IsDataFile(string fileName)
        {
            string expName = System.IO.Path.GetExtension(fileName).ToLower();
            return expName.EndsWith("data") || expName.EndsWith("byte");
        }

        private void WriteImageDataToFile(Texture2D tex, string path)
        {
            if (this.exportFileType == 0)
            {
                Texture2D newTexture = new Texture2D(tex.width, tex.height, TextureFormat.RHalf, false);

                for (int y = 0; y < tex.height; ++y)
                {
                    for (int x = 0; x < tex.width; ++x)
                    {
                        Vector4 scolor = tex.GetPixel(x, y);

                        newTexture.SetPixel(x, y, scolor);
                    }
                }

                newTexture.Apply();

                byte[] data = newTexture.EncodeToEXR(Texture2D.EXRFlags.None);
                var fl = System.IO.File.OpenWrite(path);
                fl.Write(data, 0, data.Length);
                fl.Close();
            }
            else
            {
                byte[] data = new byte[tex.width * tex.height * 2];

                float normalize = (1 << 16);
                for (int y = 0; y < tex.height; ++y)
                {
                    for (int x = 0; x < tex.width; ++x)
                    {
                        int index = x + y * tex.width;
                        int height = Mathf.RoundToInt(tex.GetPixel(y, x).r * normalize);
                        ushort compressedHeight = (ushort)Mathf.Clamp(height, 0, ushort.MaxValue);

                        byte[] byteData = System.BitConverter.GetBytes(compressedHeight);
                        data[index * 2 + 0] = byteData[0];
                        data[index * 2 + 1] = byteData[1];
                    }
                }

                FileStream fs = new FileStream(path, FileMode.Create);
                fs.Write(data, 0, data.Length);
                fs.Close();
            }
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
                this.m_lockedLyaers[i] = tmp.Length > i ? tmp[i] : false;
            }

            tmp = this.m_overlayLayers;
            this.m_overlayLayers = new bool[this.m_HeightMapNumber];
            for (int i = 0; i < this.m_HeightMapNumber; ++i)
            {
                this.m_overlayLayers[i] = tmp.Length > i ? tmp[i] : false;
            }

            TerrainManager.OverlayLayers = this.m_overlayLayers;
        }

        private void ReloadSelectedLayers()
        {
            if (TerrainManager.AllTerrain.Count == 0)
            {
                TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx, this.m_TargetHeight);
            }

            for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
            {
                TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>()?.ReLoadLayer(1);
            }
        }

        private void AddHeightLayer()
        {
            this.m_HeightMapNumber++;
            this.InitHeightMapTitles();
            this.InitLayerNumberString();

            TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx, this.m_TargetHeight);
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

                TerrainManager.InitAllTerrain(this.m_HeightMapNumber, this.m_CurrentHeightMapIdx, this.m_TargetHeight);

                this.ReloadSelectedLayers();
            }
        }

        #endregion
    }
}
