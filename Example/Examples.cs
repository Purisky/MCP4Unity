using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MCP4Unity.Example
{
    public class ToolExamples
    {
        [Tool("Echo description")]
        public static string Echo_Tool([Tool("stringArg description")] string stringArg, [Tool("intArg description")] int intArg)
        {
            return $"echo:{stringArg},{intArg}";
        }
        [Tool("Retrieves the names of all GameObjects in the hierarchy")]
        public static string[] Get_All_GameObject_in_Hierarchy([Tool("If true, only top-level GameObjects are returned; otherwise, all GameObjects are returned.")] bool top)
        {
            List<string> gameObjectNames = new();

            if (top)
            {
                foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    gameObjectNames.Add(go.name);
                }
            }
            else
            {
                foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
                {
                    gameObjectNames.Add(go.name);
                }
            }
            return gameObjectNames.ToArray();
        }
    }
}
