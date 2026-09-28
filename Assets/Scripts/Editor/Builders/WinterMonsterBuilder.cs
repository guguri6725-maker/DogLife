using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
namespace DogLife.EditorTools
{
    public static class WinterMonsterBuilder
    {
        const string Art="Assets/Art/WinterMonsters";
        public static readonly string[] Names={"IceGolem","SnowSpirit","SnowWolf","FrostMammoth"};
        static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
        static void Set(Object target,string field,Object value){var s=new SerializedObject(target);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void Float(Object target,string field,float value){var s=new SerializedObject(target);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        public static string Build()
        {
            Folder(Art+"/Animations");Folder(Art+"/Materials");
            var fx=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/WinterFrost.mat");
            if(!fx){fx=new Material(Shader.Find("DogLife/Winter Frost VFX"));AssetDatabase.CreateAsset(fx,Art+"/Materials/WinterFrost.mat");}
            for(int i=0;i<Names.Length;i++)BuildOne(Names[i],i,fx);AssetDatabase.SaveAssets();return "Four winter prefabs built";
        }
        static void BuildOne(string name,int kind,Material fx)
        {
            string path=Art+"/Models/"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();
            var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            string cp=Art+"/Animations/"+name+".controller";var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(cp);if(!ctrl)ctrl=AnimatorController.CreateAnimatorControllerAtPath(cp);
            var sm=ctrl.layers[0].stateMachine;foreach(var s in sm.states)sm.RemoveState(s.state);ctrl.parameters=new[]{new AnimatorControllerParameter{name="Moving",type=AnimatorControllerParameterType.Bool},new AnimatorControllerParameter{name="PhaseSpeed",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
            foreach(var source in clips)
            {
                string stateName=source.name.Substring(source.name.LastIndexOf('_')+1);string ap=Art+"/Animations/"+name+"_"+stateName+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ap);if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,ap);}EditorUtility.CopySerialized(source,clip);clip.name=name+"_"+stateName;
                bool loop=stateName=="Idle"||stateName=="Walk";var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
                var state=sm.AddState(stateName);state.motion=clip;if(!loop){state.speed=clip.length;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;}if(stateName=="Idle")sm.defaultState=state;
            }
            var idle=sm.states.First(s=>s.state.name=="Idle").state;var walk=sm.states.First(s=>s.state.name=="Walk").state;
            if(kind!=1){ctrl.AddParameter("WalkSpeed",AnimatorControllerParameterType.Float);walk.speedParameter="WalkSpeed";walk.speedParameterActive=true;}
            var tr=idle.AddTransition(walk);tr.hasExitTime=false;tr.duration=.18f;tr.AddCondition(AnimatorConditionMode.If,0,"Moving");tr=walk.AddTransition(idle);tr.hasExitTime=false;tr.duration=.18f;tr.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
            foreach(string suffix in new[]{"BaseColor","Normal"})
            {
                var ti=(TextureImporter)AssetImporter.GetAtPath(Art+"/Textures/"+name+"_"+suffix+".png");ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Compressed;ti.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;ti.SaveAndReimport();
            }
            string mp=Art+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,mp);}
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+name+"_BaseColor.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+name+"_Normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.25f);mat.SetFloat("_Glossiness",kind==0?.38f:.22f);
            var root=new GameObject(name);root.transform.localScale=Vector3.one*(kind==1?1:2);
            try
            {
                var hp=root.AddComponent<CombatHealth>();hp.maxHealth=kind==0?240:kind==3?190:kind==2?120:100;
                var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeRotation;
                var body=root.AddComponent<CapsuleCollider>();body.height=kind==0?2.7f:kind==3?1.9f:1.5f;body.radius=kind==0?.7f:kind==3?.65f:.45f;body.center=Vector3.up*body.height*.5f;
                var agent=root.AddComponent<NavMeshAgent>();agent.radius=body.radius;agent.height=body.height;agent.speed=kind==2?3:1.3f;agent.acceleration=12;agent.angularSpeed=180;
                var sample=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/FloralDeer.prefab");if(sample)agent.agentTypeID=sample.GetComponent<NavMeshAgent>().agentTypeID;
                var visual=new GameObject("Visual").transform;visual.SetParent(root.transform,false);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));model.transform.SetParent(visual,false);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>()){renderer.sharedMaterial=mat;var skin=renderer as SkinnedMeshRenderer;if(skin)skin.updateWhenOffscreen=true;}
                var animator=model.GetComponent<Animator>();if(!animator)animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=ctrl;animator.applyRootMotion=false;
                var enemy=root.AddComponent<WinterMonsterEnemy>();var so=new SerializedObject(enemy);so.FindProperty("species").enumValueIndex=kind;so.ApplyModifiedPropertiesWithoutUndo();
                Set(enemy,"visualRoot",visual);Set(enemy,"animator",animator);Set(enemy,"effectMaterial",fx);
                Float(enemy,"moveSpeed",kind==2?3:kind==0?1.1f:1.5f);Float(enemy,"attackDistance",kind==3?3.4f:7);Float(enemy,"attackRange",kind==3?2.8f:6);
                Float(enemy,"fanAngle",kind==3?110:55);Float(enemy,"warningTime",kind==2?.8f:kind==0?1.15f:.95f);Float(enemy,"attackTime",kind==2?.58f:kind==3?.85f:1.25f);
                Float(enemy,"recoveryTime",kind==2?1.7f:1.2f);Float(enemy,"cooldown",kind==1?5:3);
                if(kind==0){Float(enemy,"attackRange",9);Float(enemy,"attackDistance",10);Float(enemy,"shockwaveSpeed",18);}
                if(kind==1)Float(enemy,"iceRadius",5.4f);
                if(kind==2)Float(enemy,"reaimTime",.60f);
                if(kind==3){Float(enemy,"pushSpeed",11);Float(enemy,"pushLift",7);Float(enemy,"pushDuration",.75f);}
                var effect=new GameObject("WinterAttackVFX");effect.transform.SetParent(root.transform,false);var vfx=effect.AddComponent<WinterAttackVfx>();vfx.enemy=enemy;Set(vfx,"material",fx);effect.GetComponent<MeshRenderer>().sharedMaterial=fx;
                root.AddComponent<CombatFeedback>();var bar=root.AddComponent<SeasonEnemyHealthBar>();var bs=new SerializedObject(bar);bs.FindProperty("fillColor").colorValue=new Color(.34f,.62f,.80f);bs.FindProperty("highlightColor").colorValue=new Color(.85f,.96f,1);bs.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/monster/"+name+".prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
        public static string ConnectRooms()
        {
            var prefabs=Names.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/"+n+".prefab")).ToArray();int count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes/SeasonRooms/Winter"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                foreach(var root in scene.GetRootGameObjects())foreach(var spawner in root.GetComponentsInChildren<RoomMonsterSpawner>(true)){spawner.monsterPrefabs=prefabs;PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);EditorUtility.SetDirty(spawner);count++;}
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(scene,true);
            }
            return count+" existing winter spawners connected";
        }
    }
}
