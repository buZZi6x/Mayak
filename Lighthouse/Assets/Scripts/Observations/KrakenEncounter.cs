using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace LighthouseGame
{
 [DefaultExecutionOrder(180)]
 public class KrakenEncounter:MonoBehaviour
 {
  public BlockoutDirector Director;
  public Material Skin,Suckers;
  public WaterDecal Disturbance;
  public Transform Debris;
  public Transform OctopusBody,ColossalBody;
  public Material ColossalSkin,ColossalSuckers;
  public EncounterPresentation Presentation;
  public bool Colossal=true,ManualTimeline;
  public int AdmissionCount {get;private set;}
  public Bounds LastAdmissionBounds {get;private set;}
  public string CreatureName=>Colossal?"Кракен":"Большой осьминог";
  public ShipDestruction Destruction {get;private set;}
  public float Clock {get;private set;}
  public float Grip {get;private set;}
  public string Phase {get;private set;}
  public bool WreckVisible {get;private set;}
  public bool IsActive => admitted;
  private bool _stormEncounter;
  private int _radioPhases;
  // A weather transition must not interrupt a ship already being attacked.
  private bool StormEncounter => ManualTimeline || previewPlaying ? Director.Session.Storm : _stormEncounter;
  public bool Escaping=>!Director.WillEat&&Clock>=36&&Clock<46&&Director.KrakenVisible;
  public bool FrozenPreview;
  Mesh mesh;MeshRenderer arms;Vector3[] vertices,normals;Vector2[] uv;
  Vector3[] armSamples=new Vector3[10*65];
  float ready,previewClock=18;Vector3 center;Quaternion heading;OceanBuoyancy buoyancy;
  bool admitted,creatureAdmitted;float lastInput=-1;CoastalFleet fleet;
  const int Seg=64,Rad=12,TubeVerts=10*(Seg+1)*(Rad+1),CupCount=10*18*2,CupVerts=9*5;
  static float Smooth(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
  void Awake()
  {
   buoyancy=Director.Boat.GetComponent<OceanBuoyancy>();fleet=Director.Boat.GetComponent<CoastalFleet>();
   var go=new GameObject("Eight continuous arms · skin and suckers");go.layer=2;go.transform.SetParent(Director.Kraken,false);
   mesh=new Mesh{name="Kraken flexible arms",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.MarkDynamic();
   vertices=new Vector3[TubeVerts+CupCount*CupVerts];normals=new Vector3[vertices.Length];uv=new Vector2[vertices.Length];
   var skin=new List<int>();var cups=new List<int>();
   for(int a=0;a<10;a++)for(int s=0;s<=Seg;s++)for(int r=0;r<=Rad;r++)uv[(a*(Seg+1)+s)*(Rad+1)+r]=new Vector2(r/(float)Rad,s/(float)Seg*7);
   for(int a=0;a<10;a++)for(int s=0;s<Seg;s++)for(int r=0;r<Rad;r++)Quad(skin,(a*(Seg+1)+s)*(Rad+1)+r,Rad+1);
   for(int c=0;c<CupCount;c++)for(int j=0;j<8;j++)for(int k=0;k<4;k++)Quad(cups,TubeVerts+c*CupVerts+j*5+k,5);
   mesh.vertices=vertices;mesh.uv=uv;mesh.subMeshCount=2;mesh.SetTriangles(skin,0);mesh.SetTriangles(cups,1);
   go.AddComponent<MeshFilter>().sharedMesh=mesh;arms=go.AddComponent<MeshRenderer>();arms.sharedMaterials=new[]{Skin,Suckers};
  }
  static void Quad(List<int> v,int a,int stride){v.Add(a);v.Add(a+1);v.Add(a+stride);v.Add(a+1);v.Add(a+stride+1);v.Add(a+stride);}
  public void Evaluate(float clock,bool isReady)
  {
   ready=isReady?1:0;
   var d=Director;var outward=new Vector3(d.SeaAnchor.position.x,0,d.SeaAnchor.position.z).normalized;var across=Vector3.Cross(Vector3.up,outward);
   if(ManualTimeline||previewPlaying)
   {
    Clock=Mathf.Repeat(clock,92);if(center.sqrMagnitude<1)center=outward*(Colossal?142:78);
    admitted=true;creatureAdmitted=true;
   }
   else
   {
    if(clock<lastInput||!isReady){ResetEncounter();lastInput=clock;}
    float dt=lastInput<0?0:Mathf.Clamp(clock-lastInput,0,.5f);lastInput=clock;
    if(isReady&&!admitted&&!d.Session.Sleeping&&d.Session.CanStartEvent)
    {
     if(SeaPlacement.TryFindEncounter(d.Session.Player.View,outward,Colossal,out var p,out var initialBounds))
     {center=p;admitted=true;_stormEncounter=d.Session.Storm;Clock=6;AdmissionCount++;LastAdmissionBounds=initialBounds;SelectCreature(Colossal);
      d.Session.Radio?.ConsiderForecast(_stormEncounter ? (Colossal ? "kraken" : "octopus") : "ship", 11);}
    }
    if(admitted)
    {
     float next=Clock+dt;
     if(StormEncounter&&!creatureAdmitted&&next>=17)
     {
      // Future risen anatomy only; the already admitted ship has its own moving silhouette.
      var prospective=d.WillEat?d.Boat.position+heading*new Vector3(Colossal?17:-7,Colossal?10:6,Colossal?-27:-4):center+heading*new Vector3(Colossal?17:-7,Colossal?10:6,-4);
      if(!d.Session.Sleeping&&SeaPlacement.OutsideViewSphere(d.Session.Player.View,prospective,Colossal?62:35))creatureAdmitted=true;
      else
      {
       next=16.95f;
       // Only the creature waits. The already visible ship keeps sailing.
       if(!d.Session.Sleeping)center+=Vector3.Cross(Vector3.up,center.normalized)*2.5f*dt;
      }
     }
     Clock=next;
     if(Clock>89)
     {
      bool sinking=StormEncounter&&d.WillEat;
      var departure=sinking?new Bounds(center,Vector3.one*150):new Bounds(d.Boat.position+Vector3.up*(Colossal?18:8),Vector3.one*(Colossal?90:42));
      if(SeaPlacement.OutsideView(d.Session.Player.View,departure))ResetEncounter();
     }
    }
   }
   isReady&=admitted;float t=Clock;
   across=Vector3.Cross(Vector3.up,center.sqrMagnitude>1?center.normalized:outward);heading=Quaternion.LookRotation(-across);
   bool storm=StormEncounter;
   if (isReady && storm && !ManualTimeline && !previewPlaying)
   {
    if (t >= 24 && t < 36 && (_radioPhases & 1) == 0)
    { _radioPhases |= 1; d.Session.Radio?.ConsiderForecast(d.WillEat ? "feast" : "escape", 36 - t); }
    if (d.WillEat && t >= 31 && t < 39 && (_radioPhases & 2) == 0)
    { _radioPhases |= 2; d.Session.Radio?.ConsiderForecast("wreck", 39 - t); }
   }
   d.BoatVisible=isReady&&t>=6&&(!(storm&&d.WillEat)||t<89);
   d.KrakenVisible=isReady&&storm&&t>=17&&t<65;
   Grip=storm&&d.WillEat?Smooth(30,36,t):0;
   d.Feast=d.BoatVisible&&d.KrakenVisible&&d.WillEat&&t>=36&&t<59;
   WreckVisible=isReady&&storm&&d.WillEat&&t>=39&&t<89;
   float travel=storm&&d.WillEat?Mathf.Lerp(-65,0,Mathf.Clamp01((t-6)/26)):-65+(t-6)*(225f/82f);
   float sink=storm&&d.WillEat?Smooth(43,62,t):0;
   d.Boat.SetPositionAndRotation(center+across*travel+Vector3.up*(.15f-(Colossal?36:27)*sink),heading*Quaternion.Euler(8*sink,0,Grip*8+sink*32));
   if(buoyancy)buoyancy.Influence=1-Smooth(40,48,t)*(storm&&d.WillEat?1:0);
   float emerge=Smooth(17,25,t);float retreat=d.WillEat?Smooth(51,65,t):Smooth(38,55,t);
   Vector3 body=d.Boat.position+heading*new Vector3(Colossal?17:-7,-1,Colossal?-27:-4);
   if(!d.WillEat)body=center+heading*new Vector3(Colossal?17:-7,0,-4)+outward*retreat*18;
   body.y=Colossal?-30+emerge*32-retreat*45:-8+emerge*8-retreat*16;
   d.Kraken.SetPositionAndRotation(body,heading*Quaternion.Euler(Colossal?32:0,Colossal?135:-65,Colossal?-8:0));
   d.Boat.gameObject.SetActive(d.BoatVisible);d.Kraken.gameObject.SetActive(d.KrakenVisible);if(Debris)Debris.gameObject.SetActive(WreckVisible&&!Colossal);
   if(Disturbance){Disturbance.gameObject.SetActive(isReady&&storm&&t>=18&&t<78);Disturbance.regionSize=Vector2.one*(Colossal?95:42);Disturbance.transform.position=center;Disturbance.surfaceFoamDimmer=(.12f+.65f*Grip)*(1-Smooth(65,78,t));Disturbance.deepFoamDimmer=.35f;}
   if(Presentation&&isReady&&storm)Presentation.Evaluate(t,center,heading,d.WillEat,Colossal);
   Phase=!isReady?"Морские пути":t>=59&&WreckVisible?"Плавающие мачты и обломки":d.Feast?(t<36.8f?CreatureName+" обхватывает корабль":t<42?"Ломаются мачты":t<48?"Разламывается корпус":CreatureName+" утягивает корабль на дно"):Escaping?"Корабль уходит от опасности":d.KrakenVisible?(t<30?CreatureName+" поднимается из воды":"Щупальца тянутся к кораблю"):d.BoatVisible?"Корабль проходит у маяка":"Морские пути";
   if(!Colossal&&d.Feast)Phase=t<43?"Большой осьминог сжимает катер":"Большой осьминог утягивает катер";
  }
  public void SelectCreature(bool colossal)
  {
   Colossal=colossal;if(OctopusBody)OctopusBody.gameObject.SetActive(!colossal);if(ColossalBody)ColossalBody.gameObject.SetActive(colossal);
   if(fleet){fleet.Select(colossal?Mathf.Clamp(fleet.Selected,1,fleet.Vessels.Length-1):0);Destruction=fleet.Vessels[fleet.Selected].GetComponent<ShipDestruction>();if(Destruction)Destruction.Restore();}
   if(arms)arms.sharedMaterials=colossal&&ColossalSkin?new[]{ColossalSkin,ColossalSuckers}:new[]{Skin,Suckers};
  }
  public void ResetEncounter()
  {
   _radioPhases = 0;
   admitted=creatureAdmitted=false;Clock=0;center=Vector3.zero;Grip=0;
   Director.BoatVisible=Director.KrakenVisible=Director.Feast=WreckVisible=false;
   Director.Boat.gameObject.SetActive(false);Director.Kraken.gameObject.SetActive(false);
   if(Debris)Debris.gameObject.SetActive(false);if(Disturbance)Disturbance.gameObject.SetActive(false);
   if(Destruction)Destruction.Restore();if(Presentation)Presentation.ResetTimeline();
  }
  public void BeginNight(){StopPreview();ManualTimeline=false;lastInput=-1;ResetEncounter();Colossal=Random.value>.25f;}
  // Catmull-Rom through authored anatomical/contact landmarks, continuous in time.
  static Vector3 Curve(Vector3[] p,float u)
  {float f=Mathf.Clamp01(u)*(p.Length-1);int i=Mathf.Min((int)f,p.Length-2);float t=f-i;var a=p[Mathf.Max(0,i-1)];var b=p[i];var c=p[i+1];var d=p[Mathf.Min(p.Length-1,i+2)];return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);}
  readonly Vector3[] path=new Vector3[7];
  void Update()
  {
   if(FrozenPreview)return;
   if(!Director.Session.Running&&previewPlaying){previewClock+=Time.deltaTime;Evaluate(previewClock,true);}
  }
  void LateUpdate(){if(!FrozenPreview)RenderPose();}
  public void RenderPose()
  {
   float t=Clock;
   if(Destruction&&Colossal)Destruction.Evaluate(t,center+Vector3.up*.15f,heading,StormEncounter&&Director.WillEat);
   if(Debris&&WreckVisible&&!Colossal)
   {
    Debris.position=center+Vector3.Cross(Vector3.up,center.normalized)*4;
    for(int i=0;i<Debris.childCount;i++)
    {
     var p=Debris.GetChild(i);float a=i*2.399f;p.localPosition=new Vector3(Mathf.Cos(a)*(2+i*.65f),.45f,Mathf.Sin(a)*(2+i*.65f));
     var parameters=new WaterSearchParameters{targetPositionWS=p.position,startPositionWS=p.position,error=.06f,maxIterations=8,includeDeformation=true};
     if(CinematicEnvironment.Active.Ocean.ProjectPointOnWaterSurface(parameters,out var water))p.position=new Vector3(p.position.x,water.projectedPositionWS.y+.35f,p.position.z);
     p.position+=new Vector3(Mathf.Sin(i)*.15f,0,Mathf.Cos(i)*.15f)*Mathf.Max(0,t-51);p.position-=Vector3.up*Smooth(78,88,t)*12;
     p.localRotation=Quaternion.Euler(Mathf.Sin(t+i)*8,i*57+Mathf.Sin(t*.4f+i)*7,Mathf.Cos(t+i)*6);p.localScale=Vector3.one;
    }
   }
   if(!mesh||!Director.KrakenVisible){if(arms)arms.enabled=false;return;}arms.enabled=true;
   float halfWidth=Colossal?6.3f:2.4f;
   var inverse=Director.Kraken.worldToLocalMatrix;int cup=0;
   for(int a=0;a<10;a++)
   {
    bool feeder=a>=8;float scale=Colossal?2.4f:1;
    float side=a%2==0?-1:1,z=(a/2-1.5f)*(Colossal?9:4.2f);if(feeder)z=side*9;
    float reach=(a==2||a==5?2.5f:0)*scale;
    Vector3 origin=Director.Kraken.position+heading*new Vector3(side*(Colossal?4:2),0,(a/2-1.5f)*(Colossal?2.3f:1.1f));
    var boat=Director.Boat;
    path[0]=origin;path[1]=boat.TransformPoint(new Vector3(side*(halfWidth+3),-1,z-1));
    path[2]=boat.TransformPoint(new Vector3(side*(halfWidth+1.1f),1.3f*scale,z));
    path[3]=boat.TransformPoint(new Vector3(side*halfWidth*.7f,3.9f*scale+reach,z+.15f));
    path[4]=boat.TransformPoint(new Vector3(side*.15f,4.1f*scale+reach,z+.3f));
    path[5]=boat.TransformPoint(new Vector3(-side*(halfWidth*.75f),2.9f*scale+reach*.6f,z+.35f));
    path[6]=boat.TransformPoint(new Vector3(-side*halfWidth*.7f,1.9f*scale,z+.75f));
    // Two silhouette arms hook along the length of the hull. A purely
    // transverse coil is edge-on from the keeper's gallery and reads as a pole.
    if((a==2||a==6)&&!Colossal)
    {
     float zz=a==2?-3.5f:4.5f;
     path[2]=boat.TransformPoint(new Vector3(-halfWidth-1,2.2f,zz-2));
     path[3]=boat.TransformPoint(new Vector3(-halfWidth-.8f,6.3f,zz-2.1f));
     path[4]=boat.TransformPoint(new Vector3(-halfWidth+.3f,7.2f,zz+.2f));
     path[5]=boat.TransformPoint(new Vector3(-halfWidth+1.2f,5.7f,zz+2));
     path[6]=boat.TransformPoint(new Vector3(-halfWidth+1.4f,3.1f,zz+1));
     if(a==6){path[3]=boat.TransformPoint(new Vector3(-halfWidth-.6f,4.8f,zz-3));path[4]=boat.TransformPoint(new Vector3(-halfWidth+.4f,5.1f,zz));path[5]=boat.TransformPoint(new Vector3(-halfWidth+1.2f,3.3f,zz+3));path[6]=boat.TransformPoint(new Vector3(-halfWidth+1.4f,2.1f,zz+4));}
    }
    if(Colossal&&(a==2||a==6||feeder))
    {
     float h=feeder?Mathf.Lerp(29,9,Smooth(36.8f,44,t)):13.5f;
     path[2]=boat.TransformPoint(new Vector3(-halfWidth-3,4,z-5));
     path[3]=boat.TransformPoint(new Vector3(-halfWidth-2,h*.8f,z-4));
     path[4]=boat.TransformPoint(new Vector3(feeder?1:-halfWidth+1,h,z));
     path[5]=boat.TransformPoint(new Vector3(feeder?2:-halfWidth+3,h*.8f,z+5));
     path[6]=boat.TransformPoint(new Vector3(feeder?-1:-halfWidth+2,feeder?h*.68f:6,z+3));
    }
    for(int s=0;s<=Seg;s++)
    {
     float u=s/(float)Seg;float ang=a*.6283f;float armScale=scale*(feeder?1.45f:1);
     Vector3 idle=origin+heading*new Vector3((Mathf.Cos(ang)*(u*9)+Mathf.Sin(u*5+t*.36f+a)*u*2)*armScale,(Mathf.Sin(u*Mathf.PI)*8+(Mathf.Sin(t*.55f+a)*.7f-2)*u)*armScale,(Mathf.Sin(ang)*(u*9)+Mathf.Cos(u*4+t*.42f+a)*u*2)*armScale);
     Vector3 point=Vector3.Lerp(idle,Curve(path,u),Smooth(29+a*.16f,35+a*.16f,t)*Grip);
     if(!Colossal&&feeder)point=origin;armSamples[a*(Seg+1)+s]=point;
    }
    for(int s=0;s<=Seg;s++)
    {
     float u=s/(float)Seg;Vector3 p=armSamples[a*(Seg+1)+s];Vector3 tangent=(armSamples[a*(Seg+1)+Mathf.Min(Seg,s+1)]-armSamples[a*(Seg+1)+Mathf.Max(0,s-1)]).normalized;
     Vector3 n=Vector3.Cross(tangent,heading*Vector3.forward).normalized;if(n.sqrMagnitude<.1f)n=Vector3.right;Vector3 b=Vector3.Cross(tangent,n).normalized;
     float radius=Mathf.Lerp(Colossal?(feeder?1.65f:2.1f):.9f,Colossal?.18f:.11f,Mathf.Pow(u,.8f));
     if(Colossal&&feeder)radius+=Mathf.Exp(-Mathf.Pow((u-.87f)/.08f,2))*.8f;
     if(!Colossal&&feeder)radius=.0001f;
     for(int r=0;r<=Rad;r++){float angle=r*Mathf.PI*2/Rad;Vector3 radial=n*Mathf.Cos(angle)+b*Mathf.Sin(angle);int v=(a*(Seg+1)+s)*(Rad+1)+r;vertices[v]=inverse.MultiplyPoint3x4(p+radial*radius);normals[v]=inverse.MultiplyVector(radial).normalized;}
     if(s>=6&&s<=57&&(s-6)%3==0)for(int row=0;row<2;row++)
     {
      float ca=(row==0?.22f:-.22f);Vector3 outdir=(n+ca*b).normalized;Vector3 c=p+outdir*radius;float cr=radius*.29f;
      for(int j=0;j<=8;j++)for(int k=0;k<=4;k++)
      {float aa=j*Mathf.PI*2/8,bb=k*Mathf.PI*2/4;Vector3 across=(tangent*Mathf.Cos(aa)+b*Mathf.Sin(aa));Vector3 nn=across*Mathf.Cos(bb)+outdir*Mathf.Sin(bb);int v=TubeVerts+cup*CupVerts+j*5+k;vertices[v]=inverse.MultiplyPoint3x4(c+across*(cr+cr*.33f*Mathf.Cos(bb))+outdir*(cr*.33f*Mathf.Sin(bb)));normals[v]=inverse.MultiplyVector(nn).normalized;}
      cup++;
     }
    }
   }
   mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();
  }
  public bool CreatureInFrame(Camera camera)
  {
   if(!Director.KrakenVisible||!camera)return false;
   int seen=0;Vector2 min=Vector2.one,max=Vector2.zero;Vector3 previous=Vector3.positiveInfinity;
   // Small octopuses have eight arms. The two collapsed feeder meshes are never evidence.
   for(int i=0;i<(Colossal?10:8)*(Seg+1);i+=7)
   {
    var p=armSamples[i];if((p-previous).sqrMagnitude<.16f||!PhotoEvidence.Point(camera,p))continue;
    previous=p;var v=(Vector2)camera.WorldToViewportPoint(p);min=Vector2.Min(min,v);max=Vector2.Max(max,v);seen++;
    if(seen>=5&&(max.x-min.x)*(max.y-min.y)>.0004f)return true;
   }
   foreach(var r in Director.Kraken.GetComponentsInChildren<MeshRenderer>())
   {
    if(r.name.StartsWith("Eight")||r.bounds.size.sqrMagnitude<.1f)continue;
    var filter=r.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh.isReadable)continue;
    var points=filter.sharedMesh.vertices;
    for(int i=0;i<points.Length;i+=Mathf.Max(1,points.Length/28))
    {
     var point=r.transform.TransformPoint(points[i]);if(!PhotoEvidence.Point(camera,point))continue;
     var v=(Vector2)camera.WorldToViewportPoint(point);min=Vector2.Min(min,v);max=Vector2.Max(max,v);
     if(++seen>=5&&(max.x-min.x)*(max.y-min.y)>.0004f)return true;
    }
   }
   return false;
  }
  public bool ShipInFrame(Camera camera)=>Director.BoatVisible&&(Colossal&&Destruction&&Destruction.Broken?Destruction.HullInFrame(camera):PhotoEvidence.Vessel(camera,Director.Boat));
  public bool WreckInFrame(Camera camera)
  {if(!WreckVisible)return false;if(Colossal)return Destruction&&Destruction.VisibleFragments(camera);if(!Debris)return false;int seen=0;foreach(Transform p in Debris)if(p.localScale.x>.25f&&PhotoEvidence.Point(camera,p.position))seen++;return seen>=2;}
  bool previewPlaying;
  public void Preview(bool eat,bool colossal=true)
  {
   if(Director.Session.Running)return;ResetEncounter();SelectCreature(colossal);ManualTimeline=true;Director.Session.Observations.Clear();Director.Session.Observations.Automatic=false;Director.WillEat=eat;Director.Session.Storm=true;Director.FreezePreview=true;FrozenPreview=false;previewPlaying=true;previewClock=18;
   var env=CinematicEnvironment.Active;env.SetWeather(CoastalWeather.Storm,true);env.SetHour(18.4f);env.AdvanceTime=false;
   if(Presentation)Presentation.ResetTimeline(18);Evaluate(18,true);Director.Session.Player.Teleport(Director.Session.Observation.position,center+Vector3.up*(Colossal?9:4));Director.Session.Notify(CreatureName+" · просмотр события · F9 атака · F10 уход · F11 осьминог");
  }
  public void StopPreview(){previewPlaying=false;FrozenPreview=false;ManualTimeline=false;ResetEncounter();}
  void OnDestroy(){if(mesh)Destroy(mesh);}
 }
}
