#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 발굽의 접지/회수 궤적을 클립으로 굽는 도구입니다. 실제 이동은 기존 NavMesh가 담당합니다.
    public static class FrozenHorseWalkAuthoring
    {
        const string Folder="Assets/Art/FrozenKnight/Animations/";
        static Transform Bone(Transform rig,string name)=>rig.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static void Solve(Transform upper,Transform lower,Transform hoof,Vector3 target,Vector3 bend)
        {
            Vector3 start=upper.position;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,hoof.position);
            Vector3 axis=(target-start).normalized;float distance=Mathf.Clamp(Vector3.Distance(start,target),Mathf.Abs(a-b)+.0001f,a+b-.0001f);
            float along=(a*a-b*b+distance*distance)/(2*distance);
            Vector3 knee=start+axis*along+Vector3.ProjectOnPlane(bend,axis).normalized*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-start,knee-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hoof.position-lower.position,start+axis*distance-lower.position)*lower.rotation;
        }
        public static string Build()
        {
            if(EditorApplication.isPlaying)return "Requires edit mode";
            var rig=Object.FindFirstObjectByType<FrozenKnightBoss>().GetComponentsInChildren<Animator>(true).First(a=>a.name=="IceHorse").transform;
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"Horse_Idle.anim");
            var curves=new Dictionary<EditorCurveBinding,AnimationCurve>();
            var bindings=AnimationUtility.GetCurveBindings(idle).Where(b=>b.type==typeof(Transform)).ToArray();
            var bones=bindings.Select(b=>rig.Find(b.path)).Where(t=>t!=null).Distinct().ToArray();
            string[] legs={"FrontL","FrontR","HindL","HindR"};float[] phases={0,.5f,.75f,.25f};
            AnimationMode.StartAnimationMode();
            try
            {
                for(int i=0;i<=96;i++)
                {
                    float t=i/96f;
                    AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(rig.gameObject,idle,0);AnimationMode.EndSampling();
                    var hoofRotations=legs.Select(n=>Bone(rig,n+"Hoof").rotation).ToArray();
                    var root=Bone(rig,"Root");root.position+=rig.TransformVector(Vector3.up*(-.055f+.004f*Mathf.Cos(t*Mathf.PI*4)));
                    Bone(rig,"Spine").localRotation*=Quaternion.Euler(1.5f*Mathf.Sin(t*Mathf.PI*2),0,0);
                    Bone(rig,"Neck").localRotation*=Quaternion.Euler(-2.5f*Mathf.Sin(t*Mathf.PI*2),0,0);
                    for(int j=0;j<4;j++)
                    {
                        float phase=Mathf.Repeat(t+phases[j],1),u;float z,y;
                        // 접지 구간은 일정 속도로 뒤로 밀고, 회수 구간에서만 발굽을 들어 앞으로 보냅니다.
                        if(phase<.64f){u=phase/.64f;z=Mathf.Lerp(.19f,-.19f,u);y=0;}
                        else{u=(phase-.64f)/.36f;z=Mathf.Lerp(-.19f,.19f,Mathf.SmoothStep(0,1,u));y=.085f*Mathf.Sin(u*Mathf.PI);}
                        var target=new Vector3(j%2==0?-.085f:.085f,.085f+y,(j<2?.23f:-.24f)+z);
                        var hoof=Bone(rig,legs[j]+"Hoof");
                        Solve(Bone(rig,legs[j]+"Upper"),Bone(rig,legs[j]+"Lower"),hoof,rig.TransformPoint(target),rig.TransformDirection(j<2?Vector3.back:Vector3.forward));
                        hoof.rotation=hoofRotations[j];
                    }
                    foreach(var bone in bones)
                    {
                        string path=AnimationUtility.CalculateTransformPath(bone,rig);
                        for(int k=0;k<7;k++)
                        {
                            bool position=k>=4;int axis=position?k-4:k;
                            var binding=EditorCurveBinding.FloatCurve(path,typeof(Transform),(position?"m_LocalPosition.":"m_LocalRotation.")+"xyzw"[axis]);
                            if(!curves.TryGetValue(binding,out var curve))curves[binding]=curve=new AnimationCurve();
                            curve.AddKey(t,position?bone.localPosition[axis]:bone.localRotation[axis]);
                        }
                    }
                }
            }
            finally{AnimationMode.StopAnimationMode();}
            string output=Folder+"Horse_Walk.anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,output);}clip.name="Walk";clip.ClearCurves();clip.frameRate=60;
            foreach(var c in curves)AnimationUtility.SetEditorCurve(clip,c.Key,c.Value);
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();return "Four-beat Horse_Walk baked";
        }
    }
}
#endif
