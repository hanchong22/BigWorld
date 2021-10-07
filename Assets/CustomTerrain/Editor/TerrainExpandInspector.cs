using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace SeasunTerrain
{
    [CustomEditor(typeof(TerrainExpand))]
    [CanEditMultipleObjects]
    class TerrainExpandInspector : Editor
    {
        class Styles
        {
            public readonly GUIContent currentHeitMapTitle = EditorGUIUtility.TrTextContent("当前图层", "当前正在编辑的高度图");          
        }

        private static Styles m_styles;
        private Styles GetStyles()
        {
            if (m_styles == null)
            {
                m_styles = new Styles();
            }
            return m_styles;
        }
       

        private TerrainExpand script;

        public void OnEnable()
        {          
            this.script = this.target as TerrainExpand;
        }

        public override void OnInspectorGUI()
        {
            Styles styles = GetStyles();
            base.serializedObject.Update();

            GUILayout.Space(150);
            if (this.script && this.script.rtHeightMapList.Count >= TerrainManager.HeightMapNumber)
            {
                GUI.DrawTexture(new Rect(5, 5, 100, 100), this.script.rtHeightMapList[TerrainManager.CurrentHeightMapIdx]);
            }
            

            base.serializedObject.ApplyModifiedProperties();
        }

    }
}

