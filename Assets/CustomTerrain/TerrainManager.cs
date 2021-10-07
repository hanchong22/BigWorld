using UnityEngine;
using UnityEngine.Experimental.TerrainAPI;
using UnityEditor.ShortcutManagement;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using System.Collections.Generic;

namespace SeasunTerrain
{
    public static class TerrainManager
    {
        public static List<Terrain> AllTerrain { get; private set; } = new List<Terrain>();
      
        public static int HeightMapNumber { get; private set; }
        public static int CurrentHeightMapIdx { get; set; }

        public static void InitAllTerrain(int heightMapNumber)
        {
            TerrainManager.HeightMapNumber = heightMapNumber;

            Terrain[] allTerrains = GameObject.FindObjectsOfType<Terrain>();
            TerrainManager.AllTerrain.Clear();
            TerrainManager.AllTerrain.AddRange(allTerrains);

            for(int i = 0; i < TerrainManager.AllTerrain.Count; ++i)
            {
                if(!TerrainManager.AllTerrain[i].gameObject.GetComponent<TerrainExpand>())
                {
                    TerrainManager.AllTerrain[i].gameObject.AddComponent<TerrainExpand>();
                }
            }
        }

        public static void AddTerrain(Terrain t)
        {
            if(!TerrainManager.AllTerrain.Contains(t))
            {
                TerrainManager.AllTerrain.Add(t);
            }
        }


    }
}

