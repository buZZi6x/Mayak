using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LighthouseGame;
using UnityEngine;

public static class MenuRuleChecks
{
    [Serializable] private sealed class Report { public bool Passed; public int PassedCount, FailedCount; public List<string> Checks = new List<string>(); }
    public static string Run()
    {
        var report = new Report();
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/UI/MainMenu/Unity"));
        string data = Path.Combine(root, "RuleData", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(data);
        string path = Path.Combine(data,"settings.json");
        var settings = new MenuSettings { Master = 31, Ambience = 0, Effects = 88, Sensitivity = 2.4f, InvertY = true, VSync = false, FrameLimit = 144 };
        Check(report,settings.Save(path,out _),"Settings save succeeds");
        var loaded = MenuSettings.Load(path,out _);
        Check(report,settings.SameAs(loaded),"Settings survive complete JSON reload");
        var draft=loaded.Clone();draft.Master=100;
        Check(report,loaded.Master==31,"Editing a draft does not mutate saved settings");
        draft.Master=999;draft.Ambience=-3;draft.Brightness=500;draft.Sensitivity=float.NaN;draft.Display=99;draft.Width=2;draft.Height=3;draft.FrameLimit=13;draft.Normalize();
        Check(report,draft.Master==100&&draft.Ambience==0&&draft.Brightness==130&&draft.Sensitivity==1&&draft.Display==2&&draft.Width==1920&&draft.FrameLimit==60,"Invalid values normalize without NaN or unknown display modes");
        File.WriteAllText(path,"{broken");
        loaded=MenuSettings.Load(path,out string loadError);
        Check(report,loaded.Master==80&&loadError!=null&&File.ReadAllText(path)=="{broken","Corrupt settings use defaults and preserve the source");
        string blocked=Path.Combine(data,"not-a-directory");File.WriteAllText(blocked,"owned test marker");
        Check(report,!settings.Save(Path.Combine(blocked,"settings.json"),out string writeError)&&writeError!=null,"Storage failure is reported without throwing");
        var list=new List<ArchivedPhoto>();
        for(int i=0;i<25;i++)list.Add(new ArchivedPhoto{Id=Guid.NewGuid().ToString("N"),Price=i+1,Captured=i,Label="Кадр "+i});
        list.Add(new ArchivedPhoto{Id="../../escape",Price=99999});list.Add(new ArchivedPhoto{Id=Guid.NewGuid().ToString("N"),Price=0});list.Add(list[0]);
        var ordered=PhotoArchive.Ordered(list);
        Check(report,ordered.Count==20&&ordered[0].Price==25&&ordered[19].Price==6,"Archive rejects unsafe IDs, duplicates and zero prices; retains the top twenty");
        var earlier=new ArchivedPhoto{Id=Guid.NewGuid().ToString("N"),Price=50,Captured=1};var later=new ArchivedPhoto{Id=Guid.NewGuid().ToString("N"),Price=50,Captured=2};
        Check(report,PhotoArchive.Ordered(new[]{later,earlier})[0]==earlier,"Equal-price ordering is stable by capture time");
        var archive=new PhotoArchive(Path.Combine(data,"photos"),430);
        var texture=new Texture2D(8,8,TextureFormat.RGB24,false);texture.SetPixels(Enumerable.Repeat(new Color(.1f,.3f,.5f),64).ToArray());texture.Apply();
        try
        {
            var shot=new BlockoutShot{Label="Проверка архива",Price=120,Image=texture,Subjects=new List<PhotoSubject>{new PhotoSubject{Label="Корабль",Key="ship",Price=120}}};
            Check(report,!archive.Record(shot,9999,false)&&archive.Data.Photos.Count==0&&archive.Data.Best==430,"Free preview cannot populate records");
            Check(report,archive.Record(shot,480,true),"A completed game capture persists its own image");
            var reopened=new PhotoArchive(Path.Combine(data,"photos"),1);
            Check(report,reopened.Data.Photos.Count==1&&reopened.Data.Photos[0].Price==120&&reopened.Data.Best==480,"Archive and best result survive restart");
            var image=reopened.LoadImage(reopened.Data.Photos[0]);
            Check(report,image&&image.width==8&&image.height==8,"Stored image decodes independently of inventory texture");
            if(image)UnityEngine.Object.DestroyImmediate(image);
            shot.Price=0;Check(report,archive.Record(shot,500,true)&&archive.Data.Photos.Count==1&&archive.Data.Best==500,"Zero-value capture updates best result without adding a photo");
            for(int i=1;i<=22;i++){shot.Price=i*100;archive.Record(shot,500+i*100,true);}
            Check(report,archive.Data.Photos.Count==20&&archive.Data.Photos[0].Price==2200,"Repeated runs retain twenty most profitable captures");
            Check(report,Directory.GetFiles(Path.Combine(data,"photos"),"*.jpg").Length<=21,"Pruning retains current and recoverable previous manifest images only");
            string manifest=Path.Combine(data,"photos","archive.json");File.WriteAllText(manifest,"{broken");
            var recovered=new PhotoArchive(Path.Combine(data,"photos"),3000);
            Check(report,recovered.Data.Photos.Count>0&&recovered.Data.Best==3000&&Directory.GetFiles(Path.GetDirectoryName(manifest),"*.corrupt-*").Length==1,"Corrupt archive preserves evidence, restores backup and keeps legacy record");
            File.Delete(Path.Combine(data,"photos",recovered.Data.Photos[0].Id+".jpg"));
            Check(report,recovered.LoadImage(recovered.Data.Photos[0])==null&&recovered.Data.Best==3000,"Missing image does not discard the record");
            string future=Path.Combine(data,"future");Directory.CreateDirectory(future);string futureText="{\"Version\":99,\"Best\":9000,\"Photos\":[]}";File.WriteAllText(Path.Combine(future,"archive.json"),futureText);
            var futureArchive=new PhotoArchive(future,500);futureArchive.RecordBest(10000);
            Check(report,File.ReadAllText(Path.Combine(future,"archive.json"))==futureText,"Future archive version is never overwritten automatically");
        }
        finally {UnityEngine.Object.DestroyImmediate(texture);}
        report.Passed=report.FailedCount==0;string resultPath=Path.Combine(root,"rules.json");File.WriteAllText(resultPath,JsonUtility.ToJson(report,true));
        if(!report.Passed)throw new InvalidOperationException("Menu rules failed: "+resultPath);
        return report.PassedCount+" checks passed; "+resultPath;
    }
    private static void Check(Report report,bool passed,string label){report.Checks.Add((passed?"PASS ":"FAIL ")+label);if(passed)report.PassedCount++;else report.FailedCount++;}
}
