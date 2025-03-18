using System;
using UnityEngine;

namespace MCP4Unity
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MCPAttribute : Attribute
    {
        public string Desc;
        public MCPAttribute( string desc = null)
        {
            Desc = desc;
        }
    }

    [AttributeUsage(AttributeTargets.Method| AttributeTargets.Parameter, AllowMultiple = false)]
    public class ToolAttribute : MCPAttribute
    {
        public ToolAttribute(string desc = null)
        {
            Desc = desc;
        }
    }
}
