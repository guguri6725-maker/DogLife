using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

namespace DogLife.EditorTools
{
    public static class SpringBoarPhaseBuilder
    {
        const string Art="Assets/Art/SpringBoar";
        static void Link(Object obj,string field,Object value)
        {
            var serialized=new SerializedObject(obj);
            serialized.FindProperty(field).objectReferenceValue=value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        public static string Build()
        {
            string animationModel=Art+"/Phase1/Models/SpringBoarMetamorphosis.fbx";
            var animationImporter=AssetImporter.GetAtPath(animationModel) as ModelImporter;
            if(animationImporter)
            {
                animationImporter.animationType=ModelImporterAnimationType.Generic;
                animationImporter.importAnimation=true;animationImporter.SaveAndReimport();
                var source=AssetDatabase.LoadAllAssetsAtPath(animationModel).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                string clipPath=Art+"/Animations/SpringBoar_Metamorphosis.anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(!clip) {clip=new AnimationClip();AssetDatabase.CreateAsset(clip,clipPath);}
                EditorUtility.CopySerialized(source,clip);clip.name="SpringBoar_Metamorphosis";
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Art+"/Animations/SpringBoar.controller");
                var machine=controller.layers[0].stateMachine;
                var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Metamorphosis");
                if(!state)state=machine.AddState("Metamorphosis");
                state.motion=clip;state.speed=clip.length;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;
                EditorUtility.SetDirty(clip);EditorUtility.SetDirty(controller);
            }
            string model=Art+"/Phase1/Models/SpringBoarPhase1.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=false;
            importer.SaveAndReimport();
            foreach(var suffix in new[]{"BaseColor","Normal"})
            {
                var texture=(TextureImporter)AssetImporter.GetAtPath(Art+"/Phase1/Textures/SpringBoarPhase1_"+suffix+".png");
                texture.maxTextureSize=2048;
                texture.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                texture.textureCompression=TextureImporterCompression.Compressed;
                texture.SaveAndReimport();
            }
            string matPath=Art+"/Phase1/SpringBoarPhase1.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!mat) { mat=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat,matPath); }
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Phase1/Textures/SpringBoarPhase1_BaseColor.png");
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Phase1/Textures/SpringBoarPhase1_Normal.png"));
            mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale",.3f); mat.SetFloat("_Glossiness",.18f);
            EditorUtility.SetDirty(mat);
            const string path="Assets/Prefabs/monster/SpringBoarBoss.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual=root.transform.Find("Visual");
                var phase=root.GetComponent<SpringBoarPhaseTransition>();
                if(!phase) phase=root.AddComponent<SpringBoarPhaseTransition>();
                if(!root.GetComponent<SpringBoarCinematic>())root.AddComponent<SpringBoarCinematic>();
                GameObject flesh=null,skeleton=null;
                foreach(Transform child in visual)
                {
                    if(child.name=="Phase1_ForestBoar") flesh=child.gameObject;
                    else if(child.GetComponentInChildren<Animator>(true)) skeleton=child.gameObject;
                }
                if(!skeleton) throw new System.InvalidOperationException("Original skeletal model not found");
                if(!flesh)
                {
                    flesh=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model));
                    flesh.name="Phase1_ForestBoar"; flesh.transform.SetParent(visual,false);
                }
                flesh.SetActive(true);
                skeleton.SetActive(false);
                foreach(var renderer in flesh.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial=mat;
                var animator=flesh.GetComponent<Animator>();
                if(!animator) animator=flesh.AddComponent<Animator>();
                animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art+"/Animations/SpringBoar.controller");
                animator.applyRootMotion=false;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var oldAnimator=skeleton.GetComponentInChildren<Animator>(true);
                oldAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var boss=root.GetComponent<SpringBoarBoss>();
                Link(boss,"animator",animator);
                Link(phase,"fleshBody",flesh); Link(phase,"skeletonBody",skeleton);
                Link(phase,"fleshAnimator",animator); Link(phase,"skeletonAnimator",oldAnimator);
                Link(phase,"phaseOne",boss); Link(phase,"attacks",root.GetComponentInChildren<SpringBoarVfx>(true));
                Link(phase,"peelingShader",Shader.Find("DogLife/Spring Boar Peeling"));
                string fxMatPath=Art+"/Phase1/MetamorphosisVfx.mat";
                var fxMat=AssetDatabase.LoadAssetAtPath<Material>(fxMatPath);
                if(!fxMat) {fxMat=new Material(Shader.Find("DogLife/Spring Metamorphosis VFX"));AssetDatabase.CreateAsset(fxMat,fxMatPath);}
                var fx=root.GetComponentInChildren<SpringBoarMetamorphosisVfx>(true);
                if(!fx)
                {
                    var fxObj=new GameObject("MetamorphosisFlowers");fxObj.transform.SetParent(root.transform,false);
                    fx=fxObj.AddComponent<SpringBoarMetamorphosisVfx>();
                }
                Link(fx,"material",fxMat);
                Link(fx,"chest",oldAnimator.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Chest"));
                Link(fx,"head",oldAnimator.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head"));
                var skull=oldAnimator.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head");
                foreach(int sign in new[]{-1,1})
                {
                    string name=sign<0?"MetamorphosisEyeLeft":"MetamorphosisEyeRight";
                    var eye=skull.Find(name);
                    if(!eye) {eye=new GameObject(name).transform;eye.SetParent(skull,false);}
                    eye.localPosition=sign<0?new Vector3(-.001040f,.000984f,.000638f):new Vector3(.001053f,.000981f,.000641f);
                    Link(fx,sign<0?"leftEye":"rightEye",eye);
                }
                Link(phase,"transformationVfx",fx);
                var phaseSettings=new SerializedObject(phase);
                phaseSettings.FindProperty("anticipation").floatValue=1.9f;
                phaseSettings.FindProperty("peelDuration").floatValue=1.9f;
                phaseSettings.FindProperty("recovery").floatValue=1.2f;
                phaseSettings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            SpringBoarPhaseTwoBuilder.Build();
            return "Both boss phases, transformation and phase-two animations linked.";
        }
    }
}
