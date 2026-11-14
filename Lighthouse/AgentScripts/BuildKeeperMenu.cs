using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildKeeperMenu
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
        const string scene="Assets/Scenes/LighthouseBlockout.unity";
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes=new[]{LighthouseGame.KeeperMenu.ScenePath,scene},locationPathName="Builds/MainMenu/Windows/Lighthouse.exe",
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
        });
        var summary=report.summary;
        var result=new
        {
            result=summary.result.ToString(),errors=summary.totalErrors,warnings=summary.totalWarnings,
            bytes=summary.totalSize,seconds=summary.totalTime.TotalSeconds,path=summary.outputPath,scenes=new[]{LighthouseGame.KeeperMenu.ScenePath,scene},
            messages=report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Warning).Select(m=>m.content).ToArray()
        };
        string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/UI/MainMenu/Unity/build.json"));
        File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        if(summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build failed; see "+path);
        return result;
    }
}
