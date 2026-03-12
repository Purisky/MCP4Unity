using System.Diagnostics;
using System.Text.Json;

namespace MCPConsole
{
    public static class UnityManager
    {
        private static UnityConfig? _config;
        private const string ConfigFile = "unity_config.json";

        public class UnityConfig
        {
            public string UnityExePath { get; set; } = "";
            public string ProjectPath { get; set; } = "";
        }

        private static UnityConfig LoadConfig()
        {
            if (_config != null) return _config;

            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                _config = JsonSerializer.Deserialize<UnityConfig>(json) ?? new UnityConfig();
            }
            else
            {
                _config = new UnityConfig();
            }
            return _config;
        }

        public static void SaveConfig(string unityExePath, string projectPath)
        {
            _config = new UnityConfig
            {
                UnityExePath = unityExePath,
                ProjectPath = projectPath
            };
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }

        public static bool IsUnityRunning()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.ProjectPath)) return false;

            var endpointFile = Path.Combine(config.ProjectPath, "Library", "MCP4Unity", "mcp_endpoint.json");
            return File.Exists(endpointFile);
        }

        public static void StartUnity()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.UnityExePath) || string.IsNullOrEmpty(config.ProjectPath))
                throw new Exception("Unity path not configured. Use configureunity first.");

            Process.Start(new ProcessStartInfo
            {
                FileName = config.UnityExePath,
                Arguments = $"-projectPath \"{config.ProjectPath}\"",
                UseShellExecute = true
            });
        }

        public static void StopUnity()
        {
            foreach (var process in Process.GetProcessesByName("Unity"))
            {
                process.Kill();
                process.WaitForExit();
            }
        }

        public static void DeleteSceneBackups()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.ProjectPath)) return;

            var backupPath = Path.Combine(config.ProjectPath, "Temp", "__Backupscenes");
            if (Directory.Exists(backupPath))
                Directory.Delete(backupPath, true);
        }

        public static void DeleteScriptAssemblies()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.ProjectPath)) return;

            var assemblyPath = Path.Combine(config.ProjectPath, "Library", "ScriptAssemblies");
            if (Directory.Exists(assemblyPath))
                Directory.Delete(assemblyPath, true);
        }
    }
}
