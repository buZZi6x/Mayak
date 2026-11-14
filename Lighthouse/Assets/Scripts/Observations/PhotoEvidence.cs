using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace LighthouseGame
{
 public static class PhotoEvidence
 {
  private static readonly Plane[] FrustumPlanes = new Plane[6];
  public static bool Point(Camera camera,Vector3 point,bool aboveWater=true,Transform subjectVessel=null)
  {
   if(!camera)return false;
   // Reject out-of-frame samples before querying HDRP waves or scene geometry.
   var v=camera.WorldToViewportPoint(point);
   if(v.z<camera.nearClipPlane||v.z>camera.farClipPlane||v.x<.025f||v.x>.975f||v.y<.025f||v.y>.975f)return false;
   bool waterSampled=false;
   if(aboveWater&&point.y<5&&CinematicEnvironment.Active&&CinematicEnvironment.Active.Ocean)
   {
    var parameters=new WaterSearchParameters{targetPositionWS=point,startPositionWS=point,error=.08f,maxIterations=8,includeDeformation=true};
    waterSampled=CinematicEnvironment.Active.Ocean.ProjectPointOnWaterSurface(parameters,out var water);
    if(waterSampled&&point.y<water.projectedPositionWS.y+.08f)return false;
   }
   if(aboveWater&&!waterSampled&&point.y<.15f)return false;
   Vector3 delta=point-camera.transform.position;
   return !Physics.Raycast(camera.transform.position,delta.normalized,delta.magnitude-.15f,~(1<<2),QueryTriggerInteraction.Ignore)
    &&!MarinePhotoOcclusion.Blocked(camera.transform.position,point,subjectVessel);
  }
  public static bool Vessel(Camera camera,Transform ship)
  {
   if(!ship || !ship.gameObject.activeInHierarchy)return false;
   // Real deck/mast locations; origins below water never count as evidence.
   int seen=0;foreach(var r in ship.GetComponentsInChildren<MeshRenderer>())
   {
    if(!r.enabled||r.bounds.size.sqrMagnitude<.5f)continue;
    var b=r.bounds;
    if(Point(camera,b.center,true,ship)&&++seen>=3)return true;
    var offset=Vector3.up*b.extents.y*.7f;
    if(Point(camera,b.center+offset,true,ship)&&++seen>=3)return true;
    if(Point(camera,b.center-offset,true,ship)&&++seen>=3)return true;
   }
   return false;
  }

  public static bool Skinned(Camera camera, SkinnedMeshRenderer body, ref Mesh snapshot, List<Vector3> vertices)
  {
   if (!camera || !body || !body.enabled || body.forceRenderingOff || !body.gameObject.activeInHierarchy || !body.sharedMesh
       || (camera.cullingMask & (1 << body.gameObject.layer)) == 0) return false;
   GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);
   if (!GeometryUtility.TestPlanesAABB(FrustumPlanes, body.bounds)) return false;
   // Sample the posed surface, not the oversized animation bounds or an invisible root.
   // Baking happens only on a shutter press, and each subject reuses its mesh/list.
   if (!snapshot) snapshot = new Mesh { name = "Photo evidence pose", hideFlags = HideFlags.HideAndDontSave };
   body.BakeMesh(snapshot);
   snapshot.GetVertices(vertices);
   int step = Mathf.Max(1, Mathf.CeilToInt(vertices.Count / 48f));
   int visible = 0;
   Vector2 minimum = Vector2.one, maximum = Vector2.zero;
   for (int i = 0; i < vertices.Count; i += step)
   {
    Vector3 point = body.transform.TransformPoint(vertices[i]);
    if (!Point(camera, point)) continue;
    Vector2 viewport = camera.WorldToViewportPoint(point);
    minimum = Vector2.Min(minimum, viewport);
    maximum = Vector2.Max(maximum, viewport);
    visible++;
    if (visible >= 3 && (maximum - minimum).sqrMagnitude >= .006f * .006f) return true;
   }
   return false;
  }
 }
}
