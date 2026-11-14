using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using LighthouseGame;
public static class CaptureKeeperLayout
{
 public static object Main(string mode){
 var s=UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();var h=UnityEngine.Object.FindAnyObjectByType<BlockoutHUD>();
 if(mode=="tooltip"){var slot=h.Root.Q("slot-1");using(var e=PointerEnterEvent.GetPooled()){e.target=slot;slot.SendEvent(e);}}
 string name=(mode=="tooltip"?"tooltip-":"layout-")+Screen.width+"x"+Screen.height;
 ScreenCapture.CaptureScreenshot("../Artifacts/UnityUI/"+name+".png");
 var items=new[]{"stats","equipment","slots","clean-button","held","tooltip"}.Select(n=>new{name=n,bounds=h.Root.Q(n).worldBound.ToString(),display=h.Root.Q(n).resolvedStyle.display.ToString()}).ToArray();
 bool fits=new[]{"stats","equipment","slots","clean-button","held"}.All(n=>{var b=h.Root.Q(n).worldBound;return b.x>=0&&b.y>=0&&b.xMax<=Screen.width+.1f&&b.yMax<=Screen.height+.1f;});
 bool separated=!h.Root.Q("slots").worldBound.Overlaps(h.Root.Q("equipment").worldBound)&&!h.Root.Q("slots").worldBound.Overlaps(h.Root.Q("clean-button").worldBound);
 return new {name,fits,separated,items};
 }
}
