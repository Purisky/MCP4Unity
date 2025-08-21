using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MCP4Unity.Editor
{
    [InitializeOnLoad]
    public class MCPService
    {
        public static MCPService Inst = new();
        public bool Running { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        HttpListener HttpListener;

        public static Action OnStateChange;
        private const string MCPConsoleFolderName = "Assets/MCP4Unity/MCPConsole~";
        private const string HistoryPrefKey = "MCP4Unity_ExecutionHistory";
        
        // 跨平台可执行文件名和构建脚本名
        private static string MCPConsoleExeName => Application.platform == RuntimePlatform.WindowsEditor ? "MCPConsole.exe" : "MCPConsole";
        private static string BuildScriptName => Application.platform == RuntimePlatform.WindowsEditor ? "build.bat" : "build.sh";
        
        // MCP执行历史记录
        public static List<ToolExecutionHistory> MCPExecutionHistory { get; private set; } = new List<ToolExecutionHistory>();
        public static Action OnHistoryUpdated;

        static JsonSerializerSettings SerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
        };
        static MCPService()
        {
            // 加载持久化的历史记录
            LoadExecutionHistory();
            
            // 检查MCPConsole.exe是否存在，如果不存在则运行build.bat
            CheckAndBuildMCPConsole();
            
            if (EditorPrefs.GetBool("MCP4Unity_Auto_Start", true))
            {
                Inst.Start();
            }
        }  
        public static void CheckAndBuildMCPConsole()
        {
            try
            {
                // 获取MCPConsole路径（相对于项目根目录）
                string projectPath = Path.GetDirectoryName(Application.dataPath);
                string mcpConsolePath = Path.Combine(projectPath, MCPConsoleFolderName);
                string mcpConsoleExePath = Path.Combine(mcpConsolePath, MCPConsoleExeName);
                string buildScriptPath = Path.Combine(mcpConsolePath, BuildScriptName);
                
                // 检查文件夹是否存在
                if (!Directory.Exists(mcpConsolePath))
                {
                    UnityEngine.Debug.LogWarning($"MCPConsole folder not found at: {mcpConsolePath}");
                    return;
                }
                
                // 检查可执行文件是否存在
                if (!File.Exists(mcpConsoleExePath))
                {
                    UnityEngine.Debug.Log($"MCPConsole not found. Running build script...");
                    
                    // 检查构建脚本是否存在
                    if (!File.Exists(buildScriptPath))
                    {
                        UnityEngine.Debug.LogError($"Build script not found at: {buildScriptPath}");
                        return;
                    }
                    
                    // 运行构建脚本
                    ProcessStartInfo psi;
                    if (Application.platform == RuntimePlatform.WindowsEditor)
                    {
                        // Windows: 直接执行批处理文件
                        psi = new ProcessStartInfo
                        {
                            FileName = buildScriptPath,
                            WorkingDirectory = mcpConsolePath,
                            UseShellExecute = true,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                    }
                    else
                    {
                        // macOS/Linux: 使用bash执行shell脚本，设置PATH环境变量
                        psi = new ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            Arguments = $"\"{buildScriptPath}\"",
                            WorkingDirectory = mcpConsolePath,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        
                        // 确保dotnet能被找到
                        psi.EnvironmentVariables["PATH"] = "/usr/local/share/dotnet:/usr/local/bin:/opt/dotnet:" + 
                                                          Environment.GetEnvironmentVariable("PATH");
                    }
                    
                    using (Process process = Process.Start(psi))
                    {
                        if (process != null)
                        {
                            if (!Application.platform.Equals(RuntimePlatform.WindowsEditor))
                            {
                                // 对于macOS/Linux，读取输出信息
                                string output = process.StandardOutput.ReadToEnd();
                                string error = process.StandardError.ReadToEnd();
                                
                                process.WaitForExit();
                                
                                if (process.ExitCode != 0)
                                {
                                    UnityEngine.Debug.LogError($"Build script failed with exit code {process.ExitCode}");
                                    if (!string.IsNullOrEmpty(output)) UnityEngine.Debug.Log($"Output: {output}");
                                    if (!string.IsNullOrEmpty(error)) UnityEngine.Debug.LogError($"Error: {error}");
                                }
                                else
                                {
                                    UnityEngine.Debug.Log($"Build script completed successfully");
                                    if (!string.IsNullOrEmpty(output)) UnityEngine.Debug.Log($"Output: {output}");
                                }
                            }
                            else
                            {
                                process.WaitForExit();
                            }
                            
                            // 检查编译结果
                            if (File.Exists(mcpConsoleExePath))
                            {
                                UnityEngine.Debug.Log($"MCPConsole successfully built at: {mcpConsoleExePath}");
                            }
                            else
                            {
                                UnityEngine.Debug.LogError($"Failed to build MCPConsole at: {mcpConsoleExePath}");
                                // 尝试查找可执行文件的实际位置
                                string[] possiblePaths = {
                                    Path.Combine(mcpConsolePath, "bin", "Release", "net9.0", MCPConsoleExeName),
                                    Path.Combine(mcpConsolePath, "bin", "Release", "net9.0", "osx-arm64", MCPConsoleExeName),
                                    Path.Combine(mcpConsolePath, "bin", "Release", "net9.0", "osx-x64", MCPConsoleExeName),
                                    Path.Combine(mcpConsolePath, "bin", "Release", "net9.0", "linux-arm64", MCPConsoleExeName),
                                    Path.Combine(mcpConsolePath, "bin", "Release", "net9.0", "linux-x64", MCPConsoleExeName)
                                };
                                
                                foreach (string path in possiblePaths)
                                {
                                    if (File.Exists(path))
                                    {
                                        UnityEngine.Debug.Log($"Found MCPConsole at: {path}");
                                        // 尝试复制到预期位置
                                        try
                                        {
                                            File.Copy(path, mcpConsoleExePath, true);
                                            UnityEngine.Debug.Log($"Copied MCPConsole to expected location: {mcpConsoleExePath}");
                                        }
                                        catch (Exception copyEx)
                                        {
                                            UnityEngine.Debug.LogError($"Failed to copy MCPConsole: {copyEx.Message}");
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.LogError("Failed to start build process");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error checking/building MCPConsole: {ex.Message}");
            }
        }
        public async void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            HttpListener = new HttpListener();
            HttpListener.Prefixes.Add("http://localhost:8080/mcp/");
            HttpListener.Start();
            Running = true;
            OnStateChange?.Invoke();
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var getContextTask = HttpListener.GetContextAsync();
                Task completedTask = await Task.WhenAny(getContextTask, Task.Delay(-1, _cancellationTokenSource.Token));
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;
                var httpContext = await getContextTask;
                _ = HandleHttpRequest(httpContext);
            }
            Running = false;
            OnStateChange?.Invoke();
        }

        public void Stop()
        {
            if (Running)
            {
                _cancellationTokenSource?.Cancel();
                if (HttpListener != null)
                {
                    if (HttpListener.IsListening)
                    {
                        HttpListener.Stop();
                    }
                    HttpListener.Close();
                    HttpListener = null;
                }
                Running = false;
                OnStateChange?.Invoke();
            }
        }

        private async Task HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                string requestBody = "";
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                
                string responseContent =JsonConvert.SerializeObject(ProcessRequest(requestBody), SerializerSettings);
                
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseContent);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling HTTP request: {ex.Message}");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }

        private MCPResponse ProcessRequest(string requestBody)
        {
            try
            {
                MCPRequest request = JsonConvert.DeserializeObject<MCPRequest>(requestBody);
                switch (request.method.ToLower())
                {
                    case "listtools":
                        var tools = MCPFunctionInvoker.GetTools();
                        return MCPResponse.Success(tools);
                    case "calltool":
                        ToolArgs toolArgs = JsonConvert.DeserializeObject<ToolArgs>(request.params_);
                        
                        // 准备参数字典用于历史记录
                        var parameters = new System.Collections.Generic.Dictionary<string, string>();
                        if (toolArgs.arguments != null)
                        {
                            foreach (var prop in toolArgs.arguments.Properties())
                            {
                                parameters[prop.Name] = prop.Value?.ToString() ?? "";
                            }
                        }
                        
                        try
                        {
                            object res = MCPFunctionInvoker.Invoke(toolArgs.name, toolArgs.arguments);
                            string resultStr = res?.ToString() ?? "null";
                            
                            // 记录成功的工具调用历史
                            AddMCPExecutionHistory(toolArgs.name, parameters, resultStr, true);
                            
                            return MCPResponse.Success(res);
                        }
                        catch (Exception invokeEx)
                        {
                            // 记录失败的工具调用历史
                            AddMCPExecutionHistory(toolArgs.name, parameters, invokeEx.Message, false);
                            
                            return MCPResponse.Error(invokeEx);
                        }
                }
                return MCPResponse.Error($"unknown method:{request.method}") ;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing request: {ex.Message}");
                return MCPResponse.Error(ex);
            }
        }
        
        /// <summary>
        /// 添加MCP工具执行历史记录
        /// </summary>
        public static void AddMCPExecutionHistory(string toolName, Dictionary<string, string> parameters, string result, bool success)
        {
            AddExecutionHistory(toolName, parameters, result, success, ToolExecutionSource.MCP);
        }
        
        /// <summary>
        /// 添加执行历史记录（通用方法）
        /// </summary>
        public static void AddExecutionHistory(string toolName, Dictionary<string, string> parameters, string result, bool success, ToolExecutionSource source)
        {
            var historyItem = new ToolExecutionHistory(toolName, parameters, result, success, source);
            MCPExecutionHistory.Add(historyItem);
            
            // 保持最多50条记录
            if (MCPExecutionHistory.Count > 50)
            {
                MCPExecutionHistory.RemoveAt(0);
            }
            
            // 保存历史记录
            SaveExecutionHistory();
            
            // 通知历史更新
            OnHistoryUpdated?.Invoke();
        }
        
        /// <summary>
        /// 加载执行历史记录
        /// </summary>
        private static void LoadExecutionHistory()
        {
            try
            {
                string historyJson = EditorPrefs.GetString(HistoryPrefKey, "[]");
                var historyList = JsonConvert.DeserializeObject<List<ToolExecutionHistory>>(historyJson);
                if (historyList != null)
                {
                    MCPExecutionHistory.Clear();
                    MCPExecutionHistory.AddRange(historyList);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load MCP execution history: {ex.Message}");
                MCPExecutionHistory.Clear();
            }
        }
        
        /// <summary>
        /// 保存执行历史记录
        /// </summary>
        private static void SaveExecutionHistory()
        {
            try
            {
                string historyJson = JsonConvert.SerializeObject(MCPExecutionHistory, Formatting.None);
                EditorPrefs.SetString(HistoryPrefKey, historyJson);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save MCP execution history: {ex.Message}");
            }
        }
    }
    public class MCPRequest
    {
        public string method;
        [JsonProperty("params")]
        public string params_;
    }
    public class ToolArgs
    {
        public string name;
        public JObject arguments;
    }

    public class MCPResponse
    {
        public bool success;
        public object result;
        public string error;

        public static MCPResponse Success(object result)
        { 
            return new MCPResponse
            {
                success = true,
                result = result
            };
        }

        public static MCPResponse Error(string error)
        {
            return new MCPResponse
            {
                success = false,
                error = error
            };
        }
        public static MCPResponse Error(Exception ex) => Error(ex.Message);
    }
}
