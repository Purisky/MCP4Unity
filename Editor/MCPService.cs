using System;
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
        private const string MCPConsoleExeName = "MCPConsole.exe";
        private const string BuildBatName = "build.bat";

        static JsonSerializerSettings SerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
        };
        static MCPService()
        {
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
                string buildBatPath = Path.Combine(mcpConsolePath, BuildBatName);
                
                // 检查文件夹是否存在
                if (!Directory.Exists(mcpConsolePath))
                {
                    UnityEngine.Debug.LogWarning($"MCPConsole folder not found at: {mcpConsolePath}");
                    return;
                }
                
                // 检查可执行文件是否存在
                if (!File.Exists(mcpConsoleExePath))
                {
                    UnityEngine.Debug.Log($"MCPConsole.exe not found. Running build script...");
                    
                    // 检查build.bat是否存在
                    if (!File.Exists(buildBatPath))
                    {
                        UnityEngine.Debug.LogError($"Build script not found at: {buildBatPath}");
                        return;
                    }
                    
                    // 运行build.bat
                    ProcessStartInfo psi = new ()
                    {
                        FileName = buildBatPath,
                        WorkingDirectory = mcpConsolePath,
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    
                    using (Process process = Process.Start(psi))
                    {
                        process.WaitForExit();
                        
                        // 检查编译结果
                        if (File.Exists(mcpConsoleExePath))
                        {
                            UnityEngine.Debug.Log("MCPConsole.exe successfully built.");
                        }
                        else
                        {
                            UnityEngine.Debug.LogError("Failed to build MCPConsole.exe.");
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
                        return MCPResponse.Success(MCPFunctionInvoker.GetTools());
                    case "calltool":
                        ToolArgs toolArgs = JsonConvert.DeserializeObject<ToolArgs>(request.params_);
                        object res = MCPFunctionInvoker.Invoke(toolArgs.name, toolArgs.arguments);
                        return MCPResponse.Success(res);

                }
                return MCPResponse.Error($"unknown method:{request.method}") ;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing request: {ex.Message}");
                return MCPResponse.Error(ex);
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
