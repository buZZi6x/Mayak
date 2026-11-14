using UnityEngine;
using UnityEngine.UIElements;
using LighthouseGame;
public static class InspectKeeperUI
{
 public static object Main(){
 var s=Object.FindAnyObjectByType<BlockoutSession>(); var h=Object.FindAnyObjectByType<BlockoutHUD>();
 ScreenCapture.CaptureScreenshot("../Artifacts/UnityUI/first-runtime.png");
 return new {screen=new{Screen.width,Screen.height}, hud=h!=null, root=h.Root.worldBound.ToString(), surface=h.Root.Q("surface").worldBound.ToString(), slots=h.Root.Q("slots").worldBound.ToString(), camera=s.Player.CameraEquipped, torch=s.Player.Flashlight!=null,playing=Application.isPlaying};
 }
}
