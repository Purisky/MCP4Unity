using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MCP4Unity
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = false)]
    public class DescAttribute : Attribute
    {
        public string Desc;
        public DescAttribute( string desc = null)
        {
            Desc = desc;
        }
    }
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class ParamDropdownAttribute : Attribute
    { 
        public string MethodName;
        public ParamDropdownAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ToolAttribute : DescAttribute
    {
        public string ReturnDesc;
        public ToolAttribute(string desc = null, string returnDesc = null): base(desc)
        {
            ReturnDesc = returnDesc;
        }
    }
}
