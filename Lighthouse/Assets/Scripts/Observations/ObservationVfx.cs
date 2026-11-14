using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace LighthouseGame
{
    /// <summary>Bounded, reusable parametric mesh. Identical equations power the WebGL study.</summary>
    public class ObservationVfx : MonoBehaviour
    {
        public int Kind { get; private set; }
        public ObservationDefinition Definition => ObservationKernels.Catalog[Kind];
        public bool Showing { get; private set; }
        public float Age { get; private set; }
        public float Opacity { get; private set; }
        public float Lifetime;
        public bool Frozen;
        public int VertexCount => positions == null ? 0 : positions.Length;
        Mesh mesh;
        Material material;
        MeshRenderer view;
        Vector3[] positions, parameters;
        WaterDecal waterLayer;Material waterMaterial;float[] waterGrid=new float[21*21];float nextWater;
        float started;
        int heldFrame=-1;
        public void Initialize(int kind, Material template,Material waterTemplate=null)
        {
            Kind=kind;gameObject.layer=2;
            var d=Definition;
            int count=d.Parts*(d.U+1)*(d.V+1);
            positions=new Vector3[count];parameters=new Vector3[count];var uv=new Vector2[count];var parts=new Vector2[count];
            var indices=new List<int>(d.Parts*d.U*d.V*6);int n=0;
            for(int k=0;k<d.Parts;k++)
            {
                int start=n;
                for(int u=0;u<=d.U;u++)for(int v=0;v<=d.V;v++)
                { parameters[n]=new Vector3(u/(float)d.U,v/(float)d.V,k);uv[n]=new Vector2(parameters[n].x,parameters[n].y);parts[n]=new Vector2(k,0);n++; }
                for(int u=0;u<d.U;u++)for(int v=0;v<d.V;v++)
                {int a=start+u*(d.V+1)+v,b=a+d.V+1;indices.Add(a);indices.Add(b);indices.Add(a+1);indices.Add(a+1);indices.Add(b);indices.Add(b+1);}
            }
            mesh=new Mesh{name=d.Key+" · animated ribbon mesh"};mesh.MarkDynamic();mesh.vertices=positions;mesh.uv=uv;mesh.uv2=parts;mesh.SetTriangles(indices,0);
            mesh.bounds=new Bounds(Vector3.up*30,Vector3.one*240);
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;view=gameObject.AddComponent<MeshRenderer>();
            material=new Material(template){name=d.Key+" · instance"};material.SetFloat("_Kind",kind);material.SetColor("_Color",d.Color);material.SetColor("_Accent",d.Accent);view.sharedMaterial=material;
            view.shadowCastingMode=ShadowCastingMode.Off;view.receiveShadows=false;view.enabled=false;
            if(waterTemplate&&(kind==2||kind==3||kind==7))
            {
                waterMaterial=new Material(waterTemplate);waterMaterial.SetFloat("_Kind",kind);
                var go=new GameObject(d.Key+" · native ocean foam and displacement");go.transform.SetParent(transform,false);
                waterLayer=go.AddComponent<WaterDecal>();waterLayer.material=waterMaterial;waterLayer.regionSize=Vector2.one*(kind==7?120:90);waterLayer.resolution=new Vector2Int(256,256);waterLayer.updateMode=CustomRenderTextureUpdateMode.Realtime;go.SetActive(false);
            }
        }
        public void Show(Vector3 anchor, Quaternion rotation, float lifetime=-1)
        {
            transform.SetPositionAndRotation(anchor,rotation);Lifetime=lifetime>0?lifetime:Definition.Duration;
            transform.localScale=Vector3.one;
            if(Kind==2&&CinematicEnvironment.Active.Atmosphere.profile.TryGet<VolumetricClouds>(out var cloud))transform.localScale=new Vector3(1,cloud.bottomAltitude.value/545f,1);
            started=Time.time;Age=0;Frozen=false;Showing=true;view.enabled=true;nextWater=-1;if(waterLayer)waterLayer.gameObject.SetActive(true);Evaluate(0);
        }
        public void Hide(){Showing=false;if(view)view.enabled=false;if(waterLayer)waterLayer.gameObject.SetActive(false);heldFrame=-1;}
        public void HoldForPhoto(){heldFrame=Time.frameCount;}
        void Update()
        {
            if(!Showing||Frozen||heldFrame==Time.frameCount)return;
            Age=Time.time-started;if(Age>=Lifetime){Hide();return;}Evaluate(Age);
        }
        public void Evaluate(float age)
        {
            Age=age;
            Opacity=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/1.5f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((Lifetime-age)/2));
            bool surface=Kind==3||Kind==7||Kind==5;
            if(surface&&Time.time>=nextWater)
            {
                for(int y=0;y<=20;y++)for(int x=0;x<=20;x++)waterGrid[y*21+x]=ShipDestruction.Sample(transform.TransformPoint(new Vector3((x-10)*6,0,(y-10)*6)));
                nextWater=Time.time+.18f;
            }
            for(int i=0;i<positions.Length;i++)
            {
                var p=parameters[i];positions[i]=ObservationKernels.Evaluate(Kind,p.x,p.y,p.z,age);
                if(surface)
                {
                    float x=Mathf.Clamp(positions[i].x/6+10,0,19.99f),y=Mathf.Clamp(positions[i].z/6+10,0,19.99f);int ix=(int)x,iy=(int)y;
                    positions[i].y+=.35f+Mathf.Lerp(Mathf.Lerp(waterGrid[iy*21+ix],waterGrid[iy*21+ix+1],x-ix),Mathf.Lerp(waterGrid[(iy+1)*21+ix],waterGrid[(iy+1)*21+ix+1],x-ix),y-iy);
                }
            }
            mesh.vertices=positions;mesh.RecalculateBounds();material.SetFloat("_Phase",age);material.SetFloat("_Opacity",Opacity);
            var env=CinematicEnvironment.Active;if(env){material.SetFloat("_Visibility",env.Visibility);material.SetFloat("_Daylight",Mathf.Clamp01(env.Sun.intensity/85000));}
            if(waterLayer){waterMaterial.SetFloat("_Phase",age);waterMaterial.SetFloat("_Opacity",Opacity);waterLayer.surfaceFoamDimmer=Opacity*(Kind==7?.18f:.75f);waterLayer.deepFoamDimmer=Opacity*.25f;waterLayer.amplitude=Kind==3?1:0;}
        }
        public Vector3 Focus => transform.TransformPoint(Kind==0?new Vector3(0,110,0):Kind==1?new Vector3(0,85,0):Kind==2?new Vector3(0,235,0):Kind==3||Kind==5||Kind==7?Vector3.zero:new Vector3(0,8,0));
        public bool VisibleIn(Camera camera)
        {
            if(!Showing||Opacity<.18f||!camera)return false;
            int visible=0;Vector2 min=Vector2.one,max=Vector2.zero;
            // Sample rendered vertices, not merely an invisible object's origin.
            // A coprime stride avoids repeatedly sampling the same transparent
            // ribbon edge when the mesh's row width divides a uniform step.
            for(int sample=0;sample<160;sample++)
            {
                int i=(sample*7919)%positions.Length;
                if(Kind==1&&Mathf.Repeat(Age-parameters[i].z*6.7f,22)>2.8f)continue;
                // Transparent tips aren't photographic evidence.
                if(Kind==1&&parameters[i].x>.72f)continue;
                if(Kind==0&&(parameters[i].y<.08f||parameters[i].y>.85f))continue;
                if((Kind==3||Kind==7)&&(parameters[i].y<.08f||parameters[i].y>.92f))continue;
                if(Kind==2&&parameters[i].y>.85f)continue;
                Vector3 world=transform.TransformPoint(positions[i]);Vector3 vp=camera.WorldToViewportPoint(world);
                if(vp.z<=camera.nearClipPlane||vp.z>=camera.farClipPlane||vp.x<.025f||vp.x>.975f||vp.y<.025f||vp.y>.975f)continue;
                Vector3 ray=world-camera.transform.position;
                if(Physics.Raycast(camera.transform.position,ray.normalized,ray.magnitude,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                if(MarinePhotoOcclusion.Blocked(camera.transform.position,world))continue;
                min=Vector2.Min(min,vp);max=Vector2.Max(max,vp);visible++;
                if(visible>=5&&(max.x-min.x)*(max.y-min.y)>.0006f)return true;
            }
            return false;
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);if(waterMaterial)Destroy(waterMaterial);}
    }
}
