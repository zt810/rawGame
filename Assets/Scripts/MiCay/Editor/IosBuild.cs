using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MiCay.EditorTools
{
    public static class IosBuild
    {
        const string DefaultPath = "build/iOS/MiCay";

        public static void Build()
        {
            string number = Arg("-iosBuildNumber");
            if (!string.IsNullOrEmpty(number)) PlayerSettings.iOS.buildNumber = number;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.iOS, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            EditorUserBuildSettings.development = false;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("Build Settings chưa bật scene nào.");
                return;
            }

            string path = Arg("-customBuildPath") ?? DefaultPath;
            var summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None
            }).summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Xuất project Xcode thất bại ({summary.result}, {summary.totalErrors} lỗi).");
                return;
            }
            Debug.Log($"[MiCay] Đã xuất project Xcode iOS vào {path} (build {PlayerSettings.iOS.buildNumber}).");
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static void Fail(string message)
        {
            Debug.LogError("[MiCay] " + message);
            EditorApplication.Exit(1);
        }
    }
}
