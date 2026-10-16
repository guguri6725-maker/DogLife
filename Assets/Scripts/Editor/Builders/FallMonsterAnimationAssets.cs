using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace DogLife.EditorTools
{
    // 씬 오브젝트를 만들지 않고 애니메이션·컨트롤러 에셋만 제작합니다.
    public static class FallMonsterAnimationAssets
    {
        public static string Build()
        {
            foreach(var name in new[]{"MyceliumSkeleton","Scarecrow","BoneCrow","LeafHedgehog"})
            {
                string dir="Assets/Animations/Monster/Autumn/"+name;
                Directory.CreateDirectory(dir);AssetDatabase.Refresh();
                string modelPath="Assets/Models/Monster/Autumn/"+name+"/"+name+".fbx";
                var sourceClips=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
                var controller=AnimatorController.CreateAnimatorControllerAtPath(dir+"/"+name+".controller");
                controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);
                controller.AddParameter("PhaseSpeed",AnimatorControllerParameterType.Float);
                var sm=controller.layers[0].stateMachine;
                foreach(string stateName in new[]{"Idle","Walk","AttackReady","Attack","Recovery"})
                {
                    var source=sourceClips.First(c=>c.name.EndsWith(name+"_"+stateName));
                    var clip=Object.Instantiate(source);clip.name=name+"_"+stateName;
                    var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=stateName=="Idle"||stateName=="Walk";AnimationUtility.SetAnimationClipSettings(clip,settings);
                    if(name=="MyceliumSkeleton"&&stateName=="Attack")RetargetAttack(clip,AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                    AssetDatabase.CreateAsset(clip,dir+"/"+clip.name+".anim");
                    var state=sm.AddState(stateName);state.motion=clip;
                    if(stateName!="Idle"&&stateName!="Walk"){state.speed=clip.length;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;}
                    if(stateName=="Idle")sm.defaultState=state;
                }
                var idle=sm.states.First(s=>s.state.name=="Idle").state;var walk=sm.states.First(s=>s.state.name=="Walk").state;
                var toWalk=idle.AddTransition(walk);toWalk.hasExitTime=false;toWalk.duration=.15f;toWalk.AddCondition(AnimatorConditionMode.If,0,"Moving");
                var toIdle=walk.AddTransition(idle);toIdle.hasExitTime=false;toIdle.duration=.15f;toIdle.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
            }
            AssetDatabase.SaveAssets();return "Four animation controllers saved";
        }
        static void RetargetAttack(AnimationClip output,GameObject model)
        {
            // 제공된 공격의 회전 변화량만 새 리그 기준 자세에 적용해 위치·체형 차이를 제거합니다.
            var original=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Monster/Autumn/MyceliumSkeleton/_Source/Fall_Skelleton_Attack.anim");
            if(!original)throw new System.InvalidOperationException("Missing supplied skeleton attack");
            var bindings=AnimationUtility.GetCurveBindings(original);
            foreach(var path in bindings.Where(b=>b.propertyName=="m_LocalRotation.w").Select(b=>b.path).Distinct())
            {
                var bone=model.transform.Find(path);if(!bone||path.EndsWith("Root")||path.EndsWith("Hips"))continue;
                var input=new AnimationCurve[4];var result=new AnimationCurve[4];var generated=new AnimationCurve[4];
                string[] axes={"x","y","z","w"};
                for(int j=0;j<4;j++){
                    var binding=EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+axes[j]);
                    input[j]=AnimationUtility.GetEditorCurve(original,binding);generated[j]=AnimationUtility.GetEditorCurve(output,binding);result[j]=new AnimationCurve();}
                if(input.Any(c=>c==null))continue;
                var first=Sample(input,.067f);Quaternion previous=bone.localRotation;
                float length=output.length;
                for(int i=0;i<=45;i++){
                    float t=i/45f;var delta=Quaternion.Inverse(first)*Sample(input,Mathf.Lerp(.067f,original.length,t));
                    var target=bone.localRotation*delta;
                    var basePose=generated.All(c=>c!=null)?Sample(generated,t*length):bone.localRotation;
                    var q=Quaternion.Slerp(basePose,target,.65f);
                    if(Quaternion.Dot(previous,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                    float[] values={q.x,q.y,q.z,q.w};for(int j=0;j<4;j++)result[j].AddKey(t*length,values[j]);
                }
                for(int j=0;j<4;j++)AnimationUtility.SetEditorCurve(output,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+axes[j]),result[j]);
            }
            output.EnsureQuaternionContinuity();
        }
        static Quaternion Sample(AnimationCurve[] c,float t)=>new Quaternion(c[0].Evaluate(t),c[1].Evaluate(t),c[2].Evaluate(t),c[3].Evaluate(t)).normalized;
    }
}
