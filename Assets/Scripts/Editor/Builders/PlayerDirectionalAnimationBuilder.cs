using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DogLife.EditorTools
{
    public static class PlayerDirectionalAnimationBuilder
    {
        const string Folder = "Assets/Animations/Player";
        static string Swap(string path) => path.Replace("Left", "__LEFT__").Replace("Right", "Left").Replace("__LEFT__", "Right");
        public static string Build()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/DogLight.controller");
            var state = controller.layers[0].stateMachine.defaultState;
            if (state.motion is BlendTree) throw new InvalidOperationException("Locomotion is already configured.");
            var idle = state.motion as AnimationClip;
            var names = new[]{"Front","Back","Right","Left","RightFront","RightBack","LeftFront","LeftBack"};
            var points = new[]{new Vector2(0,1),new Vector2(0,-1),new Vector2(1,0),new Vector2(-1,0),new Vector2(.7071068f,.7071068f),new Vector2(.7071068f,-.7071068f),new Vector2(-.7071068f,.7071068f),new Vector2(-.7071068f,-.7071068f)};
            var clips = new AnimationClip[8];
            for (int i=0;i<names.Length;i++)
            {
                bool mirror=names[i]=="Left";
                var source=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/DogLight/_Source/Walk_"+(mirror?"Right":names[i])+".anim");
                var clip=mirror?new AnimationClip():UnityEngine.Object.Instantiate(source);
                clip.name="Walk_"+names[i];clip.frameRate=source.frameRate;
                if(mirror)
                {
                    foreach(var binding in AnimationUtility.GetCurveBindings(source))
                    {
                        var curve=AnimationUtility.GetEditorCurve(source,binding);
                        bool negate=binding.propertyName=="m_LocalPosition.x" || binding.propertyName=="m_LocalRotation.y" || binding.propertyName=="m_LocalRotation.z";
                        if(negate)
                        {
                            var keys=curve.keys;
                            for(int k=0;k<keys.Length;k++){keys[k].value=-keys[k].value;keys[k].inTangent=-keys[k].inTangent;keys[k].outTangent=-keys[k].outTangent;}
                            curve.keys=keys;
                        }
                        var target=binding;target.path=Swap(binding.path);AnimationUtility.SetEditorCurve(clip,target,curve);
                    }
                    clip.EnsureQuaternionContinuity();
                }
                var settings=AnimationUtility.GetAnimationClipSettings(source);settings.loopTime=true;settings.loopBlend=true;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                // 플레이어의 skel은 100배 스케일을 사용하므로 USD 위치 곡선을 cm 리그 단위로 변환합니다.
                // USD → FBX: Y축 180도 기준 변환. 위치/쿼터니언의 X,Z 부호도 함께 바꿔야 합니다.
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    bool position=binding.propertyName.StartsWith("m_LocalPosition.");
                    bool rotation=binding.propertyName.StartsWith("m_LocalRotation.");
                    if(!position&&!rotation)continue;
                    float factor=position?.01f:1f;
                    if(binding.propertyName.EndsWith(".x")||binding.propertyName.EndsWith(".z"))factor=-factor;
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;
                    for(int k=0;k<keys.Length;k++){keys[k].value*=factor;keys[k].inTangent*=factor;keys[k].outTangent*=factor;}
                    curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);
                }
                clip.EnsureQuaternionContinuity();
                PlayerWalkLoopBuilder.Bake(clip);
                AssetDatabase.CreateAsset(clip,Folder+"/"+clip.name+".anim");clips[i]=clip;
            }
            foreach(string name in new[]{"MoveX","MoveY"})if(!controller.parameters.Any(p=>p.name==name))controller.AddParameter(name,AnimatorControllerParameterType.Float);
            var tree=new BlendTree{name="Locomotion_9Directions",blendType=BlendTreeType.SimpleDirectional2D,blendParameter="MoveX",blendParameterY="MoveY",useAutomaticThresholds=false};
            AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(idle,Vector2.zero);
            for(int i=0;i<8;i++)tree.AddChild(clips[i],points[i]);
            state.motion=tree;state.name="Locomotion";EditorUtility.SetDirty(controller);
            string path="Assets/Prefabs/Player.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            var driver=prefab.GetComponent<PlayerDirectionalAnimation>()??prefab.AddComponent<PlayerDirectionalAnimation>();
            driver.animator=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController==controller);
            driver.animator.applyRootMotion=false;
            PrefabUtility.SaveAsPrefabAsset(prefab,path);PrefabUtility.UnloadPrefabContents(prefab);
            // 씬에서 별도 모델을 오버라이드한 플레이어도 같은 컨트롤러에 연결합니다.
            foreach(var p in UnityEngine.Object.FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var d=p.GetComponent<PlayerDirectionalAnimation>()??p.gameObject.AddComponent<PlayerDirectionalAnimation>();
                d.animator=p.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController==controller);
                d.animator.applyRootMotion=false;EditorUtility.SetDirty(d);
                PrefabUtility.RecordPrefabInstancePropertyModifications(d);
                EditorSceneManager.MarkSceneDirty(p.gameObject.scene);
                EditorSceneManager.SaveScene(p.gameObject.scene);
            }
            AssetDatabase.SaveAssets();return "Eight movement clips + Idle; mirrored Left; prefab and active player connected.";
        }
    }
}
