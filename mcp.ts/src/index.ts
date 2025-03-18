import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  ListToolsRequestSchema,
  CallToolRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";


const server = new Server(
  {
    name: "UnityMCP",
    version: "1.0.0",
    description: "A simple MCP server for Unity",
  },
  {
    capabilities: {
      // prompts: {},
      // resources: {},
      tools: {},
    },
  }
);
async function sendHttpRequest(req: any): Promise<any> {
  try {
    const response = await fetch("http://localhost:8080/mcp", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(req)
    });
    
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    
    const result = await response.json();
    console.error("Received HTTP response:", result);
    return result;
  } catch (error: any) {
    console.error("Error sending HTTP request:", error);
    throw error;
  }
}
server.setRequestHandler(ListToolsRequestSchema, async () => {
  try{
    const response = await sendHttpRequest({
      method: "listTools",
    });
    return response.result;
  }
  catch (error: any) {
    return {
      tools: [],
    };
  }
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  console.error(`Tool ${name} called with arguments`, args);
  try {
    const response = await sendHttpRequest({
      method: "callTool",
      params: JSON.stringify( { name, arguments: args }),
    });
    console.error(response);
    const result = response.result;
    return {
      content: [{ 
        type: "text", 
        text: JSON.stringify(result)
      }],
      isError: !response.success,
    };
  } catch (error: any) {
    return {
      content: [{ 
        type: "text", 
        text: `CallTool ${name} error: ${error.message}` 
      }],
      isError: true,
    };
  }
});

const transport = new StdioServerTransport();
await server.connect(transport);
