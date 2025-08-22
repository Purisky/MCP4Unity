using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MCPConsole
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            //Console.WriteLine("MCP service starting...");
            McpServerOptions options = new()
            {
                ServerInfo = new() { Name = "MCP4Unity", Version = "1.0.0" },
                Capabilities = new()
                {
                    Tools = InitToolCapability(),
                },
            };

            await using IMcpServer server = McpServerFactory.Create(new StdioServerTransport("MCP4Unity"), options);

            await server.RunAsync();
        }

        #region Http=>Unity
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _unityMcpUrl = "http://localhost:8080/mcp/";
        static async Task<string> CallUnityMcpServiceAsync(string method, object parameters)
        {
            try
            {
                var request = new
                {
                    method = method,
                    @params = parameters != null ? JsonSerializer.Serialize(parameters) : null
                };

                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(_unityMcpUrl, content);

                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var mcpResponse = JsonSerializer.Deserialize<McpResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (mcpResponse.Success)
                {
                    return mcpResponse.Result.ToString();
                }
                else
                {
                    // 返回具体的Unity服务错误信息而不是打印到控制台
                    throw new InvalidOperationException($"Unity MCP service error: {mcpResponse.Error}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"HTTP request failed - Unity MCP service may not be running at {_unityMcpUrl}: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TimeoutException($"Request to Unity MCP service timed out: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse response from Unity MCP service - invalid JSON format: {ex.Message}", ex);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"Unexpected error calling Unity MCP service method '{method}': {ex.Message}", ex);
            }
        }
        public class McpResponse
        {
            public bool Success { get; set; }
            public JsonElement Result { get; set; }
            public string Error { get; set; }
        }
        #endregion

        #region Tool
        static ToolsCapability InitToolCapability()
        {
            return new()
            {
                ListToolsHandler = async (request, cancellationToken) =>
                {
                    try
                    {
                        string result = await CallUnityMcpServiceAsync("listtools", null);
                        if (result != null)
                        {
                            return JsonSerializer.Deserialize<ListToolsResult>(result);
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
                },
                CallToolHandler = async (request, cancellationToken) =>
                {
                    string toolName = request.Params?.Name ?? "unknown";
                    
                    try
                    {
                        Console.WriteLine($"Calling tool: {toolName}");

                        // 验证工具名称
                        if (string.IsNullOrWhiteSpace(toolName))
                        {
                            return new CallToolResponse
                            {
                                IsError = true,
                                Content = [new() { Type = "text", Text = "Tool name is required but was null or empty" }]
                            };
                        }

                        // 调用Unity中的calltool方法执行工具
                        var parameters = new
                        {
                            name = toolName,
                            arguments = request.Params.Arguments ?? new Dictionary<string, object>()
                        };

                        string result = await CallUnityMcpServiceAsync("callTool", parameters);

                        return new CallToolResponse
                        {
                            Content = [new() { Type = "text", Text = result }]
                        };
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Unity MCP service error"))
                    {
                        // Unity服务返回的业务逻辑错误
                        return new CallToolResponse
                        {
                            IsError = true,
                            Content = [new() { Type = "text", Text = $"Tool '{toolName}' execution failed: {ex.Message}" }]
                        };
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("may not be running"))
                    {
                        // Unity服务连接失败
                        return new CallToolResponse
                        {
                            IsError = true,
                            Content = [new() { Type = "text", Text = $"Cannot connect to Unity MCP service. Please ensure Unity is running and MCP service is started. Details: {ex.Message}" }]
                        };
                    }
                    catch (TimeoutException ex)
                    {
                        // 请求超时
                        return new CallToolResponse
                        {
                            IsError = true,
                            Content = [new() { Type = "text", Text = $"Tool '{toolName}' execution timed out. The operation may be too complex or Unity may be unresponsive: {ex.Message}" }]
                        };
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("invalid JSON"))
                    {
                        // JSON解析错误
                        return new CallToolResponse
                        {
                            IsError = true,
                            Content = [new() { Type = "text", Text = $"Unity MCP service returned invalid response format for tool '{toolName}': {ex.Message}" }]
                        };
                    }
                    catch (Exception ex)
                    {
                        // 其他未预期的异常
                        return new CallToolResponse
                        {
                            IsError = true,
                            Content = [new() { Type = "text", Text = $"Unexpected error executing tool '{toolName}': {ex.GetType().Name} - {ex.Message}" }]
                        };
                    }
                },
            };
        }
        #endregion



    }
}