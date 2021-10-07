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
        public enum LoadHeightMapType
        {
            HeightSum = 0,
            MaxHeight = 1,
        }

        private enum HeightSpace
        {
            World,
            Local
        }       

        private string paintName = "CustomLayerPaint";

        [SerializeField] float m_TargetHeight;
        [SerializeField] HeightSpace m_HeightSpace;
        [SerializeField] float m_HeightScale = 1;
        [SerializeField] int m_HeightMapNumber = 1;
        [SerializeField] int m_CurrentHeightMapIdx = 0;
        [SerializeField] LoadHeightMapType m_heightMapLoadType = LoadHeightMapType.HeightSum;


        class Styles
        {
            public readonly GUIContent description = EditorGUIUtility.TrTextContent("地型高度编辑器，按左键编辑高度。");

            public readonly GUIContent heightMapNumber = EditorGUIUtility.TrTextContent("图层数量", "除基础图层外，附加图层的数量，最少为1");

            public readonly GUIContent currentHeightMapTypeTitle = EditorGUIUtility.TrTextContent("当前编辑层次");
            public readonly GUIContent reloadAllMapButtonTitle = EditorGUIUtility.TrTextContent("重新加载", "通过重新加载所有层次的高度图重新生成地型");
            public readonly GUIContent clearCurrentLayer = EditorGUIUtility.TrTextContent("清除当前层", "清除当前编辑的层次，其它层次不变");
            public readonly GUIContent clearAllLayerLable = EditorGUIUtility.TrTextContent("禁用所有", "清除所有层次，仅保留原始高度信息");

            public readonly GUIContent height = EditorGUIUtility.TrTextContent("画笔高度", "可以直接设置画笔高度，也可以在地形上按住shift和鼠标滚轮进行调整");
            public readonly GUIContent space = EditorGUIUtility.TrTextContent("高度值类型", "设置高度值为世界空间还是地型的模型空间");

            public readonly GUIContent heightValueScale = EditorGUIUtility.TrTextContent("高度值缩放");

            public readonly GUIContent save = EditorGUIUtility.TrTextContent("保存", "保存所修改");

            public readonly GUIContent loadTpe = EditorGUIUtility.TrTextContent("加载方式", "重新加载高度图的方式");
            public string[] LayoutBlendType = new string[] { "相加", "取最高" };

            public string[] LayerNames = new string[] { "第1层" };

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

            this.InitLayerNumberString(m_styles);
            return m_styles;
        }

        private void InitLayerNumberString(Styles styles)
        {
            styles.LayerNames = new string[this.m_HeightMapNumber];
            for (int i = 0; i < this.m_HeightMapNumber; ++i)
            {
                styles.LayerNames[i] = $"第{i + 1}层";
            }
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
            TerrainManager.InitAllTerrain(this.m_HeightMapNumber);
            this.InitLayerNumberString(this.GetStyles());
        }

        private Material ApplyBrushInternal(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform, Terrain terrain)
        {
            //shader :  Hidden/TerrainEngine/PaintHeight
            Material mat = TerrainPaintUtility.GetBuiltinPaintMaterial();

            float brushTargetHeight = Mathf.Clamp01((m_TargetHeight - paintContext.heightWorldSpaceMin) / paintContext.heightWorldSpaceSize);

            Vector4 brushParams = new Vector4(brushStrength * 0.01f, PaintContext.kNormalizedHeightScale * brushTargetHeight, 0.0f, 0.0f);
            mat.SetTexture("_BrushTex", brushTexture);
            mat.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);

            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.SetHeights);

            return mat;
        }

        public override bool OnPaint(Terrain terrain, IOnPaint editContext)
        {
            if (Event.current.shift)
            {
                m_TargetHeight = terrain.terrainData.GetInterpolatedHeight(editContext.uv.x, editContext.uv.y) + terrain.GetPosition().y;
                editContext.Repaint(RepaintFlags.UI);
                return true;
            }

            BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.uv, editContext.brushSize, 0.0f);
            PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds());

            var mat = ApplyBrushInternal(paintContext, editContext.brushStrength, editContext.brushTexture, brushXform, terrain);

            for (int i = 0; i < paintContext.terrainCount; ++i)
            {
                TerrainExpand terrainExpandData = paintContext.GetTerrain(i).gameObject.GetComponent<TerrainExpand>();
                if (!terrainExpandData)
                {
                    terrainExpandData = paintContext.GetTerrain(i).gameObject.AddComponent<TerrainExpand>();
                }

                terrainExpandData.OnPaint(this.m_CurrentHeightMapIdx, paintContext, i, mat);

                if (!this.waitToSaveTerrains.Contains(terrainExpandData))
                {
                    this.waitToSaveTerrains.Add(terrainExpandData);
                }
            }

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

                this.m_HeightSpace = (HeightSpace)EditorGUILayout.EnumPopup(styles.space, this.m_HeightSpace);

                if (EditorGUI.EndChangeCheck())
                {
                    if (m_HeightSpace == HeightSpace.Local)
                        m_TargetHeight = Mathf.Clamp(m_TargetHeight, terrain.GetPosition().y, terrain.terrainData.size.y + terrain.GetPosition().y);
                }

                // EditorGUI.BeginChangeCheck();

                if (m_HeightSpace == HeightSpace.Local)
                    m_TargetHeight = EditorGUILayout.Slider(styles.height, m_TargetHeight - terrain.GetPosition().y, 0, terrain.terrainData.size.y) + terrain.GetPosition().y;
                else
                    m_TargetHeight = EditorGUILayout.FloatField(styles.height, m_TargetHeight);

                // if (EditorGUI.EndChangeCheck())
                //    Save(true);                

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUI.BeginChangeCheck();

                    GUILayout.Label(styles.heightMapNumber);
                    this.m_HeightMapNumber = EditorGUILayout.IntSlider(this.m_HeightMapNumber, 1, 10);

                    GUILayout.Label(styles.currentHeightMapTypeTitle);
                    if (EditorGUI.EndChangeCheck())
                    {
                        this.InitLayerNumberString(styles);

                        TerrainManager.InitAllTerrain(this.m_HeightMapNumber);
                    }

                    this.m_CurrentHeightMapIdx = EditorGUILayout.Popup(this.m_CurrentHeightMapIdx, styles.LayerNames);
                    TerrainManager.CurrentHeightMapIdx = this.m_CurrentHeightMapIdx;
                }
                EditorGUILayout.EndHorizontal();
            }




            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(styles.clearCurrentLayer))
            {
                if (TerrainManager.AllTerrain.Count == 0)
                {
                    TerrainManager.InitAllTerrain(this.m_HeightMapNumber);
                }

                for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
                {
                    TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>()?.DeleteLayer(this.m_CurrentHeightMapIdx, this.m_HeightScale);
                }
            }

            if (GUILayout.Button(styles.clearAllLayerLable))
            {
                if (TerrainManager.AllTerrain.Count == 0)
                {
                    TerrainManager.InitAllTerrain(this.m_CurrentHeightMapIdx);
                }

                for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
                {
                    TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>()?.DeleteAllAddHeight(this.m_HeightScale);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(styles.reloadAllMapButtonTitle))
            {
                if (TerrainManager.AllTerrain.Count == 0)
                {
                    TerrainManager.InitAllTerrain(this.m_CurrentHeightMapIdx);
                }

                for (int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
                {
                    TerrainManager.AllTerrain[i].GetComponent<TerrainExpand>()?.ReLoadLayer(this.m_HeightScale);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(styles.loadTpe);

            EditorGUI.BeginChangeCheck();
            this.m_heightMapLoadType = (LoadHeightMapType)EditorGUILayout.Popup((int)this.m_heightMapLoadType, styles.LayoutBlendType);
            if (EditorGUI.EndChangeCheck())
                Save(true);

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(styles.heightValueScale);
            EditorGUI.BeginChangeCheck();
            this.m_HeightScale = EditorGUILayout.Slider(this.m_HeightScale, 0.01f, 10f);
            if (EditorGUI.EndChangeCheck())
                Save(true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

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
    }
}
