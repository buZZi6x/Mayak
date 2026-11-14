using System;
using System.Linq;
using UnityEngine;

namespace LighthouseGame
{
    // Run only when explicitly added by the verification command; absent from the saved scene.
    public class BlockoutRouteProbe : MonoBehaviour
    {
        public BlockoutPlayer Player;
        public bool Finished, Passed;
        public int Waypoint;
        public string Result;
        Transform[] route;
        float deadline, startHeight;
        void Start()
        {
            Player = FindAnyObjectByType<BlockoutPlayer>(); Player.InputEnabled = false;
            route = FindObjectsByType<Transform>().Where(t => t.name.StartsWith("MARK_Route_")).OrderBy(t => t.name).ToArray();
            foreach (var door in FindObjectsByType<BlockoutInteractable>().Where(s => s.Kind == BlockoutInteractable.Station.Door)) door.Open = true;
            Player.Teleport(route[0].position, route[1].position + Vector3.up * 1.65f); startHeight = Player.transform.position.y; deadline = Time.time + 12; Waypoint = 1;
        }
        void Update()
        {
            if (Finished) return;
            Vector3 delta = route[Waypoint].position - Player.transform.position; delta.y = 0;
            if (delta.magnitude < .17f)
            {
                Waypoint++; deadline = Time.time + 9;
                if (Waypoint >= route.Length) { Finish(Player.Respawns == 0 && Player.transform.position.y > startHeight + 5, "Reached the watch floor through the door and all 30 stairs"); return; }
            }
            if (Time.time > deadline) { Finish(false,"Blocked at " + route[Waypoint].name + "; position=" + Player.transform.position.ToString("F3")); return; }
            delta = route[Waypoint].position - Player.transform.position; delta.y = 0;
            Player.ProbeVelocity = delta.normalized * Mathf.Min(2.4f, delta.magnitude / Mathf.Max(Time.deltaTime, .001f));
        }
        void Finish(bool passed, string message)
        {
            Finished = true; Passed = passed; Result = message; Player.ProbeVelocity = Vector3.zero;
            Debug.Log("BLOCKOUT_ROUTE_PROBE " + JsonUtility.ToJson(new Evidence { Passed=passed, Waypoints=Waypoint, Height=Player.transform.position.y, Respawns=Player.Respawns, Message=message }));
        }
        [Serializable] class Evidence { public bool Passed; public int Waypoints, Respawns; public float Height; public string Message; }
        void OnDestroy() { if (Player) { Player.ProbeVelocity = Vector3.zero; Player.InputEnabled = true; } }
    }
}
