#nullable enable
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MCPConsole
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            McpServerOptions options = new()
            {
                ServerInfo = new Implementation { Name = "MCP4Unity", Version = "1.0.0" },
                Capabilities = new()
                {
                    Tools = new ToolsCapability(),
                },
                Handlers = new McpServerHandlers
                {
                    ListToolsHandler = ListToolsHandlerAsync,
                    CallToolHandler = CallToolHandlerAsync,
                },
            };

            await using McpServer server = McpServer.Create(new StdioServerTransport("MCP4Unity"), options);

            await server.RunAsync();
        }

        #region Http=>Unity
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            UseProxy = false
        });
        private const int DefaultPort = 8080;
        private const string ConfigFileName = "Assets/MCP4Unity/mcp_config.json";
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        private static string? _cachedUnityMcpUrl;

        private static string BuildFallbackUrl(int port) => $"http://127.0.0.1:{port}/mcp/";

        static async Task<string> CallUnityMcpServiceAsync(string method, object parameters)
        {
            Console.Error.WriteLine($"[MCP4Unity] CallUnityMcpServiceAsync: method={method}");
            string unityMcpUrl = await ResolveUnityMcpUrlAsync().ConfigureAwait(false);
            Console.Error.WriteLine($"[MCP4Unity] Resolved URL: {unityMcpUrl}");

            var request = new
            {
                method = method,
                @params = parameters != null ? JsonSerializer.Serialize(parameters) : null
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(unityMcpUrl, content).ConfigureAwait(false);
            }
            catch
            {
                _cachedUnityMcpUrl = null;
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                _cachedUnityMcpUrl = null;
            }
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var mcpResponse = JsonSerializer.Deserialize<McpResponse>(responseBody, JsonOptions) ?? throw new JsonException($"Failed to deserialize MCP response:{responseBody}");
            if (mcpResponse.Success)
            {
                return mcpResponse.Result.ToString();
            }
            else
            {
                throw new Exception($"{mcpResponse.Error}");
            }
        }

        private static async Task<string> ResolveUnityMcpUrlAsync()
        {
            const int retryDelayMs = 500;

            if (_cachedUnityMcpUrl != null)
            {
                return _cachedUnityMcpUrl;
            }

            int attempt = 0;
            while (true)
            {
                attempt++;

                // Priority: MCP4UNITY_ENDPOINT env var > mcp_endpoint.json (runtime) > mcp_config.json (configured port) > default port
                string? fromEnv = Environment.GetEnvironmentVariable("MCP4UNITY_ENDPOINT");
                string? fromState = LoadUnityMcpUrlFromState();
                string candidate = fromEnv ?? fromState ?? BuildFallbackUrl(LoadConfiguredPort());

                if (attempt <= 5)
                {
                    Console.Error.WriteLine($"[MCP4Unity] attempt={attempt} fromEnv={fromEnv ?? "null"} fromState={fromState ?? "null"} candidate={candidate}");
                }

                if (await IsEndpointReachable(candidate).ConfigureAwait(false))
                {
                    if (attempt <= 5)
                    {
                        Console.Error.WriteLine($"[MCP4Unity] Connected to {candidate} on attempt {attempt}");
                    }
                    _cachedUnityMcpUrl = candidate;
                    return candidate;
                }

                if (attempt <= 5 || attempt % 20 == 0)
                {
                    Console.Error.WriteLine($"[MCP4Unity] Waiting for Unity MCP endpoint... attempt={attempt} candidate={candidate}");
                }

                await Task.Delay(retryDelayMs).ConfigureAwait(false);
            }
        }

        private static int LoadConfiguredPort()
        {
            try
            {
                string? projectRoot = ResolveProjectRoot();
                if (string.IsNullOrEmpty(projectRoot)) return DefaultPort;

                string configPath = Path.Combine(projectRoot, ConfigFileName);
                if (!File.Exists(configPath)) return DefaultPort;

                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<MCPConfigFile>(json, JsonOptions);
                if (config != null && config.Port > 0 && config.Port <= 65535)
                    return config.Port;
            }
            catch { }
            return DefaultPort;
        }

        private class MCPConfigFile
        {
            public int Port { get; set; }
        }

        private static async Task<bool> IsEndpointReachable(string endpoint)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
                using HttpResponseMessage response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
                return response.IsSuccessStatusCode || (int)response.StatusCode >= 400;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is SocketException)
            {
                return false;
            }
        }

        private static string? LoadUnityMcpUrlFromState()
        {
            try
            {
                string? projectRoot = ResolveProjectRoot();
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return null;
                }

                string endpointStatePath = Path.Combine(projectRoot, "Library", "MCP4Unity", "mcp_endpoint.json");
                if (!File.Exists(endpointStatePath))
                {
                    return null;
                }

                string json = File.ReadAllText(endpointStatePath);
                EndpointState? endpoint = JsonSerializer.Deserialize<EndpointState>(json, JsonOptions);
                string? url = endpoint?.Url;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    url = url.Replace("://localhost:", "://127.0.0.1:");
                }

                return string.IsNullOrWhiteSpace(url) ? null : url;
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolveProjectRoot()
        {
            string? envProjectPath = Environment.GetEnvironmentVariable("MCP4UNITY_PROJECT_PATH");
            if (!string.IsNullOrWhiteSpace(envProjectPath) && Directory.Exists(envProjectPath))
            {
                return envProjectPath;
            }

            string baseDir = AppContext.BaseDirectory;
            DirectoryInfo? current = new DirectoryInfo(baseDir);
            for (int depth = 0; depth < 8 && current != null; depth++)
            {
                bool hasAssets = Directory.Exists(Path.Combine(current.FullName, "Assets"));
                bool hasProjectSettings = Directory.Exists(Path.Combine(current.FullName, "ProjectSettings"));
                if (hasAssets && hasProjectSettings)
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return null;
        }

        private class EndpointState
        {
            public string? Url { get; set; }
            public int Port { get; set; }
            public string? ProjectPath { get; set; }
            public string? UpdatedAtUtc { get; set; }
        }

        public class McpResponse
        {
            public bool Success { get; set; }
            public JsonElement Result { get; set; }
            public string? Error { get; set; }
        }
        #endregion

        #region Tool
        static async ValueTask<ListToolsResult> ListToolsHandlerAsync(
            RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
        {
            try
            {
                string result = await CallUnityMcpServiceAsync("listtools", null!);
                if (result != null)
                {
                    return JsonSerializer.Deserialize<ListToolsResult>(result, McpJsonUtilities.DefaultOptions) ?? new();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in ListToolsHandler: {ex.Message}");
            }
            return new ListToolsResult
            {
                Tools = []
            };
        }

        static async ValueTask<CallToolResult> CallToolHandlerAsync(
            RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
        {
            string toolName = request.Params?.Name ?? "unknown";

            try
            {
                Console.Error.WriteLine($"Calling tool: {toolName}");

                if (string.IsNullOrWhiteSpace(toolName))
                {
                    return new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = "Tool name is required but was null or empty" }]
                    };
                }

                // 本地工具（不需要 Unity 运行）
                var localResult = HandleLocalTool(toolName, request.Params?.Arguments);
                if (localResult != null)
                {
                    return new CallToolResult { Content = [new TextContentBlock { Text = localResult }] };
                }

                // Unity 工具
                var parameters = new
                {
                    name = toolName,
                    arguments = request.Params?.Arguments ?? new Dictionary<string, JsonElement>()
                };
                string result = await CallUnityMcpServiceAsync("callTool", parameters);

                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = result }]
                };
            }
            catch (Exception ex)
            {
                if (ex is TargetInvocationException tex)
                {
                    ex = tex.InnerException ?? tex;
                }
                return new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = $"Tool '{toolName}' execution failed: {ex}" }]
                };
            }
        }
        
        static string? HandleLocalTool(string toolName, IDictionary<string, JsonElement>? args)
        {
            switch (toolName.ToLower())
            {
                case "configureunity":
                    var unityPath = args?["unityExePath"].GetString() ?? "";
                    var projectPath = args?["projectPath"].GetString() ?? "";
                    UnityManager.SaveConfig(unityPath, projectPath);
                    return $"✅ Unity configured: {unityPath}";
                
                case "startunity":
                    UnityManager.DeleteSceneBackups();
                    UnityManager.DeleteScriptAssemblies();
                    UnityManager.StartUnity();
                    return "✅ Unity starting (cleaned backups & assemblies)...";
                
                case "stopunity":
                    UnityManager.StopUnity();
                    return "✅ Unity stopped";
                
                case "isunityrunning":
                    return UnityManager.IsUnityRunning() ? "✅ Unity is running" : "❌ Unity is not running";
                
                case "deletescenebackups":
                    UnityManager.DeleteSceneBackups();
                    return "✅ Scene backups deleted";
                
                case "deletescriptassemblies":
                    UnityManager.DeleteScriptAssemblies();
                    return "✅ ScriptAssemblies deleted";
                
                default:
                    return null;
            }
        }
        #endregion



    }
}
