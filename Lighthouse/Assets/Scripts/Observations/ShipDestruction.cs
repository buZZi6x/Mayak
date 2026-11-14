using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace LighthouseGame
{
    /// Authored pieces stay in the vessel hierarchy. Rest matrices prevent drift,
    /// deterministic trajectories allow seeking, and HDRP water owns buoyant height.
    public class ShipDestruction:MonoBehaviour
    {
        public Transform Vessel;
        public int ReleasedCount {get;private set;}
        public int FloatingCount {get;private set;}
        public float LargestWaveError {get;private set;}
        public bool Broken=>ReleasedCount>0;
        public bool EnabledForAttack;
        public float Clock;
        public Vector3 AttackOrigin;
        public Quaternion AttackHeading;
        public float ShipScale=1;
        struct Piece
        {
            public Transform T;public Vector3 LocalPosition,LocalScale,Offset;
            public Quaternion LocalRotation,RelativeRotation;
            public float Release;public bool Mast,Hull;public int Index;
        }
        Piece[] pieces;Vector3[] releasePositions;Quaternion[] releaseRotations;bool[] released;
        Transform[] intactCanvas,brokenCanvas;
        public void Initialize()
        {
            if(pieces!=null)return;
            intactCanvas=GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("STATE_Intact")).ToArray();
            brokenCanvas=GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("STATE_Broken")).ToArray();
            var transforms=GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("BREAK_")).ToArray();
            pieces=new Piece[transforms.Length];int mast=0,hull=0,plank=0;
            releasePositions=new Vector3[pieces.Length];releaseRotations=new Quaternion[pieces.Length];released=new bool[pieces.Length];
            for(int i=0;i<transforms.Length;i++)
            {
                var t=transforms[i];bool m=t.name.StartsWith("BREAK_Mast"),h=t.name.StartsWith("BREAK_Hull");int index=m?mast++:h?hull++:plank++;
                pieces[i]=new Piece{T=t,LocalPosition=t.localPosition,LocalScale=t.localScale,LocalRotation=t.localRotation,Offset=Vessel.InverseTransformPoint(t.position),RelativeRotation=Quaternion.Inverse(Vessel.rotation)*t.rotation,Release=m?36.8f+index*1.65f:h?42+index*.8f:37.6f+index*.31f,Mast=m,Hull=h,Index=index};
            }
        }
        public void Restore()
        {
            Initialize();ReleasedCount=FloatingCount=0;Array.Clear(released,0,released.Length);
            foreach(var t in intactCanvas)t.gameObject.SetActive(true);foreach(var t in brokenCanvas)t.gameObject.SetActive(false);
            foreach(var p in pieces){p.T.localPosition=p.LocalPosition;p.T.localRotation=p.LocalRotation;p.T.localScale=p.LocalScale;}
        }
        public void Evaluate(float clock,Vector3 origin,Quaternion heading,bool attack)
        {
            Initialize();Clock=clock;AttackOrigin=origin;AttackHeading=heading;EnabledForAttack=attack;
            bool torn=attack&&clock>=37.1f;
            foreach(var t in intactCanvas)if(t.gameObject.activeSelf==torn)t.gameObject.SetActive(!torn);
            foreach(var t in brokenCanvas)if(t.gameObject.activeSelf!=torn)t.gameObject.SetActive(torn);
            ReleasedCount=FloatingCount=0;LargestWaveError=0;
            for(int i=0;i<pieces.Length;i++)
            {
                var p=pieces[i];p.T.localScale=p.LocalScale;
                if(!attack||clock<p.Release){p.T.localPosition=p.LocalPosition;p.T.localRotation=p.LocalRotation;released[i]=false;continue;}
                ReleasedCount++;float age=clock-p.Release;float side=p.Index%2==0?-1:1;
                // The imported hierarchy's rotation/scale is already in the rest matrices.
                if(!released[i]){p.T.localPosition=p.LocalPosition;p.T.localRotation=p.LocalRotation;releasePositions[i]=age<.5f?p.T.position:origin+heading*Quaternion.Euler(0,0,8)*p.Offset;releaseRotations[i]=Quaternion.Inverse(heading)*p.T.rotation;released[i]=true;}
                Vector3 rest=releasePositions[i];
                Vector3 drift=heading*new Vector3(side*(p.Mast?8:p.Hull?3:5),0,(p.Index-1)*(p.Hull?3:.7f));
                float scatter=1-Mathf.Exp(-age*.45f);
                Vector3 position=rest+drift*scatter;
                Quaternion fall=heading*Quaternion.Euler((p.Hull?side*18:side*12)*scatter,side*age*(p.Mast?1.1f:4),p.Mast?side*Mathf.SmoothStep(0,84,Mathf.Clamp01(age/3.4f)):side*28*scatter)*releaseRotations[i];
                // Heavy hull sections split, roll and sink. Masts and planks remain.
                if(p.Hull)
                {
                    position.y=rest.y-Mathf.SmoothStep(0,30,Mathf.Clamp01((age-2)/13));
                    fall=heading*Quaternion.Euler(side*age*2,side*scatter*9,side*Mathf.SmoothStep(0,55,Mathf.Clamp01(age/12)))*releaseRotations[i];
                }
                else
                {
                    position+=heading*new Vector3(side,0,.25f)*(Mathf.Max(0,age-4)*.22f);
                    float water=Sample(position);float target=water+(p.Mast?.35f:.16f);
                    float landing=p.Mast?4.4f:2.5f;
                    position.y=Mathf.Lerp(rest.y+Mathf.Sin(Mathf.Clamp01(age/landing)*Mathf.PI)*(p.Mast?2:3),target,Mathf.SmoothStep(0,1,Mathf.Clamp01(age/landing)));
                    if(age>=landing)
                    {
                        FloatingCount++;LargestWaveError=Mathf.Max(LargestWaveError,Mathf.Abs(position.y-target));
                        if(p.Mast)
                        {
                            var settled=heading*Quaternion.Euler(90,side*(12+p.Index*13),0)*p.RelativeRotation;
                            fall=Quaternion.Slerp(fall,settled,Mathf.SmoothStep(0,1,Mathf.Clamp01((age-landing)/3)));
                        }
                        // Wave normals from widely spaced samples tilt the actual fragment.
                        float front=Sample(position+heading*Vector3.forward*2),back=Sample(position-heading*Vector3.forward*2);
                        float left=Sample(position-heading*Vector3.right*2),right=Sample(position+heading*Vector3.right*2);
                        fall=Quaternion.AngleAxis(Mathf.Atan2(right-left,4)*Mathf.Rad2Deg,heading*Vector3.forward)*Quaternion.AngleAxis(-Mathf.Atan2(front-back,4)*Mathf.Rad2Deg,heading*Vector3.right)*fall;
                    }
                    // Pieces submerge at the end instead of shrinking into nothing.
                    position.y-=Mathf.SmoothStep(0,12,Mathf.Clamp01((clock-78)/10));
                }
                p.T.SetPositionAndRotation(position,fall);
            }
        }
        public bool VisibleFragments(Camera camera)
        {
            if(pieces==null||!Broken)return false;int visible=0;
            foreach(var p in pieces)if(!p.Hull&&Clock>=p.Release&&PhotoEvidence.Point(camera,p.T.position,true,Vessel)&&++visible>=2)return true;
            return false;
        }
        public bool HullInFrame(Camera camera)
        {
            if(pieces==null)return false;int visible=0;
            foreach(var p in pieces)if(p.Hull)
                foreach(var r in p.T.GetComponentsInChildren<MeshRenderer>())
                    if(r.bounds.size.sqrMagnitude>4&&PhotoEvidence.Point(camera,r.bounds.center+Vector3.up*r.bounds.extents.y*.65f,true,Vessel)&&++visible>=3)return true;
            return false;
        }
        public static float Sample(Vector3 position)
        {
            var env=CinematicEnvironment.Active;if(!env||!env.Ocean)return 0;
            var p=new WaterSearchParameters{targetPositionWS=position,startPositionWS=position,error=.06f,maxIterations=8,includeDeformation=true};
            return env.Ocean.ProjectPointOnWaterSurface(p,out var r)?r.projectedPositionWS.y:0;
        }
    }
}
