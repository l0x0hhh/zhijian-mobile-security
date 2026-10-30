// 说明：从已启用的场景构建 Android APK，失败时向命令行返回错误，不更改签名或版本设置。
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Zhijian.Editor
{
    public static class AndroidBuild
    {
        public static void Build()
        {
            var output = Environment.GetEnvironmentVariable("ZHIJIAN_APK_PATH");
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output) || !output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ZHIJIAN_APK_PATH 必须是 APK 的绝对路径。");
            output = Path.GetFullPath(output);
            if (File.Exists(output)) throw new IOException("输出文件已存在，为保护已有 APK 拒绝覆盖：" + output);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || !scenes.Contains("Assets/_Project/Scenes/Main.scene"))
                throw new InvalidOperationException("Build Settings 未启用 Main.scene。");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("请以 -buildTarget Android 启动编辑器。");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.Android, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output))
                throw new InvalidOperationException("Android 构建未成功：" + report.summary.result + "；错误数 " + report.summary.totalErrors);
            UnityEngine.Debug.Log("Android APK 已构建：" + output + "，大小：" + report.summary.totalSize);
        }
    }
}
