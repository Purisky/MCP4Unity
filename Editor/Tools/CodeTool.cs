using MCP4Unity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.Build;

namespace MCP
{
    public class CodeTools
    {
        [Tool("重新编译程序集，等待编译完成后返回编译结果和错误信息")]
        public static async Task<string> RecompileAssemblies()
        {
            // 先刷新 AssetDatabase，确保外部新增/修改的文件被 Unity 识别
            AssetDatabase.Refresh();
            
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
            [Desc("日志类型筛选: all(全部), error(仅错误), warning(仅警告), info(仅信息)")] string filter = "all",
            [Desc("是否折叠重复日志")] bool collapse = false,
            [Desc("显示最近N条日志(默认20)")] int count = 20)
        {
            try
            {
                if (string.IsNullOrEmpty(filter)) filter = "all";
                if (count <= 0) count = 20;
                var logInfo = GetLogEntriesInfo(filter, collapse, count);
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
                    return "❌ 无法访问Unity日志系统";

                var logEntryType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntry");
                if (logEntryType == null)
                    return "❌ 无法访问日志条目类型";

                var getCountMethod = logEntriesType.GetMethod("GetCount", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var startMethod = logEntriesType.GetMethod("StartGettingEntries", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var endMethod = logEntriesType.GetMethod("EndGettingEntries", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                
                if (getCountMethod == null || getEntryInternalMethod == null)
                    return "❌ 无法访问日志方法";

                int logCount = (int)getCountMethod.Invoke(null, null);
                if (logCount == 0)
                    return "📭 控制台暂无日志";

                // 必须调用 StartGettingEntries 才能读取日志内容
                if (startMethod != null) startMethod.Invoke(null, null);

                try
                {
                    var modeField = logEntryType.GetField("mode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var msgField = logEntryType.GetField("message", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var fileField = logEntryType.GetField("file", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var lineField = logEntryType.GetField("line", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    var logs = new System.Collections.Generic.List<(int type, string message, string location)>();
                    var seenMessages = new System.Collections.Generic.HashSet<string>();
                    int errorCount = 0, warningCount = 0, infoCount = 0;

                    for (int i = 0; i < logCount; i++)
                    {
                        try
                        {
                            var logEntry = System.Activator.CreateInstance(logEntryType);
                            bool success = (bool)getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });
                            if (!success) continue;

                            int mode = modeField != null ? (int)modeField.GetValue(logEntry) : 0;
                            int logType;
                            if ((mode & 0x100) != 0) logType = 0;      // Error
                            else if ((mode & 0x200) != 0) logType = 2;  // Warning
                            else logType = 3;                            // Info

                            // 统计
                            if (logType == 0) errorCount++;
                            else if (logType == 2) warningCount++;
                            else infoCount++;

                            // 筛选
                            if (filter == "error" && logType != 0) continue;
                            if (filter == "warning" && logType != 2) continue;
                            if (filter == "info" && logType != 3) continue;

                            // 读取消息（只取第一行，去掉堆栈）
                            string fullMsg = msgField?.GetValue(logEntry)?.ToString() ?? "";
                            string message = fullMsg;
                            int newlineIdx = fullMsg.IndexOf('\n');
                            if (newlineIdx > 0) message = fullMsg.Substring(0, newlineIdx);

                            if (collapse && !seenMessages.Add(message)) continue;

                            // 位置
                            string file = fileField?.GetValue(logEntry)?.ToString() ?? "";
                            int line = lineField != null ? (int)(lineField.GetValue(logEntry) ?? 0) : 0;
                            string location = "";
                            if (!string.IsNullOrEmpty(file) && line > 0)
                                location = $" [{System.IO.Path.GetFileName(file)}:{line}]";
                            else if (!string.IsNullOrEmpty(file))
                                location = $" [{System.IO.Path.GetFileName(file)}]";

                            logs.Add((logType, message, location));
                        }
                        catch { continue; }
                    }

                    result.AppendLine($"📊 日志统计: ❌错误 {errorCount} | ⚠️警告 {warningCount} | 🟢信息 {infoCount}");

                    if (logs.Count == 0)
                        return result.ToString() + $"\n📭 无匹配日志 (筛选: {filter})";

                    int startIndex = System.Math.Max(0, logs.Count - maxCount);
                    int displayCount = logs.Count - startIndex;
                    result.AppendLine($"匹配 {logs.Count} 条 (筛选: {filter}, 折叠: {collapse})\n");

                    for (int i = startIndex; i < logs.Count; i++)
                    {
                        var (type, message, location) = logs[i];
                        string icon = GetLogTypeIcon(type);
                        result.AppendLine($"{icon}{location}: {message}");
                    }

                    result.AppendLine($"\n显示了最近 {displayCount} 条日志");
                    return result.ToString();
                }
                finally
                {
                    if (endMethod != null) endMethod.Invoke(null, null);
                }
            }
            catch (System.Exception ex)
            {
                return $"❌ LogEntries访问失败: {ex.Message}";
            }
        }
    

        private static int GetLogTypeValue(object logEntry, System.Type logEntryType, System.Type logEntriesType)
        {
            var modeField = logEntryType.GetField("mode", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (modeField != null)
            {
                int mode = (int)modeField.GetValue(logEntry);
                // Unity 6 mode flags: 0x100=Error, 0x200=Warning, 0x400=Log
                if ((mode & 0x100) != 0) return 0; // Error
                if ((mode & 0x200) != 0) return 2; // Warning
                if ((mode & 0x400) != 0) return 3; // Log/Info
            }
            return 3; // fallback to info
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
            // Unity 6 使用 "message" 字段
            string[] messageFields = { "message", "condition", "text", "content" };
            
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
        [Tool("生成测试日志（用于验证日志分类）")]
        public static string EmitTestLogs()
        {
            UnityEngine.Debug.Log("🧪 测试信息日志 (Info)");
            UnityEngine.Debug.LogWarning("🧪 测试警告日志 (Warning)");
            UnityEngine.Debug.LogError("🧪 测试错误日志 (Error)");
            return "✅ 已生成 3 条测试日志 (Info/Warning/Error)，使用 GetUnityConsoleLog 查看";
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

        [Tool("执行 Unity 编辑器菜单命令")]
        public static string ExecuteMenuItem(
            [Desc("菜单路径，如 'File/Save Project'、'Assets/Refresh'、'Window/General/Console'")] string menuPath)
        {
            try
            {
                bool success = EditorApplication.ExecuteMenuItem(menuPath);
                if (success)
                    return $"✅ 已执行: {menuPath}";
                else
                    return $"❌ 菜单项不存在或无法执行: {menuPath}";
            }
            catch (System.Exception ex)
            {
                return $"❌ 执行菜单命令时发生错误: {ex.Message}";
            }
        }

        [Tool("获取当前平台的脚本宏定义（Scripting Define Symbols）")]
        public static string GetDefines()
        {
            try
            {
                var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
                string defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);

                if (string.IsNullOrEmpty(defines))
                    return "当前无自定义宏定义";

                var result = new System.Text.StringBuilder();
                result.AppendLine($"平台: {buildTargetGroup}");
                result.AppendLine("宏定义:");

                var symbolList = defines.Split(';');
                foreach (var symbol in symbolList)
                {
                    if (!string.IsNullOrWhiteSpace(symbol))
                        result.AppendLine($"  {symbol.Trim()}");
                }

                return result.ToString();
            }
            catch (System.Exception ex)
            {
                return $"❌ 获取宏定义时发生错误: {ex.Message}";
            }
        }

        [Tool("设置当前平台的脚本宏定义（Scripting Define Symbols）")]
        public static string SetDefines(
            [Desc("宏定义列表，分号分隔，如 'ENABLE_DEBUG;USE_ADDRESSABLES;MY_FEATURE'")] string defines,
            [Desc("操作模式: set(替换全部), add(追加), remove(移除指定项)")] string mode = "set")
        {
            try
            {
                var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
                
                string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
                var currentList = string.IsNullOrEmpty(currentDefines) 
                    ? new List<string>() 
                    : currentDefines.Split(';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

                var newList = defines.Split(';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                var resultList = new List<string>();

                if (mode == "set")
                {
                    resultList = newList;
                }
                else if (mode == "add")
                {
                    resultList = currentList;
                    foreach (var symbol in newList)
                    {
                        if (!resultList.Contains(symbol))
                            resultList.Add(symbol);
                    }
                }
                else if (mode == "remove")
                {
                    resultList = currentList;
                    foreach (var symbol in newList)
                    {
                        resultList.Remove(symbol);
                    }
                }
                else
                {
                    return $"❌ 未知的操作模式: {mode}，支持: set, add, remove";
                }

                string newDefinesStr = string.Join(";", resultList);
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newDefinesStr);

                var result = new System.Text.StringBuilder();
                result.AppendLine($"✅ 宏定义已更新 (模式: {mode})");
                result.AppendLine($"\n操作前:");
                if (currentList.Count == 0)
                    result.AppendLine("  (无)");
                else
                    foreach (var symbol in currentList)
                        result.AppendLine($"  {symbol}");

                result.AppendLine($"\n操作后:");
                if (resultList.Count == 0)
                    result.AppendLine("  (无)");
                else
                    foreach (var symbol in resultList)
                        result.AppendLine($"  {symbol}");

                return result.ToString();
            }
            catch (System.Exception ex)
            {
                return $"❌ 设置宏定义时发生错误: {ex.Message}";
            }
        }
    }
}

