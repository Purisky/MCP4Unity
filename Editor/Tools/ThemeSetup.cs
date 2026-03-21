using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCP
{
    public static class ThemeSetup
    {
        public static string Run()
        {
            string path = "Assets/Share/Resources/UI/DefaultTestTheme.tss";
            var existing = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
            if (existing != null)
                return "Already exists: " + path;

            var theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            AssetDatabase.CreateAsset(theme, path);
            AssetDatabase.SaveAssets();
            return "Created: " + path;
        }
    }
}
