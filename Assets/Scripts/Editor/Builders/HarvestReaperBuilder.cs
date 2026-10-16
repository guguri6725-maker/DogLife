#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
 public static class HarvestReaperBuilder
 {
  const string Models="Assets/Models/Monster/Autumn/HarvestReaper";
  const string Textures="Assets/Textures/Monster/Autumn/HarvestReaper";
  const string Materials="Assets/Materials/Monster/Autumn/HarvestReaper";
  const string Animations="Assets/Animations/Monster/Autumn/HarvestReaper";
  static void Link(Object o,string key,Object value){var so=new SerializedObject(o);so.FindProperty(key).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
  public static string Build()
  {
   string model=Models+"/HarvestReaper.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.meshCompression=ModelImporterMeshCompression.Off;importer.SaveAndReimport();
   foreach(string suffix in new[]{"BaseColor","Normal"}){var ti=(TextureImporter)AssetImporter.GetAtPath(Textures+"/"+suffix+".png");ti.maxTextureSize=2048;ti.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;ti.textureCompression=TextureImporterCompression.Compressed;ti.SaveAndReimport();}
   var mat=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/HarvestReaper.mat");if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,Materials+"/HarvestReaper.mat");}mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+"/BaseColor.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+"/Normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.35f);mat.SetFloat("_Glossiness",.15f);
   var fx=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/HarvestVfx.mat");if(!fx){fx=new Material(Shader.Find("DogLife/Winter Frost VFX"));AssetDatabase.CreateAsset(fx,Materials+"/HarvestVfx.mat");}
   string controllerPath=Animations+"/HarvestReaper.controller";var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);if(!ctrl)ctrl=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);var sm=ctrl.layers[0].stateMachine;foreach(var st in sm.states)sm.RemoveState(st.state);ctrl.parameters=new[]{new AnimatorControllerParameter{name="PhaseSpeed",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
   foreach(var source in AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")))
   {
    string name=source.name.Substring(source.name.LastIndexOf('_')+1),path=Animations+"/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}EditorUtility.CopySerialized(source,clip);clip.name="HarvestReaper_"+name;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=name=="Idle"||name=="SpecialHold";AnimationUtility.SetAnimationClipSettings(clip,settings);var state=sm.AddState(name);state.motion=clip;state.speed=clip.length;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;if(name=="Idle")sm.defaultState=state;
   }
   var root=new GameObject("HarvestReaperBoss");try{
    var hp=root.AddComponent<CombatHealth>();hp.maxHealth=2200;
    var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeAll;
    var body=root.AddComponent<CapsuleCollider>();body.height=5.5f;body.radius=1;body.center=new Vector3(0,2.75f,0);
    var nav=root.AddComponent<NavMeshAgent>();nav.radius=1;nav.height=6;nav.speed=0;nav.agentTypeID=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monster/Spring/FloralDeer/FloralDeer.prefab").GetComponent<NavMeshAgent>().agentTypeID;
    var modelObject=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model));modelObject.transform.SetParent(root.transform,false);foreach(var r in modelObject.GetComponentsInChildren<Renderer>())r.sharedMaterial=mat;
    var anim=modelObject.GetComponent<Animator>();if(!anim)anim=modelObject.AddComponent<Animator>();anim.runtimeAnimatorController=ctrl;anim.applyRootMotion=false;
    var boss=root.AddComponent<HarvestReaperBoss>();Link(boss,"animator",anim);
    var effects=new GameObject("HarvestEffects");effects.transform.SetParent(root.transform,false);var vfx=effects.AddComponent<HarvestReaperVfx>();Link(vfx,"boss",boss);Link(vfx,"material",fx);effects.GetComponent<MeshRenderer>().sharedMaterial=fx;
    var grip=modelObject.GetComponentsInChildren<Transform>().First(t=>t.name=="Scythe");var tip=new GameObject("HarvestBladeTip").transform;tip.SetParent(grip,false);tip.position=modelObject.transform.TransformPoint(new Vector3(3.08f,4.48f,.14f));Link(vfx,"weaponTip",tip);Link(vfx,"weaponGrip",grip);
    var vs=new SerializedObject(vfx);vs.FindProperty("ornamentWidth").floatValue=1.3f;vs.FindProperty("edgeWidth").floatValue=.22f;vs.FindProperty("warningOpacity").floatValue=.24f;vs.FindProperty("stormLeaves").intValue=160;vs.FindProperty("stormHeight").floatValue=7;vs.FindProperty("burstHeight").floatValue=4.5f;vs.FindProperty("pumpkinSize").floatValue=1.05f;vs.FindProperty("trailWidth").floatValue=1.25f;vs.FindProperty("trailLifetime").floatValue=.18f;vs.ApplyModifiedPropertiesWithoutUndo();
    root.AddComponent<CombatFeedback>();var bar=root.AddComponent<SeasonEnemyHealthBar>();var so=new SerializedObject(bar);so.FindProperty("maxVisibleDistance").floatValue=55;so.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Monster/Autumn/HarvestReaper/Boss.prefab");
   }finally{Object.DestroyImmediate(root);}AssetDatabase.SaveAssets();return "HarvestReaperBoss prefab, 13 states, 2200 HP";
  }
 }
}
#endif
