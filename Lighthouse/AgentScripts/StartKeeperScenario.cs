using UnityEngine;
using LighthouseGame;
public static class StartKeeperScenario { public static string Main(){Object.FindAnyObjectByType<BlockoutSession>().gameObject.AddComponent<BlockoutScenarioProbe>();return "Existing gameplay regression started";} }
