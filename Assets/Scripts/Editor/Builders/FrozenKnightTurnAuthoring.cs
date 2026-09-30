#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 기존 보행과 검 파지 자세를 유지한 채 좌우 디딤·기울임을 베이크합니다.
    public static class FrozenKnightTurnAuthoring
    {
        const string Folder="Assets/Art/FrozenKnight/Animations/";
        public static string Build()
        {
            foreach(int side in new[]{-1,1})
            {
                string suffix=side>0?"Right":"Left";
                BuildClip("Walk","FootTurn"+suffix,side,false,false);
                BuildClip("MountedIdle","MountedTurn"+suffix,side,true,false);
                BuildClip("Horse_Walk","Horse_Turn"+suffix,side,true,true);
            }
            AssetDatabase.SaveAssets();return "Six directional turn clips authored";
        }
        static void BuildClip(string sourceName,string name,int side,bool mounted,bool horse)
        {
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+sourceName+".anim");
            var clip=new AnimationClip{name=name,frameRate=60};var bindings=AnimationUtility.GetCurveBindings(source);
            const int samples=72;const float duration=1.1f;
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+(horse?"Horse_Idle":mounted?"MountedIdle":"Idle")+".anim");
            // 대기 자세를 기준으로 하체만 짧게 디딥니다. 원본 시간을 멈췄다 가속하는 왜곡은 사용하지 않습니다.
            foreach(var binding in bindings)
            {
                if(binding.propertyName.StartsWith("m_LocalRotation"))continue;
                var curve=new AnimationCurve();var src=AnimationUtility.GetEditorCurve(idle,binding)??AnimationUtility.GetEditorCurve(source,binding);
                for(int i=0;i<=samples;i++)curve.AddKey(i*duration/samples,src.Evaluate(i==samples?0:i*idle.length/samples));
                AnimationUtility.SetEditorCurve(clip,binding,curve);
            }
            foreach(var binding in bindings.Where(b=>b.propertyName=="m_LocalRotation.x"))
            {
                var original=new AnimationCurve[4];var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                for(int c=0;c<4;c++){var b=binding;b.propertyName="m_LocalRotation."+"xyzw"[c];original[c]=AnimationUtility.GetEditorCurve(source,b);}
                for(int i=0;i<=samples;i++)
                {
                    float u=i/(float)samples,time=i==samples?0:u*source.length;var q=new Quaternion(original[0].Evaluate(time),original[1].Evaluate(time),original[2].Evaluate(time),original[3].Evaluate(time));
                    string bone=binding.path.Split('/').Last();Vector3 offset=Vector3.zero;
                    var baseQ=new float[4];
                    for(int c=0;c<4;c++){var rb=binding;rb.propertyName="m_LocalRotation."+"xyzw"[c];var restCurve=AnimationUtility.GetEditorCurve(idle,rb);baseQ[c]=restCurve!=null?restCurve.Evaluate(0):original[c].Evaluate(0);}
                    var rest=new Quaternion(baseQ[0],baseQ[1],baseQ[2],baseQ[3]);
                    bool leg=horse?(bone.Contains("Upper")||bone.Contains("Lower")||bone.Contains("Hoof")):(binding.path.Contains("UpLeg"));
                    q=Quaternion.Slerp(rest,q,leg?.65f:0);
                    // 검/팔/안장 자세는 유지하고 몸통의 과한 비틀림과 좌우 흔들림을 없앱니다.
                    if(horse&&bone=="Neck")offset.y=side*3;
                    if(!horse&&bone=="Spine")offset=new Vector3(0,side*2,-side*(mounted?2:0));
                    q*=Quaternion.Euler(offset);for(int c=0;c<4;c++)curves[c].AddKey(u*duration,q[c]);
                }
                for(int c=0;c<4;c++){var b=binding;b.propertyName="m_LocalRotation."+"xyzw"[c];AnimationUtility.SetEditorCurve(clip,b,curves[c]);}
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            string path=Folder+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old){EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,path);
        }
    }
}
#endif
