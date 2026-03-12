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

        public static void SaveConfig(string unityExePath)
        {
            _config = new UnityConfig
            {
                UnityExePath = unityExePath
            };
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }

        private static string GetProjectPath()
        {
            return Directory.GetCurrentDirectory();
        }

        public static bool IsUnityRunning()
        {
            var projectPath = GetProjectPath();
            var endpointFile = Path.Combine(projectPath, "Library", "MCP4Unity", "mcp_endpoint.json");
            return File.Exists(endpointFile);
        }

        public static void StartUnity()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.UnityExePath))
                throw new Exception("Unity path not configured. Use configureunity first.");

            var projectPath = GetProjectPath();
            Process.Start(new ProcessStartInfo
            {
                FileName = config.UnityExePath,
                Arguments = $"-projectPath \"{projectPath}\"",
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
            var projectPath = GetProjectPath();
            var backupPath = Path.Combine(projectPath, "Temp", "__Backupscenes");
            if (Directory.Exists(backupPath))
                Directory.Delete(backupPath, true);
        }

        public static void DeleteScriptAssemblies()
        {
            var projectPath = GetProjectPath();
            var assemblyPath = Path.Combine(projectPath, "Library", "ScriptAssemblies");
            if (Directory.Exists(assemblyPath))
                Directory.Delete(assemblyPath, true);
        }
    }
}
