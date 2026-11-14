using UnityEngine;
namespace LighthouseGame
{
    [DefaultExecutionOrder(90)]
    public class MarineTraffic:MonoBehaviour
    {
        public BlockoutSession Session;
        public CoastalFleet[] Ships;
        public int Relocations {get;private set;}
        public int VisiblePopulation {get;private set;}
        Vector3[] centers;float[] angles,speeds,radii;
        public bool Frozen;
        void Awake()
        {
            int n=Ships.Length;centers=new Vector3[n];angles=new float[n];speeds=new float[n];radii=new float[n];
            // The menu uses the same fleet without a gameplay session.
            var outward=Session ? new Vector3(Session.SeaEvent.position.x,0,Session.SeaEvent.position.z).normalized : Vector3.back;
            for(int i=0;i<n;i++)
            {
                Ships[i].Select(1+i%3);radii[i]=140+i*47+Random.Range(-9,14);angles[i]=Mathf.Atan2(outward.x,outward.z)*Mathf.Rad2Deg-83+i*53;
                speeds[i]=(i%2==0?1:-1)*Random.Range(.32f,.52f);Position(i);
            }
        }
        void Position(int i)
        {
            Vector3 dir=Quaternion.Euler(0,angles[i],0)*Vector3.forward;
            Vector3 tangent=Quaternion.Euler(0,angles[i]+90,0)*Vector3.forward*Mathf.Sign(speeds[i]);
            centers[i]=dir*radii[i]+Vector3.up*.15f;
            Ships[i].transform.SetPositionAndRotation(centers[i],Quaternion.LookRotation(-tangent));
        }
        void Update()
        {
            VisiblePopulation=0;if(Ships==null)return;
            for(int i=0;i<Ships.Length;i++)
            {
                if(!Ships[i])continue;VisiblePopulation++;
                if(Frozen||(Session&&(Session.GameOver||Session.Sleeping)))continue;
                angles[i]+=speeds[i]*Time.deltaTime;
                Position(i);
                // Long-lived vessels sail around the island; no on-screen wrap or respawn.
            }
        }
        public bool InPhoto(Camera camera)
        {
            return TryPhotoTarget(camera, out _);
        }
        public bool TryPhotoTarget(Camera camera, out CoastalFleet target)
        {
            target = null;
            if (Ships == null) return false;
            foreach (var ship in Ships)
            {
                if (!ship || (target && ship.PhotoPoints <= target.PhotoPoints)) continue;
                if (PhotoEvidence.Vessel(camera, ship.transform)) target = ship;
            }
            return target;
        }
    }
}
