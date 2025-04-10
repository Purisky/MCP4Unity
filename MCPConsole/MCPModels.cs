using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPConsole
{
    // Request model
    public class MCPRequest
    {
        public string method { get; set; }
        
        [JsonProperty("params")]
        public string @params { get; set; }
    }
    // Response model
    public class MCPResponse
    {
        public bool success { get; set; }
        public object result { get; set; }
        public string error { get; set; }
        
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