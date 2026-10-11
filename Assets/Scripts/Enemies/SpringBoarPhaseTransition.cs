using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // First depletion changes the body and hands control to the skeletal moveset.
    public sealed class SpringBoarPhaseTransition : CombatPhaseTransition
    {
        [SerializeField] GameObject fleshBody, skeletonBody;
        [SerializeField] Animator fleshAnimator, skeletonAnimator;
        [SerializeField] SpringBoarBoss phaseOne;
        [SerializeField] SpringBoarVfx attacks;
        [SerializeField] Shader peelingShader;
        [Header("페이즈 전환")]
        [SerializeField, Min(.2f)] float anticipation = 1.9f, peelDuration = 1.9f, recovery = 1.2f;
        [SerializeField] SpringBoarMetamorphosisVfx transformationVfx;
        [SerializeField, Range(0,1)] float roarShake=.22f, landingShake=.4f;
        [SerializeField, Min(1)] float shakeDistance=35;
        [SerializeField, Min(1)] float phaseTwoHealth = 1800;
        [Header("벗겨지는 살점 / 나무껍질 조각")]
        [SerializeField, Range(8,120)] int fragmentCount = 70;
        [SerializeField, Min(.01f)] float fragmentSize = .2f;
        [SerializeField, Min(0)] float fragmentTravel = 2.1f, fragmentLift = 1.5f;
        [SerializeField] Color edgeColor = new Color(.65f,.38f,.32f);
        public int CurrentPhase { get; private set; } = 1;
        public bool Transitioning { get; private set; }
        public float Progress { get; private set; }
        public float HealthReveal => Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1,Progress));
        CombatHealth health;
        SpringBoarCinematic cinematic;
        readonly List<Material> temporary = new List<Material>();
        readonly List<Material[]> fleshOriginal = new List<Material[]>();
        readonly List<Material[]> skeletonOriginal = new List<Material[]>();
        SkinnedMeshRenderer[] fleshRenderers;
        SkinnedMeshRenderer[] skeletonRenderers;
        GameObject fragments;
        Mesh fragmentsMesh;
        Vector3[] fragmentVertices, originalVertices, centers, directions;
        Vector2[] fragmentUV;
        int[] fragmentTriangles;

        void Awake()
        {
            health = GetComponent<CombatHealth>();
            cinematic=GetComponent<SpringBoarCinematic>();
            if(!cinematic)cinematic=gameObject.AddComponent<SpringBoarCinematic>();
            if(skeletonBody) skeletonBody.SetActive(false);
            fleshRenderers = fleshBody ? fleshBody.GetComponentsInChildren<SkinnedMeshRenderer>(true) : new SkinnedMeshRenderer[0];
            skeletonRenderers = skeletonBody ? skeletonBody.GetComponentsInChildren<SkinnedMeshRenderer>(true) : new SkinnedMeshRenderer[0];
        }

        public override bool TryBeginNextPhase()
        {
            if(CurrentPhase==2&&!Transitioning&&health.Current<=0)
            {
                var death=GetComponent<SpringBoarDeath>();
                return death&&death.Begin(skeletonAnimator);
            }
            if(CurrentPhase != 1 || Transitioning || !fleshBody || !skeletonBody) return false;
            CurrentPhase = 2;
            Transitioning = true;
            health.invulnerable = true;
            if(phaseOne) phaseOne.enabled = false;
            if(attacks) attacks.gameObject.SetActive(false);
            var agent = GetComponent<NavMeshAgent>();
            if(agent && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); agent.velocity = Vector3.zero; }
            StartCoroutine(ChangeBody());
            return true;
        }

        static void Pose(Animator animator, string state, float speed)
        {
            if(!animator) return;
            animator.SetBool("Moving",false);
            animator.SetFloat("PhaseSpeed",speed);
            animator.Play(state,0,0);
            animator.Update(0);
        }

        IEnumerator ChangeBody()
        {
            // CombatHealth sets its deferred-death flag after TryBeginNextPhase returns.
            yield return null;
            float duration=anticipation+peelDuration+recovery;
            cinematic.Begin();
            Pose(fleshAnimator,"Metamorphosis",1f/duration);
            foreach(var renderer in fleshRenderers)
            {
                var originals=renderer.sharedMaterials;
                fleshOriginal.Add(originals);
                var replacements=new Material[originals.Length];
                for(int i=0;i<originals.Length;i++)
                {
                    var mat=new Material(originals[i]);
                    if(peelingShader) mat.shader=peelingShader;
                    mat.SetColor("_EdgeColor",edgeColor);
                    mat.SetFloat("_Peel",0);
                    mat.SetMatrix("_BossWorldToLocal",transform.worldToLocalMatrix);
                    replacements[i]=mat; temporary.Add(mat);
                }
                renderer.sharedMaterials=replacements;
            }
            skeletonBody.SetActive(true);
            foreach(var renderer in skeletonRenderers)
            {
                var originals=renderer.sharedMaterials;
                skeletonOriginal.Add(originals);
                var replacements=new Material[originals.Length];
                for(int i=0;i<originals.Length;i++)
                {
                    var mat=new Material(originals[i]);
                    if(peelingShader) mat.shader=peelingShader;
                    mat.SetColor("_EdgeColor",edgeColor);
                    mat.SetFloat("_Reveal",1);
                    mat.SetFloat("_Peel",0);
                    mat.SetMatrix("_BossWorldToLocal",transform.worldToLocalMatrix);
                    replacements[i]=mat; temporary.Add(mat);
                }
                renderer.sharedMaterials=replacements;
            }
            Pose(skeletonAnimator,"Metamorphosis",1f/duration);
            if(transformationVfx) transformationVfx.Begin();
            float elapsed=0;
            bool fragmentsBuilt=false,roared=false,landed=false;
            while(elapsed<duration)
            {
                Progress=Mathf.Clamp01(elapsed/duration);
                float speed=cinematic.Speed(Progress);
                if(fleshAnimator)fleshAnimator.speed=speed;
                if(skeletonAnimator)skeletonAnimator.speed=speed;
                cinematic.SetProgress(Progress);
                float p=Mathf.Clamp01((elapsed-anticipation)/peelDuration);
                if(!fragmentsBuilt&&p>0) { transformationSound=GameAudio.Play("Boar_Phase_Transform",transform.position,.85f,follow:transform);BuildFragments(); fragmentsBuilt=true; }
                float charge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.43f,Progress))*(1-Mathf.InverseLerp(.65f,.8f,Progress));
                foreach(var mat in temporary) { mat.SetFloat("_Peel",p); mat.SetFloat("_Cracks",charge); }
                AnimateFragments(p);
                if(transformationVfx) transformationVfx.SetProgress(Progress);
                if(!roared&&Progress>=.53f) { roared=true; GameAudio.Play("Boar_Roar",transform.position,.9f,follow:transform);Shake(roarShake); }
                if(!landed&&Progress>=.79f) { landed=true; GameAudio.Play("Boar_Ground_Slam",transform.position,.65f);Shake(landingShake); }
                elapsed+=Time.deltaTime*speed;
                yield return null;
            }
            Progress=1;
            transformationSound.Stop();
            if(fleshAnimator)fleshAnimator.speed=1;
            if(skeletonAnimator)skeletonAnimator.speed=1;
            cinematic.End();
            fleshBody.SetActive(false);
            ClearFragments();
            RestoreMaterials();
            Pose(skeletonAnimator,"Idle",1);
            if(transformationVfx) transformationVfx.Finish();
            health.maxHealth=phaseTwoHealth;
            health.Restore();
            health.RefreshVisuals();
            health.invulnerable=false;
            Transitioning=false;
            var phaseTwo=GetComponent<SpringBoarPhaseTwo>();
            if(phaseTwo)phaseTwo.Activate(skeletonAnimator);
        }

        void Shake(float strength)
        {
            var camera=Camera.main;
            if(!camera || !camera.TryGetComponent<PlayerCamera>(out var controller)) return;
            float attenuation=Mathf.Clamp01(1-Vector3.Distance(camera.transform.position,transform.position)/shakeDistance);
            controller.AddImpactShake(strength*attenuation,.32f);
        }

        void BuildFragments()
        {
            if(fleshRenderers.Length==0) return;
            var renderer=fleshRenderers[0];
            var baked=new Mesh();
            renderer.BakeMesh(baked);
            var points=baked.vertices;
            var triangles=baked.triangles;
            var uv=baked.uv;
            if(triangles.Length<3) { Destroy(baked); return; }
            int count=Mathf.Clamp(fragmentCount,8,120);
            originalVertices=new Vector3[count*6]; fragmentVertices=new Vector3[count*6];
            fragmentUV=new Vector2[count*6]; fragmentTriangles=new int[count*6];
            centers=new Vector3[count]; directions=new Vector3[count];
            var random=new System.Random(173);
            // Each curled strip retains the source texture at a sampled point on the current pose.
            for(int i=0;i<count;i++)
            {
                int at=random.Next(triangles.Length/3)*3;
                int a=triangles[at], b=triangles[at+1], c=triangles[at+2];
                // This FBX's bone matrices already bake its 100x import scale and boss scale.
                // TransformPoint would apply those scales a second time and scatter strips far away.
                Vector3 center=renderer.transform.position+renderer.transform.rotation*((points[a]+points[b]+points[c])/3);
                Vector3 normal=renderer.transform.TransformDirection(Vector3.Cross(points[b]-points[a],points[c]-points[a]).normalized);
                Vector3 tangent=Vector3.Cross(normal,Vector3.up).normalized;
                if(tangent.sqrMagnitude<.01f) tangent=Vector3.right;
                Vector3 up=Vector3.Cross(normal,tangent).normalized;
                float size=fragmentSize*Mathf.Lerp(.6f,1.5f,(float)random.NextDouble());
                var offsets=new[]{-tangent-up*.6f,tangent-up*.6f,tangent+up*.6f,-tangent-up*.6f,tangent+up*.6f,-tangent+up*.6f};
                var tex=uv.Length==points.Length?(uv[a]+uv[b]+uv[c])/3:Vector2.zero;
                centers[i]=center;
                directions[i]=(normal+Vector3.up*.5f).normalized;
                for(int j=0;j<6;j++)
                {
                    int v=i*6+j;
                    originalVertices[v]=offsets[j]*size;
                    fragmentVertices[v]=center;
                    fragmentUV[v]=tex+new Vector2(Vector3.Dot(offsets[j],tangent),Vector3.Dot(offsets[j],up))*.008f;
                    fragmentTriangles[v]=v;
                }
            }
            Destroy(baked);
            fragments=new GameObject("SpringBoar peeling fragments");
            fragments.transform.SetParent(transform,true);
            fragments.transform.position=Vector3.zero; fragments.transform.rotation=Quaternion.identity;
            fragments.transform.localScale=new Vector3(1/transform.lossyScale.x,1/transform.lossyScale.y,1/transform.lossyScale.z);
            fragmentsMesh=new Mesh {name="Boar skin strips"};
            fragmentsMesh.MarkDynamic();
            fragmentsMesh.vertices=fragmentVertices; fragmentsMesh.uv=fragmentUV; fragmentsMesh.triangles=fragmentTriangles;
            fragments.AddComponent<MeshFilter>().sharedMesh=fragmentsMesh;
            var surface=fragments.AddComponent<MeshRenderer>();
            surface.sharedMaterial=fleshOriginal[0][0];
            surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void AnimateFragments(float p)
        {
            if(!fragmentsMesh) return;
            for(int i=0;i<centers.Length;i++)
            {
                float age=Mathf.Clamp01((p-(i%13)/13f*.55f)/.45f);
                float scale=age<=0?0:Mathf.Sin(age*Mathf.PI);
                Vector3 center=centers[i]+directions[i]*fragmentTravel*age+Vector3.up*(fragmentLift*Mathf.Sin(age*Mathf.PI)-age*age*.7f);
                var rotation=Quaternion.AngleAxis(age*(180+i%5*45),directions[i]);
                for(int j=0;j<6;j++) fragmentVertices[i*6+j]=center+rotation*originalVertices[i*6+j]*scale;
            }
            fragmentsMesh.vertices=fragmentVertices;
            fragmentsMesh.RecalculateNormals(); fragmentsMesh.RecalculateBounds();
        }

        void RestoreMaterials()
        {
            for(int i=0;i<fleshOriginal.Count;i++) if(fleshRenderers[i]) fleshRenderers[i].sharedMaterials=fleshOriginal[i];
            fleshOriginal.Clear();
            for(int i=0;i<skeletonOriginal.Count;i++) if(skeletonRenderers[i]) skeletonRenderers[i].sharedMaterials=skeletonOriginal[i];
            skeletonOriginal.Clear();
            foreach(var mat in temporary) if(mat) Destroy(mat);
            temporary.Clear();
        }
        void ClearFragments()
        {
            if(fragments) Destroy(fragments);
            if(fragmentsMesh) Destroy(fragmentsMesh);
        }
        GameAudio.Handle transformationSound;
        void OnDisable() { transformationSound.Stop();StopAllCoroutines(); if(cinematic)cinematic.End(); if(fleshAnimator)fleshAnimator.speed=1;if(skeletonAnimator)skeletonAnimator.speed=1; ClearFragments(); RestoreMaterials(); if(transformationVfx) transformationVfx.Clear(); }
    }
}
