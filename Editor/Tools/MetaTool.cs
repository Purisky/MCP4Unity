using System.Linq;
using System.Text;
using MCP4Unity;
using MCP4Unity.Editor;

namespace MCP
{
    public class MetaTool
    {
        [Tool("列出所有已注册的工具，重载脚本后返回工具名称和描述")]
        public static string ListTools()
        {
            // 重载工具注册
            MCPFunctionInvoker.Tools.Clear();
            MCPFunctionInvoker.LoadMethods();

            var sb = new StringBuilder();
            sb.AppendLine($"已注册工具数量: {MCPFunctionInvoker.Tools.Count}");
            sb.AppendLine();

            foreach (var kvp in MCPFunctionInvoker.Tools.OrderBy(k => k.Key))
            {
                var tool = kvp.Value;
                sb.AppendLine($"  {tool.name} - {tool.description}");
                if (tool.inputSchema.orderedProperties.Count > 0)
                {
                    foreach (var prop in tool.inputSchema.orderedProperties)
                    {
                        sb.AppendLine($"    param: {prop.Name} ({prop.type}) {prop.description ?? ""}");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
