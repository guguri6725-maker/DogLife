#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 편집 중 클립에 자세를 굽습니다. 게임 실행 중 관절을 강제로 돌리지 않습니다.
    public static class FrozenKnightPoseRefinement
    {
        const string Folder="Assets/Animations/Monster/Winter/FrozenKnight/";
        static AnimationClip Load(string name)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+name+".anim");
        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static Transform Bone(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static void Sample(GameObject root,AnimationClip clip,float time)
        {
            AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(root,clip,time);AnimationMode.EndSampling();
        }
        static void PointFoot(Transform root,string side,Vector3 direction,float weight)
        {
            var foot=Bone(root,side+"Foot");var toe=Bone(root,side+"ToeBase");
            var corrected=Quaternion.FromToRotation(toe.position-foot.position,root.TransformDirection(direction))*foot.rotation;
            foot.rotation=Quaternion.Slerp(foot.rotation,corrected,weight);
        }
        // 두 뼈의 길이를 보존하면서 발바닥을 고정해 무릎이 실제로 바닥에 닿게 합니다.
        static void PlaceLeg(Transform root,string side,Vector3 localTarget)
        {
            var hip=Bone(root,side+"UpLeg");var knee=Bone(root,side+"Leg");var ankle=Bone(root,side+"Foot");
            Vector3 target=root.TransformPoint(localTarget),start=hip.position;
            float a=Vector3.Distance(start,knee.position),b=Vector3.Distance(knee.position,ankle.position);
            Vector3 axis=(target-start).normalized;float distance=Mathf.Clamp(Vector3.Distance(start,target),Mathf.Abs(a-b)+.0001f,a+b-.0001f);
            target=start+axis*distance;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            var bend=Vector3.ProjectOnPlane(root.TransformDirection(Vector3.back),axis).normalized;
            Vector3 joint=start+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            hip.rotation=Quaternion.FromToRotation(knee.position-start,joint-start)*hip.rotation;
            knee.rotation=Quaternion.FromToRotation(ankle.position-knee.position,target-knee.position)*knee.rotation;
        }
        static void SaveCurves(AnimationClip clip,Dictionary<EditorCurveBinding,AnimationCurve> curves)
        {
            foreach(var item in curves)AnimationUtility.SetEditorCurve(clip,item.Key,item.Value);
            clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);
        }
        static void Record(Dictionary<EditorCurveBinding,AnimationCurve> curves,Transform root,Transform bone,float time,bool position=false)
        {
            string path=AnimationUtility.CalculateTransformPath(bone,root);
            for(int k=0;k<(position?3:4);k++)
            {
                var binding=EditorCurveBinding.FloatCurve(path,typeof(Transform),(position?"m_LocalPosition.":"m_LocalRotation.")+"xyzw"[k]);
                if(!curves.TryGetValue(binding,out var curve))curves[binding]=curve=new AnimationCurve();
                curve.AddKey(time,position?bone.localPosition[k]:bone.localRotation[k]);
            }
        }
        static void BakeKneel(Transform rig,bool stand)
        {
            var idle=Load("Idle");var output=Load(stand?"StandUp":"Kneel");
            var bones=AnimationUtility.GetCurveBindings(idle).Where(b=>b.type==typeof(Transform)&&!string.IsNullOrEmpty(b.path)).Select(b=>rig.Find(b.path)).Where(t=>t!=null).Distinct().ToArray();
            Sample(rig.gameObject,Load("Plant"),Load("Plant").length);
            var released=bones.Where(b=>AnimationUtility.CalculateTransformPath(b,rig).Contains("Arm")||b.name.EndsWith("Shoulder")).ToDictionary(b=>b,b=>b.localRotation);
            var curves=new Dictionary<EditorCurveBinding,AnimationCurve>();float duration=stand?1.9f:2.2f;
            for(int i=0;i<=132;i++)
            {
                float time=i/132f,t=stand?1-Ease(0,1,time):time;Sample(rig.gameObject,idle,0);
                // 검을 놓은 손 자세를 이어받아 빈손으로 검을 잡는 자세가 끼어들지 않게 합니다.
                foreach(var arm in released)arm.Key.localRotation=Quaternion.Slerp(arm.Value,arm.Key.localRotation,stand?Ease(.65f,1,time):0);
                var root=Bone(rig,"Root");Vector3 left=rig.InverseTransformPoint(Bone(rig,"LeftFoot").position),right=rig.InverseTransformPoint(Bone(rig,"RightFoot").position);
                float drop=Ease(.12f,.8f,t),settle=Mathf.Sin(Mathf.InverseLerp(.72f,1,t)*Mathf.PI)*.008f;
                root.localPosition+=Vector3.down*(.28f*drop+settle);
                PlaceLeg(rig,"Left",Vector3.Lerp(left,new Vector3(.1f,.09f,-.30f),drop));
                PlaceLeg(rig,"Right",Vector3.Lerp(right,new Vector3(-.095f,.09f,.18f),drop));
                PointFoot(rig,"Left",new Vector3(.018f,-.018f,-.115f),drop);
                PointFoot(rig,"Right",new Vector3(-.018f,-.07f,.09f),drop);
                // 충격을 받아 가슴이 먼저 숙여지고, 무게가 한쪽 무릎에 실린 뒤 가라앉습니다.
                float bow=Ease(.06f,.74f,t),recoil=Mathf.Sin(Ease(0,.4f,t)*Mathf.PI);
                Bone(rig,"Spine").localRotation*=Quaternion.Euler(12*bow-5*recoil,0,-3*drop);
                Bone(rig,"Spine1").localRotation*=Quaternion.Euler(7*bow,0,0);
                Bone(rig,"Head").localRotation*=Quaternion.Euler(9*bow,0,0);
                foreach(var bone in bones){Record(curves,rig,bone,time*duration);Record(curves,rig,bone,time*duration,true);}
            }
            output.ClearCurves();SaveCurves(output,curves);
        }
        static void BakeFeet(Transform rig,AnimationClip clip,bool mount)
        {
            var curves=new Dictionary<EditorCurveBinding,AnimationCurve>();float duration=clip.length;
            for(int i=0;i<=120;i++)
            {
                float t=i/120f;Sample(rig.gameObject,clip,t*duration);
                // 안장에 앉은 뒤 발끝은 말의 정면을 향합니다. 종아리 회전이 발목까지 꺾지 않게 보정합니다.
                foreach(string side in new[]{"Left","Right"})
                {
                    PointFoot(rig,side,new Vector3(side=="Left"?.015f:-.015f,-.02f,-.115f),mount?Ease(.35f,.94f,t):1);
                    Record(curves,rig,Bone(rig,side+"Foot"),t*duration);
                }
            }
            SaveCurves(clip,curves);
        }
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required");
            var boss=UnityEngine.Object.FindFirstObjectByType<FrozenKnightBoss>();
            var rig=boss.GetComponentsInChildren<Animator>(true).First(a=>a.name=="Knight").transform;
            AnimationMode.StartAnimationMode();
            try
            {
                BakeKneel(rig,false);BakeKneel(rig,true);
                foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{Folder.TrimEnd('/')}))
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if(clip.name.StartsWith("Mounted"))BakeFeet(rig,clip,false);
                }
                BakeFeet(rig,Load("Mount"),true);
            }
            finally{AnimationMode.StopAnimationMode();}
            AssetDatabase.SaveAssets();return "Kneel, StandUp and mounted foot poses baked";
        }
        public static string BuildTransition()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required");
            var rig=UnityEngine.Object.FindFirstObjectByType<FrozenKnightBoss>().GetComponentsInChildren<Animator>(true).First(a=>a.name=="Knight").transform;
            AnimationMode.StartAnimationMode();
            try{BakeKneel(rig,false);BakeKneel(rig,true);}finally{AnimationMode.StopAnimationMode();}
            AssetDatabase.SaveAssets();return "Transition poses updated";
        }
    }
}
#endif
