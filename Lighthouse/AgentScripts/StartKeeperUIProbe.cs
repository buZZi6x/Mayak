using UnityEngine;
using UnityEditor;
using LighthouseGame;
public static class StartKeeperUIProbe
{
 public static string Main(){
  var gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
  EditorWindow.GetWindow(gameView).Focus();
  Object.FindAnyObjectByType<BlockoutSession>().gameObject.AddComponent<KeeperUIValidationProbe>();
  return "UI probe started";
 }
}
