using UnityEditor;
using UnityEngine;

public static class LogEntryDebugger
{
    [MenuItem("Tools/Test Log Classification")]
    public static void TestLogClassification()
    {
        Debug.Log("Test Info Log");
        Debug.LogWarning("Test Warning Log");
        Debug.LogError("Test Error Log");
        
        UnityEditor.EditorApplication.delayCall += () =>
        {
            var logEntriesType = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.CoreModule");
            var logEntryType = System.Type.GetType("UnityEditor.LogEntry,UnityEditor.CoreModule");
            var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            var getCountMethod = logEntriesType.GetMethod("GetCount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            
            int logCount = (int)getCountMethod.Invoke(null, null);
            var modeField = logEntryType.GetField("mode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            for (int i = System.Math.Max(0, logCount - 3); i < logCount; i++)
            {
                var logEntry = System.Activator.CreateInstance(logEntryType);
                var parameters = new object[] { i, logEntry };
                bool success = (bool)getEntryInternalMethod.Invoke(null, parameters);
                
                if (success && modeField != null)
                {
                    int mode = (int)modeField.GetValue(logEntry);
                    var conditionField = logEntryType.GetField("condition", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    string msg = conditionField?.GetValue(logEntry)?.ToString() ?? "";
                    
                    UnityEngine.Debug.Log($"[{i}] mode={mode} (0x{mode:X}), msg={msg.Substring(0, System.Math.Min(30, msg.Length))}");
                }
            }
        };
    }
}
