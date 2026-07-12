#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWebGL
{
    public static void Build()
    {
        string outputPath = "Builds/WebGL";
        Directory.CreateDirectory(outputPath);

        var options = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Scenes/LobbyScene.unity",
                "Assets/Scenes/GameScene.unity"
            },
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"WebGL build failed: {report.summary.result}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"WebGL build succeeded: {outputPath}");
    }
}
#endif
