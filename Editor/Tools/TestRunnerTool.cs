using MCP4Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MCP
{
    [InitializeOnLoad]
    public class TestRunnerTool
    {
        private static readonly string ResultFilePath = Path.Combine(Application.dataPath, "..", "TestOutput", "mcp_test_results.txt");
        private static readonly string StatusFilePath = Path.Combine(Application.dataPath, "..", "TestOutput", "mcp_test_status.txt");

        static TestRunnerTool()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var listener = new PersistentTestListener(ResultFilePath, StatusFilePath);
            api.RegisterCallbacks(listener);
        }

        [Tool("启动 Editor 内测试（异步，用 gettestresults 查询结果）")]
        public static string RunTestsInEditor(
            [Desc("测试模式: EditMode 或 PlayMode")] string testMode = "PlayMode",
            [Desc("测试过滤器（类名或方法名），为空则运行全部")] string testFilter = "")
        {
            if (string.IsNullOrEmpty(testMode)) testMode = "PlayMode";
            if (testMode != "EditMode" && testMode != "PlayMode")
                return $"❌ 无效的 testMode: {testMode}，必须是 EditMode 或 PlayMode";

            if (File.Exists(StatusFilePath))
            {
                string s = File.ReadAllText(StatusFilePath).Trim();
                if (s == "running")
                    return "⚠️ 测试正在运行中，请用 gettestresults 查询进度";
            }

            // 清理旧结果
            var dir = Path.GetDirectoryName(ResultFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(ResultFilePath)) File.Delete(ResultFilePath);
            File.WriteAllText(StatusFilePath, "running");

            SessionState.SetString("MCP_TestMode", testMode);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = testMode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode,
            };

            if (!string.IsNullOrEmpty(testFilter))
            {
                filter.testNames = new[] { testFilter };
            }

            api.Execute(new ExecutionSettings(filter));

            return $"✅ {testMode} 测试已启动。PlayMode 测试会触发 domain reload，请等待 15-30 秒后用 gettestresults 查询结果";
        }

        [Tool("查询 Editor 内测试运行结果")]
        public static string GetTestResults()
        {
            if (!File.Exists(StatusFilePath))
                return "❌ 没有测试记录，请先调用 runtestsineditor";

            string status = File.ReadAllText(StatusFilePath).Trim();

            if (status == "running")
            {
                if (File.Exists(ResultFilePath))
                {
                    string partial = File.ReadAllText(ResultFilePath);
                    return $"⏳ 测试运行中...\n{partial}";
                }
                return "⏳ 测试运行中，尚无结果（PlayMode 可能正在 domain reload）...";
            }

            if (status == "done" && File.Exists(ResultFilePath))
            {
                return File.ReadAllText(ResultFilePath);
            }

            return $"❓ 未知状态: {status}";
        }

        private class PersistentTestListener : ICallbacks
        {
            private readonly string _resultPath;
            private readonly string _statusPath;
            private int _passCount;
            private int _failCount;
            private int _skipCount;
            private float _totalDuration;
            private int _totalTests;
            private DateTime _startTime;
            private readonly List<string> _failures = new List<string>();

            public PersistentTestListener(string resultPath, string statusPath)
            {
                _resultPath = resultPath;
                _statusPath = statusPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                _passCount = 0;
                _failCount = 0;
                _skipCount = 0;
                _totalDuration = 0;
                _totalTests = CountLeafTests(testsToRun);
                _startTime = DateTime.Now;
                _failures.Clear();

                var dir = Path.GetDirectoryName(_statusPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_statusPath, "running");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string testMode = SessionState.GetString("MCP_TestMode", "Unknown");
                string summary = BuildSummary(testMode, false);

                var dir = Path.GetDirectoryName(_resultPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_resultPath, summary);
                File.WriteAllText(_statusPath, "done");
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren)
                    return;

                _totalDuration += (float)result.Duration;

                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        _passCount++;
                        break;
                    case TestStatus.Failed:
                        _failCount++;
                        string msg = result.Message ?? "";
                        if (msg.Length > 200) msg = msg.Substring(0, 200) + "...";
                        _failures.Add($"{result.Test.Name}\n   {msg}");
                        break;
                    case TestStatus.Skipped:
                        _skipCount++;
                        break;
                }

                // 每完成一个测试，更新中间进度文件
                WriteProgress();
            }

            private void WriteProgress()
            {
                try
                {
                    string testMode = SessionState.GetString("MCP_TestMode", "Unknown");
                    string progress = BuildSummary(testMode, true);
                    var dir = Path.GetDirectoryName(_resultPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(_resultPath, progress);
                }
                catch
                {
                    // 忽略写入错误
                }
            }

            private string BuildSummary(string testMode, bool isProgress)
            {
                var sb = new StringBuilder();
                int done = _passCount + _failCount + _skipCount;
                bool allPassed = _failCount == 0;

                if (isProgress)
                {
                    sb.AppendLine($"⏳ 进度: {done}/{_totalTests}（✅{_passCount} ❌{_failCount} ⏭{_skipCount}）");

                    if (done > 0)
                    {
                        double elapsed = (DateTime.Now - _startTime).TotalSeconds;
                        double perTest = elapsed / done;
                        int remaining = _totalTests - done;
                        double eta = perTest * remaining;
                        sb.AppendLine($"  - 已用时: {elapsed:F1}s，预估剩余: {eta:F1}s");
                    }
                }
                else
                {
                    sb.AppendLine(allPassed ? "✅ 测试全部通过" : "❌ 测试失败");
                    sb.AppendLine();
                    sb.AppendLine("📊 统计:");
                    sb.AppendLine($"  - 模式: {testMode}");
                    sb.AppendLine($"  - Total: {done}");
                    sb.AppendLine($"  - Passed: {_passCount}");
                    sb.AppendLine($"  - Failed: {_failCount}");
                    sb.AppendLine($"  - Skipped: {_skipCount}");
                    sb.AppendLine($"  - Duration: {_totalDuration:F3}s");
                }

                if (_failures.Count > 0)
                {
                    sb.AppendLine();
                    int showCount = Math.Min(_failures.Count, 20);
                    sb.AppendLine($"🔴 失败测试 (显示前 {showCount} 条):");
                    sb.AppendLine();
                    for (int i = 0; i < showCount; i++)
                    {
                        sb.AppendLine($"{i + 1}. {_failures[i]}");
                    }
                    if (_failures.Count > 20)
                    {
                        sb.AppendLine($"\n⚠️  还有 {_failures.Count - 20} 条失败测试未显示");
                    }
                }

                return sb.ToString();
            }

            private static int CountLeafTests(ITestAdaptor test)
            {
                if (!test.HasChildren)
                    return 1;
                int count = 0;
                foreach (var child in test.Children)
                    count += CountLeafTests(child);
                return count;
            }
        }
    }
}
