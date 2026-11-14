using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using LighthouseGame;
public static class ReviewKeeperLight
{
 public static async Task<string> Main(){
 var s=Object.FindAnyObjectByType<BlockoutSession>();var p=s.Player;
 p.InputEnabled=false; p.CloseInspection(); if(p.UIHidden)p.ToggleUI();if(p.CameraEquipped)p.ToggleCamera();
 var door=Object.FindObjectsByType<BlockoutInteractable>().First(x=>x.Kind==BlockoutInteractable.Station.Door);door.Open=false;
 await Task.Delay(1200); var center=door.GetComponentInChildren<Collider>().bounds.center;
 p.Teleport(center+door.transform.forward*2-Vector3.up*1.2f,center);
 if(p.FlashlightOn)p.ToggleFlashlight();s.MessageUntil=0;
 await Task.Delay(900);ScreenCapture.CaptureScreenshot("../Artifacts/UnityUI/flashlight-off.png");
 await Task.Delay(500);p.ToggleFlashlight();await Task.Delay(900);ScreenCapture.CaptureScreenshot("../Artifacts/UnityUI/flashlight-on.png");
 return "Night door before/after flashlight captured";
 }
}
