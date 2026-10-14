using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    public sealed class KrakenWaterVolume:MonoBehaviour
    {
        [SerializeField] Color underwaterFog=new Color(.035f,.24f,.29f);
        [SerializeField] float fogDensity=.025f,transitionRate=2,particleRadius=12;
        [Header("Full room surface; independent of combat limits")]
        [SerializeField,Min(0)] float edgeOverlap=2;
        [SerializeField,Min(1)] float minimumHalfWidth=26;
        [SerializeField,Range(32,160)] int gridResolution=96;
        [SerializeField] Material floodMaterial;
        [SerializeField] Material moteMaterial;
        [SerializeField,Range(0,.5f)] float waveHeight=.13f;
        MeshRenderer surface;Mesh mesh;Material materialInstance;
        PlayerWaterMovement swimmer;Bounds volume;Camera view;ParticleSystem particles;
        readonly List<Renderer> oldSurfaces=new List<Renderer>();
        readonly Dictionary<Renderer,MaterialPropertyBlock> originalFloorBlocks=new Dictionary<Renderer,MaterialPropertyBlock>();
        [SerializeField,Min(.2f)] float floorBlendDepth=1.8f;
        float originalSurfaceHeight;
        readonly Dictionary<Renderer,bool> stoppedDecorationWater=new Dictionary<Renderer,bool>();
        readonly Dictionary<Renderer,MaterialPropertyBlock> originalDecorationBlocks=new Dictionary<Renderer,MaterialPropertyBlock>();
        MaterialPropertyBlock flowBlock;
        [SerializeField,Range(0,1)] float decorationFadeStart=.12f,decorationFadeEnd=.9f;
        [SerializeField,Min(.05f)] float submergedFlowFeather=.8f;
        public int StoppedDecorationCount=>stoppedDecorationWater.Count;
        public float FlowVisibility{get;private set;}=1;
        public bool IsRaised=>raised;
        public void EnterUnderwater()
        {
            PrepareFlowDecoration();FadeFlowDecoration(1);
            foreach(var pair in stoppedDecorationWater)if(pair.Key)pair.Key.forceRenderingOff=true;
        }
        void PrepareFlowDecoration()
        {
            var room=GetComponentInParent<ForestRoom>();if(!room)return;
            foreach(var r in room.GetComponentsInChildren<Renderer>(true))
            {
                if(r.transform.IsChildOf(transform))continue;
                // 통일된 수면 재질에서도 폭포/거품 역할을 구분해 침수 전환을 유지합니다.
                bool flowing=false;foreach(var m in r.sharedMaterials)if(m&&m.shader&&(m.shader.name=="DogLife/Original Coastal Waterfall"||m.shader.name=="DogLife/Coastal Landing Foam"||(m.HasProperty("_EnvironmentMode")&&m.GetFloat("_EnvironmentMode")>1.5f)))flowing=true;
                if(!flowing)continue;
                if(stoppedDecorationWater.ContainsKey(r))continue;
                stoppedDecorationWater.Add(r,r.forceRenderingOff);var oldBlock=new MaterialPropertyBlock();r.GetPropertyBlock(oldBlock);originalDecorationBlocks.Add(r,oldBlock);
            }
        }
        void FadeFlowDecoration(float progress)
        {
            if(flowBlock==null)flowBlock=new MaterialPropertyBlock();
            float visibility=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(decorationFadeStart,Mathf.Max(decorationFadeStart+.01f,decorationFadeEnd),progress));
            FlowVisibility=visibility;
            foreach(var pair in stoppedDecorationWater)
            {
                var r=pair.Key;if(!r)continue;r.GetPropertyBlock(flowBlock);
                // Independent multiplier survives WaterGateFade's opacity/door animation updates.
                flowBlock.SetFloat("_FloodVisibility",visibility);flowBlock.SetFloat("_FloodLevel",Height);flowBlock.SetFloat("_FloodFeather",submergedFlowFeather);r.SetPropertyBlock(flowBlock);
            }
        }
        bool surfacePrepared,raised;
        public Bounds SurfaceBounds{get;private set;}
        bool captured,fogWasOn;Color oldColor;float oldDensity,oldStart,oldEnd,blend;FogMode oldMode;
        CameraClearFlags oldClearFlags;Color oldBackground;
        public float Height{get;private set;}
        public void SetCinematicLift(bool value){if(swimmer)swimmer.LiftWithSurface=value;}
        public void Setup(PlayerMotor player,Bounds bounds,Material material)
        {
            volume=bounds;view=Camera.main;swimmer=player.GetComponent<PlayerWaterMovement>();if(!swimmer)swimmer=player.gameObject.AddComponent<PlayerWaterMovement>();
            if(view)view.depthTextureMode|=DepthTextureMode.Depth;
            Height=bounds.min.y-.2f;
            var go=new GameObject("Full room flood surface");go.transform.SetParent(transform,false);go.transform.rotation=Quaternion.identity;
            var scale=transform.lossyScale;go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            surface=go.AddComponent<MeshRenderer>();surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            surface.receiveShadows=false;surface.enabled=false;
            materialInstance=floodMaterial?new Material(floodMaterial):new Material(Shader.Find("DogLife/Kraken Flood Surface"));
            materialInstance.SetFloat("_WaveHeight",0);materialInstance.SetFloat("_Reveal",0);materialInstance.renderQueue=3020;surface.sharedMaterial=materialInstance;
            mesh=new Mesh{name="Flood wave grid"};go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var p=new GameObject("Suspended sea motes");p.transform.SetParent(transform,false);particles=p.AddComponent<ParticleSystem>();particles.Stop();var main=particles.main;main.startLifetime=7;main.startSpeed=.15f;main.startSize=.035f;main.startColor=new Color(.45f,.9f,.9f,.35f);main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;var emission=particles.emission;emission.rateOverTime=16;var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=particleRadius;particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            p.transform.localScale=go.transform.localScale;if(moteMaterial)particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=moteMaterial;
            var fade=particles.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(1,.65f),new GradientAlphaKey(0,1)});fade.color=gradient;
        }
        void PrepareSurface()
        {
            if(surfacePrepared)return;surfacePrepared=true;
            PrepareFlowDecoration();
            var room=GetComponentInParent<ForestRoom>();
            var bounds=new Bounds(volume.center,new Vector3(room?room.exitX-room.entryX:volume.size.x,1,minimumHalfWidth*2));
            // Streamed scenery is ready at flood time, not necessarily during boss Start.
            originalSurfaceHeight=volume.min.y;
            if(room)foreach(var r in room.GetComponentsInChildren<Renderer>())
            {
                if(!r.enabled||r.transform.IsChildOf(transform)||r.bounds.size.y>.5f)continue;
                if(r.name.IndexOf("water",System.StringComparison.OrdinalIgnoreCase)<0||r.bounds.size.x<5||r.bounds.size.z<5)continue;
                bool floorWater=false;foreach(var m in r.sharedMaterials)if(m&&m.shader&&(m.shader.name=="DogLife/Coastal Floor Water"||(m.HasProperty("_EnvironmentMode")&&Mathf.Approximately(m.GetFloat("_EnvironmentMode"),1))))floorWater=true;
                if(!floorWater)continue;
                bounds.Encapsulate(r.bounds);oldSurfaces.Add(r);originalSurfaceHeight=Mathf.Max(originalSurfaceHeight,r.bounds.max.y);
                var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);originalFloorBlocks.Add(r,block);
            }
            bounds.Expand(new Vector3(edgeOverlap*2,0,edgeOverlap*2));SurfaceBounds=bounds;
            int n=Mathf.Clamp(gridResolution,32,160),side=n+1;var verts=new Vector3[side*side];var uv=new Vector2[verts.Length];var triangles=new int[n*n*6];int k=0;
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){int i=z*side+x;uv[i]=new Vector2(x/(float)n,z/(float)n);verts[i]=new Vector3((uv[i].x-.5f)*bounds.size.x,0,(uv[i].y-.5f)*bounds.size.z);}
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){int i=z*side+x;triangles[k++]=i;triangles[k++]=i+side;triangles[k++]=i+1;triangles[k++]=i+1;triangles[k++]=i+side;triangles[k++]=i+side+1;}
            mesh.vertices=verts;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.bounds=new Bounds(Vector3.zero,new Vector3(bounds.size.x,2,bounds.size.z));
        }
        public void SetHeight(float y)
        {
            if(!surface)return;PrepareSurface();raised=true;Height=y;if(swimmer)swimmer.SetWater(y,volume);
            surface.transform.position=new Vector3(SurfaceBounds.center.x,y,SurfaceBounds.center.z);surface.enabled=true;
            float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(originalSurfaceHeight-.15f,originalSurfaceHeight+floorBlendDepth*.65f,y));
            materialInstance.SetFloat("_Reveal",reveal);materialInstance.SetFloat("_WaveHeight",waveHeight*reveal);
            if(flowBlock==null)flowBlock=new MaterialPropertyBlock();
            foreach(var r in oldSurfaces)if(r){
                float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(r.bounds.max.y+.1f,r.bounds.max.y+floorBlendDepth,y));
                r.GetPropertyBlock(flowBlock);flowBlock.SetFloat("_FloodFloorVisibility",1-fade);r.SetPropertyBlock(flowBlock);r.enabled=fade<1;
            }
            FadeFlowDecoration(Mathf.InverseLerp(volume.min.y,volume.max.y,y));
        }
        void LateUpdate()
        {
            if(!raised||!view||!surface)return;bool submerged=view.transform.position.y<Height;
            if(!captured&&submerged){GameAudio.Play("Kraken_Submerge",view.transform.position,.5f,minimumInterval:2);captured=true;fogWasOn=RenderSettings.fog;oldColor=RenderSettings.fogColor;oldDensity=RenderSettings.fogDensity;oldMode=RenderSettings.fogMode;oldStart=RenderSettings.fogStartDistance;oldEnd=RenderSettings.fogEndDistance;oldClearFlags=view.clearFlags;oldBackground=view.backgroundColor;}
            blend=Mathf.MoveTowards(blend,submerged?1:0,Time.deltaTime*transitionRate);
            if(captured){RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=Color.Lerp(oldColor,underwaterFog,blend);RenderSettings.fogDensity=Mathf.Lerp(fogWasOn?oldDensity:0,fogDensity,blend);view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=underwaterFog;if(blend<=0)RestoreFog();}
            if(particles){particles.transform.position=view.transform.position;if(submerged&&!particles.isPlaying)particles.Play();else if(!submerged&&particles.isPlaying)particles.Stop();}
        }
        void RestoreFog(){if(!captured)return;RenderSettings.fog=fogWasOn;RenderSettings.fogColor=oldColor;RenderSettings.fogDensity=oldDensity;RenderSettings.fogMode=oldMode;RenderSettings.fogStartDistance=oldStart;RenderSettings.fogEndDistance=oldEnd;if(view){view.clearFlags=oldClearFlags;view.backgroundColor=oldBackground;}captured=false;}
        public void Clear(){FlowVisibility=1;raised=false;if(swimmer)swimmer.Leave();if(surface)surface.enabled=false;foreach(var r in oldSurfaces)if(r){r.enabled=true;r.SetPropertyBlock(originalFloorBlocks[r]);}foreach(var pair in stoppedDecorationWater)if(pair.Key){pair.Key.forceRenderingOff=pair.Value;pair.Key.SetPropertyBlock(originalDecorationBlocks[pair.Key]);}stoppedDecorationWater.Clear();originalDecorationBlocks.Clear();if(particles)particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);RestoreFog();blend=0;}
        void OnDisable(){Clear();}
        void OnDestroy(){if(mesh)Destroy(mesh);if(materialInstance)Destroy(materialInstance);}
    }
}
