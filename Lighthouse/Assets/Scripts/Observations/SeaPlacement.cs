using UnityEngine;
namespace LighthouseGame
{
    /// Admission uses the entire prospective silhouette and a safety margin.
    /// It never falls back to a visible position when the sea sector is occupied.
    public static class SeaPlacement
    {
        static readonly Plane[] planes=new Plane[6];
        public static bool ClearOfTraffic(Vector3 position,float radius)
        {
            var env=CinematicEnvironment.Active;
            if(!env||!env.Session||!env.Session.Director.Traffic)return true;
            foreach(var ship in env.Session.Director.Traffic.Ships)
            {var delta=ship.transform.position-position;delta.y=0;if(delta.magnitude<radius)return false;}
            return true;
        }
        public static bool OutsideView(Camera camera,Bounds bounds,float margin=12)
        {
            if(!camera)return false;
            var projection=Matrix4x4.Perspective(Mathf.Max(72,camera.fieldOfView),camera.aspect,camera.nearClipPlane,camera.farClipPlane);
            GeometryUtility.CalculateFrustumPlanes(projection*camera.worldToCameraMatrix,planes);
            bounds.Expand(margin*2);
            return !GeometryUtility.TestPlanesAABB(planes,bounds);
        }
        public static bool OutsideViewSphere(Camera camera,Vector3 centre,float radius)
        {
            if(!camera)return false;
            var projection=Matrix4x4.Perspective(Mathf.Max(72,camera.fieldOfView),camera.aspect,camera.nearClipPlane,camera.farClipPlane);
            GeometryUtility.CalculateFrustumPlanes(projection*camera.worldToCameraMatrix,planes);
            foreach(var plane in planes)if(plane.GetDistanceToPoint(centre)<-radius)return true;
            return false;
        }
        public static bool TryFindEncounter(Camera camera,Vector3 outward,bool colossal,out Vector3 position,out Bounds startBounds)
        {
            for(int i=0;i<64;i++)
            {
                var p=Quaternion.Euler(0,Random.Range(-148f,148f),0)*outward*Random.Range(colossal?125:72,colossal?185:115);
                var tangent=Vector3.Cross(Vector3.up,p.normalized);var start=p-tangent*65;var rotation=Quaternion.LookRotation(-tangent);
                Vector3 right=rotation*Vector3.right,forward=rotation*Vector3.forward;
                var horizontal=new Vector3(Mathf.Abs(right.x),0,Mathf.Abs(right.z))*(colossal?14:5)+new Vector3(Mathf.Abs(forward.x),0,Mathf.Abs(forward.z))*(colossal?38:12);
                var b=new Bounds(start+Vector3.up*(colossal?18:7),horizontal*2+Vector3.up*(colossal?48:22));
                if(ClearOfTraffic(start,colossal?72:44)&&ClearOfTraffic(p,colossal?65:35)&&OutsideView(camera,b))
                {position=p;startBounds=b;return true;}
            }
            position=default;startBounds=default;return false;
        }
        public static bool TryFind(Camera camera,Vector3 outward,float near,float far,Vector3 size,float height,out Vector3 position)
        {
            for(int i=0;i<48;i++)
            {
                float angle=Random.Range(-148f,148f),distance=Random.Range(near,far);
                var p=Quaternion.Euler(0,angle,0)*outward*distance;
                bool occupied=height<45&&!ClearOfTraffic(p,Mathf.Min(60,size.x*.35f)+24);
                if(occupied)continue;
                if(OutsideView(camera,new Bounds(p+Vector3.up*height,size))){position=p;return true;}
            }
            position=default;return false;
        }
    }
}
