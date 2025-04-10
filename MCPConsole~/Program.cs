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
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _unityMcpUrl = "http://localhost:8080/mcp/"; // Fixed Unity MCP service URL
        
        // 通过HTTP调用Unity中的MCPService
        private static async Task<JsonNode> CallUnityMcpServiceAsync(string method, object parameters)
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
                    return JsonNode.Parse(mcpResponse.Result.ToString());
                }
                else
                {
                    Console.Error.WriteLine($"Error from Unity MCP service: {mcpResponse.Error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error calling Unity MCP service: {ex.Message}");
                return null;
            }
        }
        
        public static async Task Main(string[] args)
        {
            Console.WriteLine("MCP service starting...");
            McpServerOptions options = new()
            {
                ServerInfo = new() { Name = "MCP4Unity", Version = "1.0.0" },
                Capabilities = new()
                {
                    Tools = new()
                    {
                        ListToolsHandler = async (request, cancellationToken) =>
                        {
                            try
                            {
                                Console.WriteLine($"ListToolsHandler");
                                // 调用Unity中的listtools方法获取工具列表
                                JsonNode result = await CallUnityMcpServiceAsync("listtools", null);
                                Console.WriteLine($"ListToolsHandler result: {result}");
                                if (result != null && result["tools"] != null && result["tools"] is JsonArray toolsArray)
                                {
                                    List<Tool> tools = [];
                                    foreach (JsonNode node in toolsArray)
                                    {
                                        tools.Add(new()
                                        {
                                            Name = node["name"]?.GetValue<string>(),
                                            Description = node["description"]?.GetValue<string>(),
                                            InputSchema = JsonDocument.Parse(node["inputSchema"].ToJsonString()).RootElement,
                                        });
                                    }

                                    return new ListToolsResult
                                    {
                                        Tools = tools
                                    };
                                }
                                
                                // 如果无法获取工具列表，返回空数组
                                return new ListToolsResult
                                {
                                    Tools = []
                                };
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Error in ListToolsHandler: {ex.Message}");
                                return new ListToolsResult
                                {
                                    Tools = []
                                };
                            }
                        },

                        CallToolHandler = async (request, cancellationToken) =>
                        {
                            try
                            {
                                Console.WriteLine($"Calling tool: {request.Params.Name}");
                                
                                // 调用Unity中的calltool方法执行工具
                                var parameters = new 
                                {
                                    name = request.Params.Name,
                                    arguments = request.Params.Arguments
                                };
                                
                                JsonNode result = await CallUnityMcpServiceAsync("callTool", parameters);
                                
                                if (result != null)
                                {
                                    return new CallToolResponse
                                    {
                                        Content = [new() { Type = "text", Text = result.ToString() }]
                                    };
                                }

                                // 如果调用失败，返回错误信息
                                return new CallToolResponse
                                {
                                    IsError = true,
                                    Content = [new() { Type = "text", Text = $"CallTool {request.Params.Name} failed" }]
                                };
                            }
                            catch (Exception ex)
                            {
                                return new CallToolResponse
                                {
                                    IsError = true,
                                    Content = [new() { Type = "text", Text = $"CallTool {request.Params.Name} error: {ex.Message}" }]
                                };
                            }
                        },
                    }
                },
            };
           
            await using IMcpServer server = McpServerFactory.Create(new StdioServerTransport("MCP4Unity"), options);

            await server.RunAsync();
        }
    }
    
    // 用于解析Unity MCP服务响应的类
    public class McpResponse
    {
        public bool Success { get; set; }
        public JsonElement Result { get; set; }
        public string Error { get; set; }
    }
}