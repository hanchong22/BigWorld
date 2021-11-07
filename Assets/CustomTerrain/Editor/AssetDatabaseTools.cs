using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Bindings;
using UnityEngineInternal;
using uei = UnityEngine.Internal;
using Object = UnityEngine.Object;
using UnityEngine.Scripting;
using UnityEditor.Experimental;
using UnityEngine.Internal;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;

namespace Seasun.Editor
{

    public class AssetDatabaseTools
    {
        public static void CreateAssetFromObjects(Object[] assets, string path)
        {
            AssetDatabase.CreateAsset(assets[0], path);
            Object mainObj = AssetDatabase.LoadAssetAtPath<Object>(path);
            for(int i = 1; i < assets.Length; i ++)
            {
                AssetDatabase.AddObjectToAsset(assets[i], mainObj);
            }

            AssetDatabase.SaveAssets();
        }
    }

}
