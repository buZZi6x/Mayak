using System.Collections.Generic;
using UnityEngine;
namespace LighthouseGame
{
    /// Photo-only triangle tests: the sea meshes intentionally have no gameplay colliders.
    /// Cached source triangles are shared by every instance; no per-frame physics cost.
    public static class MarinePhotoOcclusion
    {
        sealed class Geometry {public Vector3[] Vertices;public int[] Indices;}
        static readonly Dictionary<Mesh,Geometry> geometry=new Dictionary<Mesh,Geometry>();
        static readonly Dictionary<Transform,MeshRenderer[]> surfaces=new Dictionary<Transform,MeshRenderer[]>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear(){geometry.Clear();surfaces.Clear();}
        public static bool Blocked(Vector3 from,Vector3 point,Transform subjectVessel=null)
        {
            var env=CinematicEnvironment.Active;if(!env||!env.Session)return false;
            var d=env.Session.Director;var delta=point-from;float length=delta.magnitude-.3f;
            if(length<=0)return false;var ray=new Ray(from,delta.normalized);
            if(d.BoatVisible&&d.Boat!=subjectVessel&&Hit(d.Boat,ray,length))return true;
            if(d.Traffic)foreach(var fleet in d.Traffic.Ships)
                if(fleet&&fleet.transform!=subjectVessel&&Hit(fleet.transform,ray,length))return true;
            return false;
        }
        static bool Hit(Transform vessel,Ray worldRay,float maxDistance)
        {
            if(!vessel||!vessel.gameObject.activeInHierarchy)return false;
            if(!surfaces.TryGetValue(vessel,out var renderers))surfaces[vessel]=renderers=vessel.GetComponentsInChildren<MeshRenderer>(true);
            foreach(var r in renderers)
            {
                if(!r||!r.enabled||!r.gameObject.activeInHierarchy||!r.bounds.IntersectRay(worldRay,out float distance)||distance>maxDistance)continue;
                var filter=r.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;
                var mesh=filter.sharedMesh;
                if(!geometry.TryGetValue(mesh,out var g))geometry[mesh]=g=new Geometry{Vertices=mesh.vertices,Indices=mesh.triangles};
                var matrix=r.transform.worldToLocalMatrix;var origin=matrix.MultiplyPoint3x4(worldRay.origin);var direction=matrix.MultiplyVector(worldRay.direction);
                // Do not normalise the local ray: t must remain a distance in world metres.
                for(int i=0;i<g.Indices.Length;i+=3)
                {
                    var a=g.Vertices[g.Indices[i]];var edge1=g.Vertices[g.Indices[i+1]]-a;var edge2=g.Vertices[g.Indices[i+2]]-a;
                    var h=Vector3.Cross(direction,edge2);float det=Vector3.Dot(edge1,h);if(Mathf.Abs(det)<1e-12f)continue;
                    float inverse=1/det;var s=origin-a;float u=inverse*Vector3.Dot(s,h);if(u<0||u>1)continue;
                    var q=Vector3.Cross(s,edge1);float v=inverse*Vector3.Dot(direction,q);if(v<0||u+v>1)continue;
                    float t=inverse*Vector3.Dot(edge2,q);if(t>.1f&&t<maxDistance)return true;
                }
            }
            return false;
        }
    }
}
