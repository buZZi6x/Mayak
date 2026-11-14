using System;
using System.Collections.Generic;
using UnityEngine;

namespace LighthouseGame
{
    public partial class BlockoutPlayer
    {
        private const float BoundaryStep = .15f;
        private const float EdgeClearance = .05f;
        private const int FootprintSamples = 16;
        private Vector2[] _islandOutline = Array.Empty<Vector2>();
        private Vector2[] _pierOutline = Array.Empty<Vector2>();

        private void InitializeIslandBoundary()
        {
            var islandPoints = new List<Vector2>();
            var pierPoints = new List<Vector2>();
            // Use the existing collision surfaces, including the pier added after the
            // original scene builder. Resolve them once; no extra scene setup is needed.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var surface in root.GetComponentsInChildren<Collider>())
            {
                if (surface is MeshCollider terrace &&
                    (surface.name == "COLL_Terrace" || surface.name == "SOLID_Terrace"))
                {
                    foreach (var vertex in terrace.sharedMesh.vertices)
                    {
                        var point = terrace.transform.TransformPoint(vertex);
                        islandPoints.Add(new Vector2(point.x, point.z));
                    }
                }
                else if (surface is BoxCollider board &&
                         surface.name.StartsWith("Pier · weathered board ", StringComparison.Ordinal))
                {
                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = board.center + Vector3.Scale(board.size * .5f, new Vector3(x, y, z));
                        var point = board.transform.TransformPoint(corner);
                        pierPoints.Add(new Vector2(point.x, point.z));
                    }
                }
            }
            _islandOutline = WalkableHull(islandPoints);
            // One outline bridges the small seams between the individual planks.
            _pierOutline = WalkableHull(pierPoints);
            if (_islandOutline.Length < 3)
                Debug.LogError("Island movement boundary requires COLL_Terrace or SOLID_Terrace in the player scene.", this);
        }

        private void MoveOnIsland(Vector3 displacement)
        {
            // Short steps prevent a long frame or diagonal movement from cutting
            // across the water at the concave junction of the island and the pier.
            int steps = Mathf.Max(1, Mathf.CeilToInt(new Vector2(displacement.x, displacement.z).magnitude / BoundaryStep));
            var step = displacement / steps;
            for (int i = 0; i < steps; i++)
            {
                var before = transform.position;
                var allowed = step;
                if (!CanStandOnIsland(before + allowed))
                {
                    // Slide along the boundary while retaining vertical jump/gravity motion.
                    var alongX = new Vector3(step.x, step.y, 0);
                    var alongZ = new Vector3(0, step.y, step.z);
                    bool canX = CanStandOnIsland(before + alongX);
                    bool canZ = CanStandOnIsland(before + alongZ);
                    allowed = canX && (!canZ || Mathf.Abs(step.x) >= Mathf.Abs(step.z)) ? alongX :
                        canZ ? alongZ : new Vector3(0, step.y, 0);
                }
                motor.Move(allowed);
                // CharacterController can slide sideways on a collider. Validate its
                // actual result too, so a railing cannot push the keeper over the edge.
                if (CanStandOnIsland(before) && !CanStandOnIsland(transform.position))
                {
                    motor.enabled = false;
                    transform.position = before;
                    motor.enabled = true;
                }
            }
        }

        private bool CanStandOnIsland(Vector3 position)
        {
            var center = new Vector2(position.x, position.z);
            if (!ContainsWalkablePoint(center)) return false;
            float clearance = motor.radius + motor.skinWidth + EdgeClearance;
            for (int i = 0; i < FootprintSamples; i++)
            {
                float angle = i * (Mathf.PI * 2 / FootprintSamples);
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * clearance;
                if (!ContainsWalkablePoint(center + offset)) return false;
            }
            return true;
        }

        private bool ContainsWalkablePoint(Vector2 point)
        {
            // Test the union before applying the keeper's clearance, so the join
            // remains open. Height is deliberately irrelevant: jumping cannot bypass it.
            return ContainsConvexPoint(_islandOutline, point) || ContainsConvexPoint(_pierOutline, point);
        }

        private static bool ContainsConvexPoint(Vector2[] outline, Vector2 point)
        {
            if (outline.Length < 3) return false;
            for (int i = 0; i < outline.Length; i++)
            {
                if (WalkableCross(outline[i], outline[(i + 1) % outline.Length], point) < -.00001f)
                    return false;
            }
            return true;
        }

        private static Vector2[] WalkableHull(List<Vector2> points)
        {
            if (points.Count < 3) return Array.Empty<Vector2>();
            points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var hull = new List<Vector2>();
            foreach (var point in points)
            {
                while (hull.Count >= 2 && WalkableCross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lowerCount = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                while (hull.Count > lowerCount && WalkableCross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull.ToArray();
        }

        private static float WalkableCross(Vector2 a, Vector2 b, Vector2 point)
        {
            return (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
        }
    }
}
