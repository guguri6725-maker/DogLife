using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
namespace DogLife.EditorTools
{
    public static class SummerMonsterBuilder
    {
        const string Art="Assets/Art/SummerMonsters";
        public static readonly string[] Names={"CoralMermaid","BlueJellyfish","BoneTortoise","HermitCrab"};
        static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
        static void Set(UnityEngine.Object target,string field,UnityEngine.Object value){var s=new SerializedObject(target);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void Float(UnityEngine.Object target,string field,float value){var s=new SerializedObject(target);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        public static string Build()
        {
            Folder(Art+"/Animations");Folder(Art+"/Materials");
            string fxPath=Art+"/Materials/SummerWater.mat";
            var fx=AssetDatabase.LoadAssetAtPath<Material>(fxPath);
            if(!fx){fx=new Material(Shader.Find("DogLife/Summer Water VFX"));AssetDatabase.CreateAsset(fx,fxPath);}
            for(int i=0;i<Names.Length;i++)BuildOne(Names[i],i,fx);
            AssetDatabase.SaveAssets();return "Four rigged animated summer prefabs built";
        }
        static void BuildOne(string name,int kind,Material fx)
        {
            string modelPath=Art+"/Models/"+name+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();
            var clips=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            string ctrlPath=Art+"/Animations/"+name+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm=controller.layers[0].stateMachine;foreach(var s in sm.states)sm.RemoveState(s.state);
            controller.parameters=new AnimatorControllerParameter[0];controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);controller.AddParameter("PhaseSpeed",AnimatorControllerParameterType.Float);
            foreach(var source in clips)
            {
                string stateName=source.name.Substring(source.name.LastIndexOf(name+"_",StringComparison.Ordinal)+name.Length+1);
                string path=Art+"/Animations/"+name+"_"+stateName+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(!clip){clip=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(clip,path);}else EditorUtility.CopySerialized(source,clip);
                clip.name=name+"_"+stateName;
                bool loop=stateName=="Idle"||stateName=="Walk"||stateName=="Burrow"||stateName=="GuardLoop";
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
                var state=sm.AddState(stateName);state.motion=clip;
                if(!loop){state.speed=clip.length;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;}
                if(stateName=="Idle")sm.defaultState=state;
            }
            var idle=sm.states.First(s=>s.state.name=="Idle").state;var walk=sm.states.First(s=>s.state.name=="Walk").state;
            var transition=idle.AddTransition(walk);transition.hasExitTime=false;transition.duration=.15f;transition.AddCondition(AnimatorConditionMode.If,0,"Moving");
            transition=walk.AddTransition(idle);transition.hasExitTime=false;transition.duration=.15f;transition.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/"+name+".mat");if(!material){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,Art+"/Materials/"+name+".mat");}
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+name+"_BaseColor.png");material.SetFloat("_Glossiness",kind==1?.4f:.18f);
            if(kind==1){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);}
            var root=new GameObject(name);root.layer=0;
            try
            {
                var health=root.AddComponent<CombatHealth>();health.maxHealth=kind==2?180:kind==0?110:80;
                var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeRotation;
                var body=root.AddComponent<CapsuleCollider>();body.radius=kind==2?.65f:.45f;body.height=kind==0?2.2f:kind==1?2.0f:1.4f;body.center=Vector3.up*body.height*.5f;
                var agent=root.AddComponent<NavMeshAgent>();agent.radius=body.radius;agent.height=body.height;agent.baseOffset=0;agent.speed=2;agent.angularSpeed=180;agent.acceleration=12;
                var example=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/FloralDeer.prefab");if(example)agent.agentTypeID=example.GetComponent<NavMeshAgent>().agentTypeID;
                var visual=new GameObject("Visual").transform;visual.SetParent(root.transform,false);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));model.transform.SetParent(visual,false);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>()){renderer.sharedMaterial=material;if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;}
                var animator=model.GetComponent<Animator>();if(!animator)animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                var enemy=root.AddComponent<SummerMonsterEnemy>();var serialized=new SerializedObject(enemy);serialized.FindProperty("species").enumValueIndex=kind;serialized.ApplyModifiedPropertiesWithoutUndo();
                Set(enemy,"visualRoot",visual);Set(enemy,"animator",animator);Set(enemy,"effectMaterial",fx);
                Float(enemy,"moveSpeed",kind==2?1.1f:kind==1?1.5f:2.0f);Float(enemy,"attackDistance",kind==0?8:kind==1?4.2f:kind==2?2.3f:8);
                Float(enemy,"attackRadius",kind==0?2:kind==1?3.4f:kind==2?1.05f:1);
                Float(enemy,"attackTime",kind==0?.85f:kind==1?.65f:kind==2?.55f:.7f);
                Float(enemy,"warningTime",kind==2?.65f:.85f);Float(enemy,"cooldown",kind==3?4:2.5f);
                if(kind==0)Float(enemy,"diveTime",.9f);
                var attack=new GameObject(kind==1?"DamageArea":"AttackCollider");attack.transform.SetParent(root.transform,false);attack.transform.localPosition=Vector3.up*.8f;
                var sphere=attack.AddComponent<SphereCollider>();sphere.radius=kind==1?3.4f:kind==0?2:1.05f;sphere.isTrigger=true;sphere.enabled=false;Set(enemy,"attackCollider",sphere);
                var origin=new GameObject("BubbleOrigin").transform;origin.SetParent(visual,false);origin.localPosition=new Vector3(0,.85f,.6f);Set(enemy,"bubbleOrigin",origin);
                var marker=new GameObject("SubmergedEffectAnchor").transform;marker.SetParent(root.transform,false);marker.localPosition=Vector3.up*.23f;Set(enemy,"submergedEffectAnchor",marker);
                var vfxGo=new GameObject("SummerAttackVFX");vfxGo.transform.SetParent(root.transform,false);var vfx=vfxGo.AddComponent<SummerWaterVfx>();vfx.enemy=enemy;Set(vfx,"material",fx);vfxGo.GetComponent<MeshRenderer>().sharedMaterial=fx;Set(enemy,"presentation",vfx);
                root.AddComponent<CombatFeedback>();var hp=root.AddComponent<SeasonEnemyHealthBar>();var hs=new SerializedObject(hp);hs.FindProperty("fillColor").colorValue=new Color(.2f,.65f,.75f);hs.FindProperty("highlightColor").colorValue=new Color(.65f,.95f,1);hs.ApplyModifiedPropertiesWithoutUndo();
                foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=0;
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/monster/"+name+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        public static string ConnectRooms()
        {
            var prefabs=Names.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/"+n+".prefab")).ToArray();int count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes/SeasonRooms/SummerJourney7","Assets/Scenes/SeasonRooms/Summer"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                foreach(var root in scene.GetRootGameObjects())foreach(var spawner in root.GetComponentsInChildren<RoomMonsterSpawner>(true)){spawner.monsterPrefabs=prefabs;PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);EditorUtility.SetDirty(spawner);count++;}
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(scene,true);
            }
            return count+" summer room spawners connected";
        }
    }
}
