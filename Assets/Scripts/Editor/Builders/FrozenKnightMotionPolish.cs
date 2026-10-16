#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 기존 리그의 클립만 다듬습니다. 씬 생성이나 실시간 관절 강제 보정은 하지 않습니다.
    public static class FrozenKnightMotionPolish
    {
        const string Folder="Assets/Animations/Monster/Winter/FrozenKnight/";
        static AnimationClip Load(string name)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+name+".anim");
        static Quaternion Rotation(AnimationClip clip,string path,float time)
        {
            var q=Quaternion.identity;
            for(int i=0;i<4;i++){var c=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[i]));if(c!=null)q[i]=c.Evaluate(time);}
            return q.normalized;
        }
        static float Ease(float start,float end,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,t));
        public static string Build()
        {
            var idle=Load("Idle");var seated=Load("MountedIdle");var mount=Load("Mount");var horseIdle=Load("Horse_Idle");var gallop=Load("Horse_Gallop");
            // 왼발을 먼저 딛고 몸을 들어 올린 다음 오른다리를 넘겨 안장에 앉습니다.
            foreach(var path in AnimationUtility.GetCurveBindings(idle).Where(b=>b.propertyName=="m_LocalRotation.x").Select(b=>b.path))
            {
                var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                for(int i=0;i<=60;i++)
                {
                    float t=i/60f,seat=Ease(.35f,.94f,t),step=Mathf.Sin(Ease(.05f,.57f,t)*Mathf.PI),swing=Mathf.Sin(Ease(.37f,.87f,t)*Mathf.PI);
                    Quaternion q=Quaternion.Slerp(Rotation(idle,path,0),Rotation(seated,path,0),seat);Vector3 extra=Vector3.zero;
                    if(path.EndsWith("LeftUpLeg"))extra=new Vector3(38*step,0,-8*step);
                    else if(path.EndsWith("/LeftLeg"))extra=new Vector3(-48*step,0,0);
                    else if(path.EndsWith("RightUpLeg"))extra=new Vector3(15*swing,0,42*swing);
                    else if(path.EndsWith("/RightLeg"))extra=new Vector3(-25*swing,0,0);
                    else if(path.EndsWith("/Spine"))extra=new Vector3(14*step,0,-9*step);
                    else if(path.EndsWith("LeftArm"))extra=new Vector3(-12*step,0,-28*step);
                    else if(path.EndsWith("LeftForeArm"))extra=new Vector3(0,22*step,0);
                    q*=Quaternion.Euler(extra);for(int k=0;k<4;k++)curves[k].AddKey(t,q[k]);
                }
                for(int k=0;k<4;k++)AnimationUtility.SetEditorCurve(mount,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[k]),curves[k]);
            }
            var rootY=new AnimationCurve();for(int i=0;i<=60;i++){float t=i/60f;rootY.AddKey(t,.5165f*(1-Ease(.2f,.85f,t)));}
            AnimationUtility.SetEditorCurve(mount,EditorCurveBinding.FloatCurve("skel/Root",typeof(Transform),"m_LocalPosition.y"),rootY);mount.EnsureQuaternionContinuity();EditorUtility.SetDirty(mount);
            // 앞다리와 뒷다리, 좌우 발의 박자를 어긋나게 해 도약과 착지를 구분합니다.
            float duration=gallop.length;
            foreach(var path in AnimationUtility.GetCurveBindings(horseIdle).Where(b=>b.propertyName=="m_LocalRotation.x").Select(b=>b.path))
            {
                string name=path.Split('/').Last();var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};Quaternion rest=Rotation(horseIdle,path,0);
                for(int i=0;i<=60;i++)
                {
                    float p=i/60f*Mathf.PI*2,angle=0;Vector3 extra=Vector3.zero;
                    bool front=name.StartsWith("Front"),hind=name.StartsWith("Hind");
                    if(front||hind)
                    {
                        float a=p+(hind?Mathf.PI*.92f:0)+(name.Contains("R")?.65f:0);
                        if(name.EndsWith("Upper"))angle=48*Mathf.Sin(a);
                        else if(name.EndsWith("Lower"))angle=-58*Mathf.Pow(Mathf.Max(0,Mathf.Sin(a+.45f)),1.5f);
                        else if(name.EndsWith("Hoof"))angle=19*Mathf.Sin(a-.45f);
                    }
                    else if(name=="Spine")angle=4.5f*Mathf.Sin(p-.25f);
                    else if(name=="Neck")angle=-5*Mathf.Sin(p-.4f);
                    else if(name=="Head")angle=2.5f*Mathf.Sin(p-.7f);
                    else if(name=="Tail")extra.z=9*Mathf.Sin(p-.8f);
                    extra.x=angle;var q=rest*Quaternion.Euler(extra);for(int k=0;k<4;k++)curves[k].AddKey(i/60f*duration,q[k]);
                }
                for(int k=0;k<4;k++)AnimationUtility.SetEditorCurve(gallop,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[k]),curves[k]);
            }
            gallop.EnsureQuaternionContinuity();EditorUtility.SetDirty(gallop);AssetDatabase.SaveAssets();return "Mount and horse gallop polished";
        }
    }
}
#endif
