using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
    public static class SpringBoarBuilder
    {
        const string Art="Assets/Art/SpringBoar";
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');Folder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
        static void Link(Object o,string name,Object value){var s=new SerializedObject(o);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        public static string Build()
        {
            Folder(Art+"/Materials");Folder(Art+"/Animations");
            string model=Art+"/Models/SpringBoar.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();
            foreach(string suffix in new[]{"BaseColor","Normal"}){var texture=(TextureImporter)AssetImporter.GetAtPath(Art+"/Textures/SpringBoar_"+suffix+".png");texture.maxTextureSize=2048;texture.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;texture.textureCompression=TextureImporterCompression.Compressed;texture.SaveAndReimport();}
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/SpringBoar.mat");if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,Art+"/Materials/SpringBoar.mat");}
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/SpringBoar_BaseColor.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/SpringBoar_Normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.30f);mat.SetFloat("_Glossiness",.18f);
            string ctrlPath=Art+"/Animations/SpringBoar.controller";var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);if(!ctrl)ctrl=AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm=ctrl.layers[0].stateMachine;foreach(var st in sm.states)sm.RemoveState(st.state);
            ctrl.parameters=new[]{new AnimatorControllerParameter{name="Moving",type=AnimatorControllerParameterType.Bool},new AnimatorControllerParameter{name="WalkSpeed",type=AnimatorControllerParameterType.Float,defaultFloat=1},new AnimatorControllerParameter{name="PhaseSpeed",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
            foreach(var source in AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")))
            {
                string name=source.name.Substring(source.name.LastIndexOf('_')+1),path=Art+"/Animations/SpringBoar_"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}EditorUtility.CopySerialized(source,clip);clip.name="SpringBoar_"+name;
                bool loop=name=="Idle"||name=="Walk"||name=="Charge"||name=="Stunned";var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
                var state=sm.AddState(name);state.motion=clip;if(!loop){state.speed=clip.length;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;}if(name=="Walk"){state.speedParameter="WalkSpeed";state.speedParameterActive=true;}if(name=="Charge")state.speed=3.4f;if(name=="Idle")sm.defaultState=state;
            }
            var idle=sm.states.First(s=>s.state.name=="Idle").state;var walk=sm.states.First(s=>s.state.name=="Walk").state;var tr=idle.AddTransition(walk);tr.hasExitTime=false;tr.duration=.15f;tr.AddCondition(AnimatorConditionMode.If,0,"Moving");tr=walk.AddTransition(idle);tr.hasExitTime=false;tr.duration=.15f;tr.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
            var fx=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/SakuraBoneVfx.mat");if(!fx){fx=new Material(Shader.Find("DogLife/Winter Frost VFX"));AssetDatabase.CreateAsset(fx,Art+"/Materials/SakuraBoneVfx.mat");}
            var root=new GameObject("SpringBoarBoss");root.transform.localScale=Vector3.one*10;
            try{
                var hp=root.AddComponent<CombatHealth>();hp.maxHealth=1800;
                var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeRotation;
                var body=root.AddComponent<CapsuleCollider>();body.direction=2;body.height=.90f;body.radius=.23f;body.center=new Vector3(0,.30f,0);
                foreach(float x in new[]{-.19f,.19f})foreach(float z in new[]{-.20f,.22f}){var leg=new GameObject("LegHurtbox");leg.transform.SetParent(root.transform,false);leg.transform.localPosition=new Vector3(x,.12f,z);var c=leg.AddComponent<CapsuleCollider>();c.height=.24f;c.radius=.09f;}
                var agent=root.AddComponent<NavMeshAgent>();agent.radius=2.3f;agent.height=6;agent.speed=2.5f;agent.acceleration=7;agent.angularSpeed=130;
                var sample=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/FloralDeer.prefab");agent.agentTypeID=sample.GetComponent<NavMeshAgent>().agentTypeID;
                var visual=new GameObject("Visual").transform;visual.SetParent(root.transform,false);var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model));obj.transform.SetParent(visual,false);
                foreach(var renderer in obj.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=mat;
                var animator=obj.GetComponent<Animator>();if(!animator)animator=obj.AddComponent<Animator>();animator.runtimeAnimatorController=ctrl;animator.applyRootMotion=false;
                var boss=root.AddComponent<SpringBoarBoss>();Link(boss,"animator",animator);Link(boss,"visualRoot",visual);
                var effect=new GameObject("SakuraBoneEffects");effect.transform.SetParent(root.transform,false);var vfx=effect.AddComponent<SpringBoarVfx>();vfx.boss=boss;Link(vfx,"material",fx);effect.GetComponent<MeshRenderer>().sharedMaterial=fx;
                root.AddComponent<CombatFeedback>();var bar=root.AddComponent<SeasonEnemyHealthBar>();var so=new SerializedObject(bar);so.FindProperty("maxVisibleDistance").floatValue=55;so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/monster/SpringBoarBoss.prefab");
            }finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Phase1/Models/SpringBoarPhase1.fbx")) SpringBoarPhaseBuilder.Build();
            return "SpringBoarBoss: scale 10, 11 animations, phase-one attacks / skeletal phase transition";
        }
    }
}
