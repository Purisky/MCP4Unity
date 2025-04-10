using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPConsole
{
    public class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static HttpListener _httpListener;
        private static readonly string _unityMcpUrl = "http://localhost:8080/mcp/"; // Fixed Unity MCP service URL
        
        public static async Task Main(string[] args)
        {
            Console.WriteLine("MCP service starting...");
            
            // Port must be manually specified, default value not allowed
            if (args.Length < 1 || !int.TryParse(args[0], out int localPort))
            {
                Console.WriteLine("Error: You must specify a listening port.");
                Console.WriteLine("Usage: MCPConsole.exe <port>");
                Console.WriteLine("Example: MCPConsole.exe 9090");
                return;
            }
            
            try
            {
                await StartServer(localPort);
                Console.WriteLine($"MCP service started, listening on port: {localPort}, connected to Unity MCP: {_unityMcpUrl}");
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"Failed to start service: {ex.Message}");
                if (ex.ErrorCode == 183) // Address already in use
                {
                    Console.WriteLine($"Port {localPort} is already in use, please specify another port.");
                }
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred when starting service: {ex.Message}");
                return;
            }
            
            // Keep the program running
            await Task.Delay(-1);
        }
        
        private static async Task StartServer(int port)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{port}/mcp/");
            _httpListener.Start(); // This will throw an exception if the port is already in use
            
            // Start asynchronous request processing
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var context = await _httpListener.GetContextAsync();
                        _ = HandleHttpRequest(context);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error occurred when processing HTTP request: {ex.Message}");
                    }
                }
            });
        }
        
        private static async Task HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                // Handle OPTIONS requests (CORS preflight requests)
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    HandleCorsRequest(context);
                    return;
                }
                
                // Read request content
                string requestBody = "";
                using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                
                // Process request and generate response
                string responseContent = JsonConvert.SerializeObject(await ProcessRequest(requestBody));
                
                // Set CORS response headers
                HandleCorsRequest(context);
                
                // Send response
                byte[] buffer = Encoding.UTF8.GetBytes(responseContent);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred when processing HTTP request: {ex.Message}");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }
        
        private static void HandleCorsRequest(HttpListenerContext context)
        {
            // Set CORS response headers
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        }
        
        private static async Task<MCPResponse> ProcessRequest(string requestBody)
        {
            try
            {
                MCPRequest request = JsonConvert.DeserializeObject<MCPRequest>(requestBody);
                
                // Forward request to Unity's MCP service
                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                
                try
                {
                    var response = await _httpClient.PostAsync(_unityMcpUrl, content);
                    response.EnsureSuccessStatusCode();
                    
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var mcpResponse = JsonConvert.DeserializeObject<MCPResponse>(responseContent);
                    
                    return mcpResponse;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cannot connect to Unity's MCP service: {ex.Message}");
                    return MCPResponse.Error("Cannot connect to Unity's MCP service. Please make sure Unity is running and the MCP4Unity service is started.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred when processing request: {ex.Message}");
                return MCPResponse.Error(ex);
            }
        }
    }
}