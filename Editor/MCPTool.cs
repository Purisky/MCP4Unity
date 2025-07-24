using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace MCP4Unity.Editor
{
    public class Property
    {
        [JsonIgnore]
        public Type Type;
        [JsonIgnore]
        public string Name;
        [JsonIgnore]
        public int Order; // Add order to maintain parameter sequence
        [JsonIgnore]
        public ParameterInfo ParameterInfo; // Store parameter info for attribute access
        public string type;

        public string description;
        public Property(string name, Type type_, int order = 0, ParameterInfo parameterInfo = null)
        {
            Type = type_;
            Name = name;
            Order = order;
            ParameterInfo = parameterInfo;
            type = SharpTypeToTypeScriptType(type_);
        }
        
        /// <summary>
        /// Check if this parameter has ParamDropdown attribute
        /// </summary>
        public bool HasDropdown => ParameterInfo?.GetCustomAttribute<ParamDropdownAttribute>() != null;
        
        /// <summary>
        /// Get the ParamDropdown attribute if it exists
        /// </summary>
        public ParamDropdownAttribute GetDropdownAttribute() => ParameterInfo?.GetCustomAttribute<ParamDropdownAttribute>();
        
        public static string SharpTypeToTypeScriptType(Type type)
        {
            if (type == typeof(int))
            {
                return "integer";
            }
            if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                return "number";
            }
            else if (type == typeof(string))
            {
                return "string";
            }
            else if (type == typeof(bool))
            {
                return "boolean";
            }
            else if (type == typeof(DateTime))
            {
                return "string";
            }
            else if (type == typeof(void))
            {
                return "void";
            }
            else if (type == typeof(object))
            {
                return "object";
            }
            else if (type.IsGenericType)
            {
                Type genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(List<>))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    return $"{SharpTypeToTypeScriptType(itemType)}[]";
                }
            }
            else if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                return $"{SharpTypeToTypeScriptType(elementType)}[]";
            }
            return "object";
        }
    }

    public class InputSchema
    {
        public string type = "object";
        public Dictionary<string, Property> properties = new();
        
        // Add ordered properties list to maintain parameter order
        [JsonIgnore]
        public List<Property> orderedProperties = new();
    }

    public class ToolResponse
    {
        public Content[] content;
        public bool isError;
    }
    public class Content
    {
        public string type = "text";
        public string text;
    }
    public class Tools
    {
        public List<MCPTool> tools = new();
    }
    public class MCPTool
    {
        public string name;
        public string description;
        public InputSchema inputSchema;
        public Property returns;
        [JsonIgnore]
        public MethodInfo MethodInfo;

        public MCPTool(MethodInfo methodInfo,ToolAttribute toolAttribute)
        {
            MethodInfo = methodInfo;
            name = methodInfo.Name.ToLower();
            description = toolAttribute.Desc;
            inputSchema = new();
            ParameterInfo[] parameters = methodInfo.GetParameters();
            
            // Process parameters in their original order
            for (int i = 0; i < parameters.Length; i++)
            {
                DescAttribute descAttribute = parameters[i].GetCustomAttribute<DescAttribute>();
                Property property = new(parameters[i].Name, parameters[i].ParameterType, i, parameters[i]);
                if (descAttribute != null)
                {
                    property.description = descAttribute.Desc;
                }
                
                // Add to both dictionary and ordered list
                inputSchema.properties.Add(parameters[i].Name, property);
                inputSchema.orderedProperties.Add(property);
            }
            returns = new("return", methodInfo.ReturnType) { description = toolAttribute.ReturnDesc};
        }

    }



}
