using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class CampaignReleaseBuilder
{
    public const string Output = "Builds/DogLife_Campaign_20261025";
    [MenuItem("DogLife/Build/Windows Campaign (no exhibition features)")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/build-status.txt","Building");
        try
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName=Output+"/DogLife.exe",
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.None,
                extraScriptingDefines=new[]{"DOGLIFE_CAMPAIGN_BUILD"}
            });
            File.WriteAllText(Output+"/build-status.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; warnings="+report.summary.totalWarnings+"; bytes="+report.summary.totalSize+"; duration="+report.summary.totalTime);
            File.WriteAllText(Output+"/build-messages.txt",string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==UnityEngine.LogType.Error||m.type==UnityEngine.LogType.Exception).Select(m=>m.content)));
        }
        catch(Exception e){File.WriteAllText(Output+"/build-status.txt","Failed: "+e);throw;}
    }
}
