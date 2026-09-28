#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
 public static class KrakenBuilder
 {
  const string Art="Assets/Art/Kraken";
  static void Link(Object o,string n,Object v){var s=new SerializedObject(o);s.FindProperty(n).objectReferenceValue=v;s.ApplyModifiedPropertiesWithoutUndo();}
  static GameObject Child(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
  static GameObject Model(string kind,Transform parent)
  {
   string path=Art+"/Models/Kraken_"+kind+".fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.animationType=ModelImporterAnimationType.Generic;imp.importAnimation=true;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.SaveAndReimport();
   foreach(string suffix in new[]{"BaseColor","Normal"}){var ti=(TextureImporter)AssetImporter.GetAtPath(Art+"/Textures/"+kind+"_"+suffix+".png");ti.maxTextureSize=2048;ti.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;ti.textureCompression=TextureImporterCompression.Compressed;ti.SaveAndReimport();}
   var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/"+kind+".mat");if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,Art+"/Materials/"+kind+".mat");}mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+kind+"_BaseColor.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+kind+"_Normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.45f);mat.SetFloat("_Glossiness",.38f);
   string cp=Art+"/Animations/"+kind+".controller";var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(cp);if(!ctrl)ctrl=AnimatorController.CreateAnimatorControllerAtPath(cp);var sm=ctrl.layers[0].stateMachine;foreach(var st in sm.states)sm.RemoveState(st.state);ctrl.parameters=new[]{new AnimatorControllerParameter{name="PhaseSpeed",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
   foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview"))){string name=clip.name.Substring(clip.name.LastIndexOf('_')+1);string ap=Art+"/Animations/"+kind+"_"+name+".anim";var asset=AssetDatabase.LoadAssetAtPath<AnimationClip>(ap);if(!asset){asset=new AnimationClip();AssetDatabase.CreateAsset(asset,ap);}EditorUtility.CopySerialized(clip,asset);asset.name=kind+"_"+name;var settings=AnimationUtility.GetAnimationClipSettings(asset);settings.loopTime=name=="Idle"||name=="Groggy"||name=="Bind";AnimationUtility.SetAnimationClipSettings(asset,settings);var state=sm.AddState(name);state.motion=asset;state.speed=asset.length;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;if(name=="Idle")sm.defaultState=state;}
   var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));go.transform.SetParent(parent,false);foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterial=mat;var anim=go.GetComponent<Animator>();if(!anim)anim=go.AddComponent<Animator>();anim.runtimeAnimatorController=ctrl;anim.applyRootMotion=false;anim.cullingMode=AnimatorCullingMode.AlwaysAnimate;return go;
  }
  public static string Build()
  {
   var tr=new GameObject("KrakenTentacle");var model=Model("Tentacle",tr.transform);var tent=tr.AddComponent<KrakenTentacle>();Link(tent,"animator",model.GetComponent<Animator>());Link(tent,"visual",model.transform);
   foreach(bool hit in new[]{false,true})foreach(bool circle in new[]{false,true})
   {
    var g=Child((hit?"HitCollider":"AttackCollider")+(circle?"Circle":"Line"),tr.transform);Collider c=circle?(Collider)g.AddComponent<SphereCollider>():g.AddComponent<BoxCollider>();c.isTrigger=!hit;c.enabled=false;Link(tent,(hit?"hit":"attack")+(circle?"Sphere":"Box"),c);
    if(!hit){var trigger=g.AddComponent<KrakenAttackTrigger>();trigger.tentacle=tent;}
   }
   var tp=PrefabUtility.SaveAsPrefabAsset(tr,"Assets/Prefabs/monster/KrakenTentacle.prefab");Object.DestroyImmediate(tr);
   var wr=new GameObject("KrakenWave");var wave=wr.AddComponent<KrakenWave>();foreach(string side in new[]{"left","right"}){var g=Child("AttackCollider_"+side,wr.transform);var c=g.AddComponent<BoxCollider>();c.isTrigger=true;c.enabled=false;Link(wave,side,c);g.AddComponent<KrakenAttackTrigger>().wave=wave;}
   var wp=PrefabUtility.SaveAsPrefabAsset(wr,"Assets/Prefabs/monster/KrakenWave.prefab");Object.DestroyImmediate(wr);
   var root=new GameObject("KrakenBoss");try
   {
    var hp=root.AddComponent<CombatHealth>();hp.maxHealth=2500;var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeAll;
    var col=root.AddComponent<CapsuleCollider>();col.height=7;col.radius=2.3f;col.center=Vector3.up*3;
    var nav=root.AddComponent<NavMeshAgent>();nav.radius=1;nav.height=7;nav.speed=0;nav.agentTypeID=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/FloralDeer.prefab").GetComponent<NavMeshAgent>().agentTypeID;
    var body=Model("Body",root.transform);body.transform.localRotation=Quaternion.Euler(0,180,0);var boss=root.AddComponent<KrakenBoss>();Link(boss,"bodyAnimator",body.GetComponent<Animator>());Link(boss,"tentaclePrefab",tp.GetComponent<KrakenTentacle>());Link(boss,"wavePrefab",wp.GetComponent<KrakenWave>());
    var refs=new SerializedObject(boss);var arms=refs.FindProperty("restingTentacles");arms.arraySize=2;
    for(int i=0;i<2;i++){var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/Kraken_Tentacle.fbx"));g.name="RestingTentacle_"+i;g.transform.SetParent(root.transform,false);g.transform.localPosition=new Vector3(i==0?-4:4,-.4f,-1);g.transform.localScale=Vector3.one*.7f;g.transform.localRotation=Quaternion.Euler(0,i==0?35:-35,0);foreach(var r in g.GetComponentsInChildren<Renderer>())r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/Tentacle.mat");var a=g.GetComponent<Animator>();a.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>(Art+"/Animations/Tentacle.controller");arms.GetArrayElementAtIndex(i).objectReferenceValue=a;}refs.ApplyModifiedPropertiesWithoutUndo();
    var fx=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/KrakenWater.mat");if(!fx){fx=new Material(Shader.Find("DogLife/Winter Frost VFX"));AssetDatabase.CreateAsset(fx,Art+"/Materials/KrakenWater.mat");}
    var effects=Child("KrakenWaterEffects",root.transform).AddComponent<KrakenVfx>();Link(effects,"boss",boss);Link(effects,"material",fx);
    root.AddComponent<CombatFeedback>();root.AddComponent<SeasonEnemyHealthBar>();PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/monster/KrakenBoss.prefab");
   }finally{Object.DestroyImmediate(root);}AssetDatabase.SaveAssets();return "KrakenBoss + KrakenTentacle + KrakenWave created";
  }
 }
}
#endif
