using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife.EditorTools
{
    public static class BlossomSkeletonBuilder
    {
        const string Folder="Assets/Art/Enemies/BlossomSkeleton";
        public static string Build()
        {
            string prefabPath="Assets/Prefabs/monster/BlossomSkeleton.prefab";
            Directory.CreateDirectory("Assets/Prefabs/monster");AssetDatabase.Refresh();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))return "Prefab already exists";
            var shader=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Player3D/DogLight/Materials/DogLight_Lit.mat").shader;
            var material=new Material(shader){name="BlossomSkeleton"};
            material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/base_color.png"));
            var normalImporter=(TextureImporter)AssetImporter.GetAtPath(Folder+"/normal.png");
            normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.SaveAndReimport();
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/normal.png"));
            material.EnableKeyword("_NORMALMAP");material.SetFloat("_Glossiness",.25f);
            AssetDatabase.CreateAsset(material,Folder+"/BlossomSkeleton.mat");
            var controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/BlossomSkeleton.controller");
            controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);controller.AddParameter("Attacking",AnimatorControllerParameterType.Bool);
            var sm=controller.layers[0].stateMachine;
            foreach(string name in new[]{"Idle","Walk","Attack"})
            {
                var source=AssetDatabase.LoadAllAssetsAtPath(Folder+"/BlossomSkeleton.fbx").OfType<AnimationClip>().First(c=>c.name=="Armature|"+name);
                var clip=Object.Instantiate(source);clip.name=name;
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=name!="Attack";settings.loopBlend=false;
                AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,Folder+"/"+name+".anim");
                var state=sm.AddState(name);state.motion=clip;if(name=="Idle")sm.defaultState=state;
            }
            var idle=sm.states.First(s=>s.state.name=="Idle").state;var walk=sm.states.First(s=>s.state.name=="Walk").state;var attack=sm.states.First(s=>s.state.name=="Attack").state;
            var transition=idle.AddTransition(walk);transition.hasExitTime=false;transition.duration=.12f;transition.AddCondition(AnimatorConditionMode.If,0,"Moving");transition.AddCondition(AnimatorConditionMode.IfNot,0,"Attacking");
            transition=walk.AddTransition(idle);transition.hasExitTime=false;transition.duration=.12f;transition.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");transition.AddCondition(AnimatorConditionMode.IfNot,0,"Attacking");
            transition=sm.AddAnyStateTransition(attack);transition.hasExitTime=false;transition.duration=0;transition.canTransitionToSelf=false;transition.AddCondition(AnimatorConditionMode.If,0,"Attacking");
            transition=attack.AddTransition(idle);transition.hasExitTime=false;transition.duration=.08f;transition.AddCondition(AnimatorConditionMode.IfNot,0,"Attacking");
            var root=MonsterRootFactory.Create();
            try
            {
                root.name="BlossomSkeleton";
                Object.DestroyImmediate(root.GetComponent<StageEnemy>());
                var enemy=root.AddComponent<BlossomSkeletonEnemy>();
                foreach(Transform child in root.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                var visual=new GameObject("VisualRoot");visual.transform.SetParent(root.transform,false);visual.transform.localPosition=new Vector3(0,-1,0);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/BlossomSkeleton.fbx"));
                model.transform.SetParent(visual.transform,false);model.transform.localScale=Vector3.one*2;
                var animator=model.GetComponent<Animator>();if(!animator)animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                var head=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
                var idleClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Idle.anim");
                var headPath=AnimationUtility.CalculateTransformPath(head,model.transform);
                var values=new float[4];int axisIndex=0;
                foreach(var axis in new[]{"x","y","z","w"})
                {
                    var binding=AnimationUtility.GetCurveBindings(idleClip).First(b=>b.path==headPath&&b.propertyName=="m_LocalRotation."+axis);
                    values[axisIndex++]=AnimationUtility.GetEditorCurve(idleClip,binding).Evaluate(0);
                }
                head.localRotation=new Quaternion(values[0],values[1],values[2],values[3]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(head);
                foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.sharedMaterial=material;
                enemy.visualRoot=visual.transform;enemy.modelAnimator=animator;enemy.speed=2;enemy.detectionRange=13;
                enemy.hitTime=1.25f;enemy.attackDuration=1.65f;
                var agent=root.GetComponent<NavMeshAgent>();if(!agent)agent=root.AddComponent<NavMeshAgent>();agent.speed=2;agent.radius=.4f;agent.height=2;agent.baseOffset=1;agent.stoppingDistance=1.3f;agent.updateRotation=false;
                root.GetComponent<Rigidbody>().isKinematic=true;
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            }
            finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();return prefabPath;
        }
    }
}
