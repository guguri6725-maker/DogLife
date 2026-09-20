using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.Animations;

namespace DogLife.EditorTools
{
    public static class FloralDeerBuilder
    {
        const string Folder="Assets/Art/Enemies/FloralDeer/";
        public static void UpdateEffects(GameObject root)
        {
            var petal=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"Petal.asset");
            if(!petal)
            {
                petal=new Mesh{name="DeerPetal"};petal.vertices=new[]{new Vector3(0,-.5f,0),new Vector3(.3f,0,0),new Vector3(.2f,.5f,0),new Vector3(0,.3f,0),new Vector3(-.2f,.5f,0),new Vector3(-.3f,0,0),Vector3.zero};
                petal.triangles=new[]{6,0,1,6,1,2,6,2,3,6,3,4,6,4,5,6,5,0};petal.colors=Enumerable.Repeat(Color.white,7).ToArray();petal.RecalculateBounds();petal.RecalculateNormals();AssetDatabase.CreateAsset(petal,Folder+"Petal.asset");
            }
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"DeerPetals.mat");mat.shader=Shader.Find("DogLife/Deer Petals");EditorUtility.SetDirty(mat);
            foreach(var ps in root.GetComponentsInChildren<ParticleSystem>())
            {
                var r=ps.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.enableGPUInstancing=false;r.mesh=petal;r.sharedMaterial=mat;
                var rotation=ps.rotationOverLifetime;rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-6,6);
            }
        }
        static ParticleSystem Effect(Transform parent,string name,Material mat,bool loop,Vector3 offset)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=offset;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var m=ps.main;m.duration=.7f;m.loop=loop;m.playOnAwake=false;m.startLifetime=new ParticleSystem.MinMaxCurve(.2f,.55f);m.startSize=new ParticleSystem.MinMaxCurve(.035f,.10f);m.startSpeed=new ParticleSystem.MinMaxCurve(.15f,.6f);m.maxParticles=50;m.simulationSpace=ParticleSystemSimulationSpace.World;m.gravityModifier=.4f;
            m.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.45f,.65f),new Color(1,.9f,.94f));
            var em=ps.emission;em.rateOverTime=loop?32:0;if(!loop)em.SetBursts(new[]{new ParticleSystem.Burst(0,18)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=loop?.25f:.1f;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
            var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
        [MenuItem("DogLife/Monsters/Build Floral Deer")]
        public static string Build()
        {
            const string path="Assets/Prefabs/monster/FloralDeer.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return "Prefab already exists";
            var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"FloralDeer.fbx");importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.SaveAndReimport();
            var normal=(TextureImporter)AssetImporter.GetAtPath(Folder+"normal.png");normal.textureType=TextureImporterType.NormalMap;normal.SaveAndReimport();
            var material=new Material(Shader.Find("Standard")){name="FloralDeer"};material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"base_color.png");material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"normal.png"));material.EnableKeyword("_NORMALMAP");material.SetFloat("_Glossiness",.2f);AssetDatabase.CreateAsset(material,Folder+"FloralDeer.mat");
            var energy=new Material(Shader.Find("Sprites/Default")){name="DeerPetals",color=new Color(1,.45f,.68f,.85f)};AssetDatabase.CreateAsset(energy,Folder+"DeerPetals.mat");
            var controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"FloralDeer.controller");var sm=controller.layers[0].stateMachine;
            foreach(string name in new[]{"Idle","Walk","Windup","Charge","Recover"})
            {
                var source=AssetDatabase.LoadAllAssetsAtPath(Folder+"FloralDeer.fbx").OfType<AnimationClip>().First(c=>c.name.EndsWith("|"+name));
                var clip=Object.Instantiate(source);clip.name=name;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=name=="Idle"||name=="Walk"||name=="Charge";AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,Folder+name+".anim");
                var state=sm.AddState(name);state.motion=clip;if(name=="Idle")sm.defaultState=state;
            }
            var root=new GameObject("FloralDeer");
            try
            {
                root.layer=0;
                var collider=root.AddComponent<CapsuleCollider>();collider.direction=2;collider.radius=.35f;collider.height=1.2f;
                root.AddComponent<Rigidbody>().isKinematic=true;root.AddComponent<CombatHealth>().maxHealth=32;
                var agent=root.AddComponent<NavMeshAgent>();agent.radius=.4f;agent.height=1.6f;agent.baseOffset=1;agent.speed=2.3f;agent.updateRotation=false;agent.acceleration=30;
                var visual=new GameObject("VisualRoot");visual.transform.SetParent(root.transform,false);visual.transform.localPosition=Vector3.down;
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"FloralDeer.fbx"));model.transform.SetParent(visual.transform,false);model.transform.localScale=Vector3.one*2;
                var animator=model.GetComponent<Animator>();if(!animator)animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();skin.sharedMaterial=material;
                // Include every animation pose in the skin's culling bounds.
                var bounds=new Bounds();bool first=true;var basis=skin.rootBone?skin.rootBone:skin.transform;var baked=new Mesh();
                try{foreach(string name in new[]{"Idle","Walk","Windup","Charge","Recover"})
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+name+".anim");
                    for(int f=0;f<=30;f++)
                    {
                        clip.SampleAnimation(model,clip.length*f/30f);skin.BakeMesh(baked);var b=baked.bounds;var scale=skin.transform.lossyScale;
                        for(int i=0;i<8;i++){var p=new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z);p=skin.transform.TransformPoint(new Vector3(p.x/scale.x,p.y/scale.y,p.z/scale.z));p=basis.InverseTransformPoint(p);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                    }
                }}finally{Object.DestroyImmediate(baked);}
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"Idle.anim").SampleAnimation(model,0);skin.localBounds=bounds;
                var enemy=root.AddComponent<FloralDeerEnemy>();enemy.visualRoot=visual.transform;enemy.modelAnimator=animator;
                var warn=new GameObject("ChargeWarning");warn.transform.SetParent(root.transform,false);enemy.warning=warn.AddComponent<LineRenderer>();enemy.warning.sharedMaterial=energy;enemy.warning.useWorldSpace=true;enemy.warning.startWidth=.035f;enemy.warning.endWidth=.035f;enemy.warning.enabled=false;enemy.warning.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                enemy.scrapeEffect=Effect(root.transform,"HoofScrape",energy,false,new Vector3(.12f,-.95f,.4f));enemy.chargeEffect=Effect(root.transform,"ChargePetals",energy,true,new Vector3(0,-.45f,-.35f));
                UpdateEffects(root);
                Directory.CreateDirectory("Assets/Prefabs/monster");PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();return path;
        }
    }
}
