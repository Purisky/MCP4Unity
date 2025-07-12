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
                    try
                    {
                        Console.WriteLine($"Calling tool: {request.Params.Name}");

                        // 调用Unity中的calltool方法执行工具
                        var parameters = new
                        {
                            name = request.Params.Name,
                            arguments = request.Params.Arguments
                        };

                        string result = await CallUnityMcpServiceAsync("callTool", parameters);

                        if (result != null)
                        {
                            return new CallToolResponse
                            {
                                Content = [new() { Type = "text", Text = result }]
                            };
                        }
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
            };
        }
        #endregion



    }
}