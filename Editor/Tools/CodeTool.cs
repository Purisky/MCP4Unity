using MCP4Unity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;

namespace MCP
{
    public class CodeTools
    {
        [Tool("重新编译程序集，等待编译完成后返回编译结果和错误信息")]
        public static async Task<string> RecompileAssemblies()
        {
            var tcs = new TaskCompletionSource<bool>();
            var errors = new List<CompilerMessage>();
            var warnings = new List<CompilerMessage>();
            object targetContext = null;

            void OnStarted(object context)
            {
                targetContext = context;
            }

            void OnAssemblyFinished(string assemblyPath, CompilerMessage[] messages)
            {
                foreach (var msg in messages)
                {
                    if (msg.type == CompilerMessageType.Error)
                        errors.Add(msg);
                    else if (msg.type == CompilerMessageType.Warning)
                        warnings.Add(msg);
                }
            }

            void OnFinished(object context)
            {
                CompilationPipeline.compilationStarted -= OnStarted;
                CompilationPipeline.assemblyCompilationFinished -= OnAssemblyFinished;
                CompilationPipeline.compilationFinished -= OnFinished;
                tcs.TrySetResult(true);
            }

            CompilationPipeline.compilationStarted += OnStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.compilationFinished += OnFinished;

            CompilationPipeline.RequestScriptCompilation();

            // 等待编译完成
            await tcs.Task;

            // 构建结果
            var result = new System.Text.StringBuilder();
            bool success = errors.Count == 0;

            result.AppendLine(success ? "✅ 编译成功" : "❌ 编译失败");

            if (errors.Count > 0)
            {
                result.AppendLine($"\n错误 ({errors.Count}):");
                foreach (var err in errors)
                {
                    result.AppendLine($"  {err.file}({err.line},{err.column}): {err.message}");
                }
            }

            if (warnings.Count > 0)
            {
                result.AppendLine($"\n警告 ({warnings.Count}):");
                foreach (var warn in warnings)
                {
                    result.AppendLine($"  {warn.file}({warn.line},{warn.column}): {warn.message}");
                }
            }

            return result.ToString();
        }
        [Tool("读取Unity控制台日志")]
        public static string GetUnityConsoleLog(
            [Desc("日志类型筛选: all(全部), error(仅错误), warning(仅警告), log(仅信息)")] string filter = "all",
            [Desc("是否折叠重复日志")] bool collapse = false,
            [Desc("显示最近N条日志")] int maxCount = 10)
        {
            try
            {
                var logInfo = GetLogEntriesInfo(filter, collapse, maxCount);
                return string.IsNullOrEmpty(logInfo) ? "📭 无匹配日志" : logInfo;
            }
            catch (System.Exception ex)
            {
                return $"❌ 获取日志失败: {ex.Message}";
            }
        }
        
        private static string GetLogEntriesInfo(string filter = "all", bool collapse = false, int maxCount = 10)
        {
            try
            {
                var result = new System.Text.StringBuilder();
                var logEntriesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
                if (logEntriesType == null)
                {
                    return "❌ 无法访问Unity日志系统";
                }

                var setConsoleFlagMethod = logEntriesType.GetMethod("SetConsoleFlag", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var getConsoleFlagsMethod = logEntriesType.GetMethod("get_consoleFlags", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                
                int originalFlags = 0;
                if (getConsoleFlagsMethod != null)
                {
                    originalFlags = (int)getConsoleFlagsMethod.Invoke(null, null);
                }

                if (setConsoleFlagMethod != null)
                {
                    setConsoleFlagMethod.Invoke(null, new object[] { 0x80, true });
                    setConsoleFlagMethod.Invoke(null, new object[] { 0x100, true });
                    setConsoleFlagMethod.Invoke(null, new object[] { 0x200, true });
                }

                var getCountMethod = logEntriesType.GetMethod("GetCount", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                
                if (getCountMethod == null || getEntryInternalMethod == null)
                {
                    return "❌ 无法访问日志方法";
                }

                int logCount = (int)getCountMethod.Invoke(null, null);
                if (logCount == 0)
                {
                    if (setConsoleFlagMethod != null && getConsoleFlagsMethod != null)
                    {
                        setConsoleFlagMethod.Invoke(null, new object[] { 0x80, (originalFlags & 0x80) != 0 });
                        setConsoleFlagMethod.Invoke(null, new object[] { 0x100, (originalFlags & 0x100) != 0 });
                        setConsoleFlagMethod.Invoke(null, new object[] { 0x200, (originalFlags & 0x200) != 0 });
                    }
                    return "📭 控制台暂无日志";
                }

                var logEntryType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntry");
                if (logEntryType == null)
                {
                    return "❌ 无法访问日志条目类型";
                }

                // 收集日志
                var logs = new System.Collections.Generic.List<(int type, string message, string location)>();
                var seenMessages = new System.Collections.Generic.HashSet<string>();
                var typeCounts = new System.Collections.Generic.Dictionary<int, int>();
                
                for (int i = 0; i < logCount; i++)
                {
                    try
                    {
                        var logEntry = System.Activator.CreateInstance(logEntryType);
                        var parameters = new object[] { i, logEntry };
                        
                        bool success = (bool)getEntryInternalMethod.Invoke(null, parameters);
                        if (!success || logEntry == null) continue;

                        int logType = GetLogTypeValue(logEntry, logEntryType, logEntriesType);
                        
                        if (!typeCounts.ContainsKey(logType))
                            typeCounts[logType] = 0;
                        typeCounts[logType]++;
                        
                        if (filter == "error" && logType != 0 && logType != 4) continue;
                        if (filter == "warning" && logType != 2) continue;
                        if (filter == "log" && logType != 3) continue;

                        string message = GetLogMessage(logEntry, logEntryType);
                        
                        if (collapse && !seenMessages.Add(message)) continue;

                        string location = GetLogLocation(logEntry, logEntryType);
                        logs.Add((logType, message, location));
                    }
                    catch
                    {
                        continue;
                    }
                }

                int errorCount = (typeCounts.ContainsKey(0) ? typeCounts[0] : 0) + (typeCounts.ContainsKey(4) ? typeCounts[4] : 0);
                int warningCount = typeCounts.ContainsKey(2) ? typeCounts[2] : 0;
                int logInfoCount = typeCounts.ContainsKey(3) ? typeCounts[3] : 0;
                
                result.AppendLine($"📊 日志统计: ❌错误 {errorCount} | ⚠️警告 {warningCount} | 🟢信息 {logInfoCount}");
                
                if (logs.Count == 0)
                {
                    return result.ToString() + $"\n📭 无匹配日志 (筛选: {filter})";
                }

                int startIndex = System.Math.Max(0, logs.Count - maxCount);
                int displayCount = logs.Count - startIndex;
                
                result.AppendLine($"匹配 {logs.Count} 条 (筛选: {filter}, 折叠: {collapse})\n");
                
                for (int i = startIndex; i < logs.Count; i++)
                {
                    var (type, message, location) = logs[i];
                    string typeIcon = GetLogTypeIcon(type);
                    result.AppendLine($"{typeIcon}{location}: {message}");
                }

                result.AppendLine($"\n显示了最近 {displayCount} 条日志");
                
                if (setConsoleFlagMethod != null && getConsoleFlagsMethod != null)
                {
                    setConsoleFlagMethod.Invoke(null, new object[] { 0x80, (originalFlags & 0x80) != 0 });
                    setConsoleFlagMethod.Invoke(null, new object[] { 0x100, (originalFlags & 0x100) != 0 });
                    setConsoleFlagMethod.Invoke(null, new object[] { 0x200, (originalFlags & 0x200) != 0 });
                }
                
                return result.ToString();
            }
            catch (System.Exception ex)
            {
                return $"❌ LogEntries访问失败: {ex.Message}";
            }
        }
    

        private static int GetLogTypeValue(object logEntry, System.Type logEntryType, System.Type logEntriesType)
        {
            // 先获取消息内容
            var conditionField = logEntryType.GetField("condition", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            string message = conditionField?.GetValue(logEntry)?.ToString() ?? "";
            
            // 编译器警告判断
            if (!string.IsNullOrEmpty(message) && message.Contains(": warning CS"))
                return 2;
            
            var fileField = logEntryType.GetField("file", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            string file = fileField?.GetValue(logEntry)?.ToString() ?? "";
            
            // MCP 内部日志
            if (file.IndexOf("MCPService.cs", System.StringComparison.Ordinal) >= 0 || 
                file.IndexOf("Watcher.cs", System.StringComparison.Ordinal) >= 0)
                return 3;
            
            // 使用 Unity Console 的 HasMode 方法判断日志类型
            var hasModeMethod = logEntriesType.GetMethod("HasMode", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            
            int detectedType = 3;
            if (hasModeMethod != null)
            {
                var modeField = logEntryType.GetField("mode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (modeField != null)
                {
                    int mode = (int)modeField.GetValue(logEntry);
                    
                    if ((bool)hasModeMethod.Invoke(null, new object[] { mode, 0x200 }))
                        detectedType = 3;
                    else if ((bool)hasModeMethod.Invoke(null, new object[] { mode, 0x100 }))
                        detectedType = 2;
                    else if ((bool)hasModeMethod.Invoke(null, new object[] { mode, 0x80 }))
                        detectedType = 0;
                }
            }
            
            // 异常检测：如果消息包含 Exception，强制标记为错误
            if (detectedType == 3 && !string.IsNullOrEmpty(message) && message.Contains("Exception"))
                return 0;
            
            return detectedType;
        }
        
        private static string GetLogCallstack(object logEntry, System.Type logEntryType)
        {
            // Unity LogEntry 的堆栈跟踪字段名可能是 "callstack" 或其他
            // 尝试所有可能的字段名
            string[] callstackFields = { "callstack", "stackTrace", "trace", "message" };
            foreach (var fieldName in callstackFields)
            {
                var field = logEntryType.GetField(fieldName, 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(logEntry);
                    if (value != null)
                    {
                        return value.ToString();
                    }
                }
            }
            return string.Empty;
        }

        private static string GetLogTypeIcon(int logType)
        {
            return logType switch
            {
                0 => "❌ [错误]",
                1 => "⚡ [断言]",
                2 => "⚠️ [警告]",
                3 => "🟢 [信息]",
                4 => "💥 [异常]",
                _ => "❓ [未知]"
            };
        }

        private static string GetLogMessage(object logEntry, System.Type logEntryType)
        {
            // 尝试多种可能的消息字段名
            string[] messageFields = { "condition", "message", "text", "content" };
            
            foreach (var fieldName in messageFields)
            {
                var field = logEntryType.GetField(fieldName, 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    string value = field.GetValue(logEntry)?.ToString();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            return "[无法获取消息内容]";
        }

        private static string GetLogTypeDetailed(object logEntry, System.Type logEntryType)
        {
            try
            {
                var modeField = logEntryType.GetField("mode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (modeField == null) 
                {
                    // 尝试其他可能的字段名
                    modeField = logEntryType.GetField("type", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                               logEntryType.GetField("logType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                }
                
                if (modeField == null) return "❓ [未知类型-无mode字段]";

                int mode = (int)modeField.GetValue(logEntry);
                
                int logType = mode & 0xF;
                
                
                string typeIcon = logType switch
                {
                    0 => "❌",    // Error
                    1 => "⚡",    // Assert
                    2 => "⚠️",    // Warning
                    3 => "🟢",    // Log
                    4 => "💥",    // Exception
                    _ => "❓"     // 未知
                };
                
                string typeName = logType switch
                {
                    0 => "错误",
                    1 => "断言", 
                    2 => "警告",
                    3 => "信息",
                    4 => "异常",
                    _ => $"未知({logType})"
                };
                
                // 返回详细信息用于调试
                return $"{typeIcon} [{typeName}]";
            }
            catch (System.Exception ex)
            {
                return $"❓ [类型解析失败: {ex.Message}]";
            }
        }

        private static string GetLogLocation(object logEntry, System.Type logEntryType)
        {
            var fileField = logEntryType.GetField("file", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lineField = logEntryType.GetField("line", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            string file = fileField?.GetValue(logEntry)?.ToString() ?? "";
            int line = lineField != null ? (int)(lineField.GetValue(logEntry) ?? 0) : 0;

            if (!string.IsNullOrEmpty(file) && line > 0)
            {
                return $" [{System.IO.Path.GetFileName(file)}:{line}]";
            }
            else if (!string.IsNullOrEmpty(file))
            {
                return $" [{System.IO.Path.GetFileName(file)}]";
            }
            return "";
        }
        [Tool("刷新 MCP 工具列表")]
        public static string RefreshTools()
        {
            MCP4Unity.Editor.MCPFunctionInvoker.Tools.Clear();
            MCP4Unity.Editor.MCPFunctionInvoker.LoadMethods();
            return $"✅ 已刷新，当前工具数量: {MCP4Unity.Editor.MCPFunctionInvoker.Tools.Count}";
        }

        [Tool("运行无参静态函数")]
        public static string RunCode(string methodFullName)
        {
            try
            {
                // 清理方法名，去除可能的括号和分号
                string cleanMethodName = methodFullName?.Trim()
                    .Replace("()", "")
                    .Replace(";", "")
                    .Trim();
                
                if (string.IsNullOrEmpty(cleanMethodName))
                {
                    return "方法名不能为空";
                }
                
                // 解析方法名，可能包含命名空间和类名
                string targetNamespace = null;
                string targetClassName = null;
                string targetMethodName = cleanMethodName;
                
                var parts = cleanMethodName.Split('.');
                if (parts.Length >= 3)
                {
                    // 格式: NameSpace.ClassName.MethodName 或 NameSpace.SubNameSpace.ClassName.MethodName
                    targetMethodName = parts[^1];
                    targetClassName = parts[^2];
                    targetNamespace = string.Join(".", parts.Take(parts.Length - 2));
                }
                else if (parts.Length == 2)
                {
                    // 格式: ClassName.MethodName
                    targetMethodName = parts[1];
                    targetClassName = parts[0];
                }
                
                // 获取当前域中的所有程序集
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // 获取程序集中的所有类型
                        var types = assembly.GetTypes();
                        
                        foreach (var type in types)
                        {
                            // 通过命名空间和类名前缀过滤
                            if (!string.IsNullOrEmpty(targetNamespace) && 
                                !type.Namespace?.Equals(targetNamespace, System.StringComparison.OrdinalIgnoreCase) == true)
                            {
                                continue;
                            }
                            
                            if (!string.IsNullOrEmpty(targetClassName) && 
                                !type.Name.Equals(targetClassName, System.StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            
                            // 查找指定名称的公共静态方法
                            var method = type.GetMethod(targetMethodName, 
                                System.Reflection.BindingFlags.Public | 
                                System.Reflection.BindingFlags.Static);
                            
                            if (method != null && method.GetParameters().Length == 0)
                            {
                                return $"成功调用方法: {type.FullName}.{targetMethodName}:\n{method.Invoke(null, null)}";
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        // 跳过无法访问的程序集
                        UnityEngine.Debug.LogWarning($"无法访问程序集 {assembly.FullName}: {ex.Message}");
                    }
                }
                
                return $"未找到无参静态方法: {cleanMethodName}";
            }
            catch (System.Exception ex)
            {
                return $"执行方法时发生错误: {ex.Message}";
            }
        }
    }
}

