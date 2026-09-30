#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 반복 경계의 자세와 접선 속도를 맞추며 원본은 ArtSource에 보관합니다.
    public static class FrozenKnightWalkLoopAuthoring
    {
        public static string Apply(string name,float blendSeconds)
        {
            string path="Assets/Art/FrozenKnight/Animations/"+name+".anim";
            string backup="ArtSource/FrozenKnight/"+name+"_BeforeLoop.anim";
            if(!File.Exists(backup))File.Copy(path,backup);
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            float end=clip.length,start=Mathf.Max(0,end-blendSeconds),epsilon=1f/120;
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var original=AnimationUtility.GetEditorCurve(clip,binding);
                float first=original.Evaluate(0),velocity=(original.Evaluate(epsilon)-first)/epsilon;
                float at=original.Evaluate(start),slope=(original.Evaluate(start+epsilon)-original.Evaluate(start-epsilon))/(2*epsilon);
                var keys=new System.Collections.Generic.List<Keyframe>();
                foreach(var key in original.keys)if(key.time<start)keys.Add(key);
                // 끝 구간만 Hermite 곡선으로 이어 다음 주기의 첫 움직임까지 연결합니다.
                keys.Add(new Keyframe(start,at,slope,slope));
                keys.Add(new Keyframe(end,first,velocity,velocity));
                if(keys.Count>0){var firstKey=keys[0];firstKey.inTangent=velocity;firstKey.outTangent=velocity;keys[0]=firstKey;}
                AnimationUtility.SetEditorCurve(clip,binding,new AnimationCurve(keys.ToArray()));
            }
            clip.EnsureQuaternionContinuity();
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();return name+" loop seam authored";
        }
    }
}
#endif
