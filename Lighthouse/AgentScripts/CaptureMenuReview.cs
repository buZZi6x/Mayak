using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using LighthouseGame;
using UnityEngine;

public static class CaptureMenuReview
{
    public static async Task<object> Main()
    {
        var menu=Object.FindAnyObjectByType<KeeperMenu>();
        var backdrop=menu.GetComponent<MenuBackdrop>();
        var archive=menu.Archive;
        var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/UI/MainMenu/Unity"));
        var report=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(root,"runtime.json")));
        var field=typeof(KeeperMenu).GetField("_archive",BindingFlags.Instance|BindingFlags.NonPublic);
        string suffix=Screen.width+"x"+Screen.height;
        try
        {
            menu.ShowPage("home");backdrop.Glitches=false;await Task.Delay(800);
            ScreenCapture.CaptureScreenshot(Path.Combine(root,"review-home-"+suffix+".png"));await Task.Delay(150);
            menu.ShowPage("gallery");backdrop.Glitches=false;await Task.Delay(200);
            ScreenCapture.CaptureScreenshot(Path.Combine(root,"review-archive-empty-"+suffix+".png"));await Task.Delay(150);
            field.SetValue(menu,new PhotoArchive(Path.Combine((string)report["DataDirectory"],"photos"),0));
            menu.ShowPage("gallery");backdrop.Glitches=false;await Task.Delay(250);
            ScreenCapture.CaptureScreenshot(Path.Combine(root,"review-archive-"+suffix+".png"));await Task.Delay(150);
            typeof(KeeperMenu).GetMethod("OpenPhoto",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{0});
            await Task.Delay(200);ScreenCapture.CaptureScreenshot(Path.Combine(root,"review-photo-"+suffix+".png"));await Task.Delay(150);
        }
        finally
        {
            field.SetValue(menu,archive);menu.ShowPage("settings");backdrop.Glitches=false;
        }
        await Task.Delay(250);ScreenCapture.CaptureScreenshot(Path.Combine(root,"review-settings-"+suffix+".png"));await Task.Delay(150);
        backdrop.Glitches=menu.AppliedSettings.Glitches;
        return new{suffix,root,usedRealTestCaptures=true,originalArchiveRestored=menu.Archive==archive};
    }
}
