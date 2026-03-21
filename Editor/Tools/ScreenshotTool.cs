using MCP4Unity;
using UnityEngine;
using UnityEditor;

namespace MCP
{
    public class ScreenshotTool
    {
        [Tool("截取 Game 视图截图并保存为 PNG")]
        public static string Screenshot(
            [Desc("截图保存路径（相对项目根目录），如 'Assets/Screenshots/shot.png'、'TestOutput/screenshot.png'")] string savePath = "TestOutput/screenshot.png",
            [Desc("截图宽度（像素），0 表示使用当前 Game 视图尺寸")] int width = 0,
            [Desc("截图高度（像素），0 表示使用当前 Game 视图尺寸")] int height = 0,
            [Desc("超采样倍数（1-4），用于提高截图质量")] int superSize = 1)
        {
            try
            {
                if (string.IsNullOrEmpty(savePath))
                    savePath = "TestOutput/screenshot.png";

                if (!savePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    savePath += ".png";

                // 确保目录存在
                string dir = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                if (superSize < 1) superSize = 1;
                if (superSize > 4) superSize = 4;

                if (width > 0 && height > 0)
                {
                    // 指定尺寸截图
                    var rt = new RenderTexture(width, height, 24);
                    var cam = Camera.main;
                    if (cam == null)
                    {
                        // 尝试找任意相机
                        cam = Object.FindFirstObjectByType<Camera>();
                    }

                    if (cam != null)
                    {
                        var prevRT = cam.targetTexture;
                        cam.targetTexture = rt;
                        cam.Render();
                        cam.targetTexture = prevRT;

                        RenderTexture.active = rt;
                        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;

                        byte[] bytes = tex.EncodeToPNG();
                        System.IO.File.WriteAllBytes(savePath, bytes);

                        Object.DestroyImmediate(tex);
                        Object.DestroyImmediate(rt);

                        string fullPath = System.IO.Path.GetFullPath(savePath);
                        return $"✅ 截图已保存: {fullPath} ({width}x{height})";
                    }
                    else
                    {
                        Object.DestroyImmediate(rt);
                        // 回退到 ScreenCapture
                    }
                }

                // 使用 ScreenCapture（Game 视图截图）
                ScreenCapture.CaptureScreenshot(savePath, superSize);
                string absPath = System.IO.Path.GetFullPath(savePath);
                return $"✅ 截图已保存: {absPath} (superSize: {superSize}x)\n注意: ScreenCapture 需要在 PlayMode 下才能截取 Game 视图，否则可能为空白";
            }
            catch (System.Exception ex)
            {
                return $"❌ 截图失败: {ex.Message}";
            }
        }

        [Tool("截取 Scene 视图截图并保存为 PNG")]
        public static string SceneScreenshot(
            [Desc("截图保存路径，如 'TestOutput/scene_shot.png'")] string savePath = "TestOutput/scene_screenshot.png",
            [Desc("截图宽度（像素）")] int width = 1920,
            [Desc("截图高度（像素）")] int height = 1080)
        {
            try
            {
                if (string.IsNullOrEmpty(savePath))
                    savePath = "TestOutput/scene_screenshot.png";

                if (!savePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    savePath += ".png";

                string dir = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null)
                    return "❌ 没有活跃的 Scene 视图";

                var cam = sceneView.camera;
                if (cam == null)
                    return "❌ Scene 视图相机不可用";

                var rt = new RenderTexture(width, height, 24);
                var prevRT = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prevRT;

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                byte[] bytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(savePath, bytes);

                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);

                string fullPath = System.IO.Path.GetFullPath(savePath);
                return $"✅ Scene 视图截图已保存: {fullPath} ({width}x{height})";
            }
            catch (System.Exception ex)
            {
                return $"❌ Scene 视图截图失败: {ex.Message}";
            }
        }
    }
}
