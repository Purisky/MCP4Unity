using MCP4Unity;
using UnityEditor;

namespace MCP
{
    public class PlayModeTool
    {
        [Tool("进入或退出 PlayMode")]
        public static string SetPlayMode(
            [Desc("true=进入PlayMode, false=退出PlayMode")] bool enter)
        {
            if (enter && !EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
                return "✅ 正在进入 PlayMode...\n💡 等待 5-10 秒后使用 GetUnityConsoleLog 查看启动日志";
            }
            else if (!enter && EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return "✅ 正在退出 PlayMode...";
            }
            return $"已经处于目标状态 (PlayMode={EditorApplication.isPlaying})";
        }

        [Tool("获取当前 PlayMode 状态")]
        public static string GetPlayModeState()
        {
            return $"isPlaying={EditorApplication.isPlaying}, isPaused={EditorApplication.isPaused}, isCompiling={EditorApplication.isCompiling}";
        }
    }
}
