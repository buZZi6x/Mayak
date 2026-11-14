using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LighthouseGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public static class CheckMenuLayout
{
    private static void Resize(int width,int height)
    {
        const BindingFlags f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        var a=typeof(EditorWindow).Assembly;var t=a.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(t).GetProperty("instance",f).GetValue(null);
        var group=t.GetProperty("currentGroup",f).GetValue(sizes);var gt=group.GetType();
        int count=(int)gt.GetMethod("GetTotalCount",f).Invoke(group,null),index=-1;
        for(int i=0;i<count;i++){var s=gt.GetMethod("GetGameViewSize",f).Invoke(group,new object[]{i});var st=s.GetType();if((int)st.GetProperty("width",f).GetValue(s)==width&&(int)st.GetProperty("height",f).GetValue(s)==height){index=i;break;}}
        if(index<0){var s=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),f,null,new object[]{Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),width,height,"Keeper UI "+width+"x"+height},null);gt.GetMethod("AddCustomSize",f).Invoke(group,new[]{s});index=count;}
        var view=EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));view.GetType().GetProperty("selectedSizeIndex",f).SetValue(view,index);view.Repaint();
    }
    public static async Task<object> Main()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        var menu=Object.FindAnyObjectByType<KeeperMenu>();var backdrop=menu.GetComponent<MenuBackdrop>();
        var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/UI/MainMenu/Unity"));
        var results=new List<object>();int failures=0;
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768),new Vector2Int(390,844),new Vector2Int(844,390)})
        {
            Resize(size.x,size.y);await Task.Delay(300);menu.ShowPage("settings");backdrop.Glitches=false;
            foreach(var section in new[]{"audio","video","controls"})
            {
                menu.SelectTab(section);await Task.Delay(250);
                var root=menu.Root;var panel=root.Q("panel-"+section);
                var controls=panel.Query<VisualElement>().ToList().Where(e=>e is OpticalSlider||e is Toggle||e.ClassListContains("choice-field")).ToArray();
                bool inBounds=controls.All(e=>e.worldBound.x>=-1&&e.worldBound.xMax<=root.resolvedStyle.width+1&&e.worldBound.width>25&&e.worldBound.height>=40);
                var actions=new[]{root.Q("reset-settings"),root.Q("apply-settings")};
                bool actionsFit=actions.All(e=>e.worldBound.x>=-1&&e.worldBound.xMax<=root.resolvedStyle.width+1)&&actions[0].worldBound.xMax<=actions[1].worldBound.x;
                bool dimensions=Screen.width==size.x&&Screen.height==size.y;
                bool passed=inBounds&&actionsFit&&dimensions;if(!passed)failures++;
                results.Add(new{width=size.x,height=size.y,section,passed,inBounds,actionsFit,dimensions,controls=controls.Select(e=>new{name=e.name,rect=e.worldBound.ToString()}).ToArray()});
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"settings-"+section+"-"+size.x+"x"+size.y+".png"));await Task.Delay(100);
            }
        }
        Resize(1280,720);await Task.Delay(250);menu.ShowPage("home");backdrop.Glitches=false;await Task.Delay(750);
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"home-final.png"));await Task.Delay(150);
        menu.ShowPage("settings");backdrop.Glitches=menu.AppliedSettings.Glitches;
        var result=new{passed=failures==0,checkedStates=results.Count,failures,states=results};
        File.WriteAllText(Path.Combine(output,"layout.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return new{passed=failures==0,checkedStates=results.Count,failures,path=Path.Combine(output,"layout.json")};
    }
}
