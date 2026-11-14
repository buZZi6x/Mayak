using UnityEngine;
using UnityEngine.UIElements;
using LighthouseGame;
public static class CheckKeeperEndState
{
 public static object Main(){var s=Object.FindAnyObjectByType<BlockoutSession>();var h=Object.FindAnyObjectByType<BlockoutHUD>();
 if(!s.Player.CameraEquipped)s.Player.ToggleCamera();s.BeginRun();s.EndRun("Проверка завершения");
 bool reason=h.Root.Q<Label>("result-reason").text==s.EndReason;
 bool noFrame=h.Root.Q("viewfinder").style.display.value==DisplayStyle.None;
 s.BeginRun();bool restored=h.Root.Q("viewfinder").style.display.value==DisplayStyle.Flex;
 s.Running=false;s.Player.InputEnabled=false;
 return new{reason,noFrame,restored};}
}
