#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 원본 동작을 보존하고 상체 전용 클립에는 기존 하체 자세를 합성합니다.
    public static class FrozenKnightImportedCastAuthoring
    {
        const string Source="Assets/VARCO3DImports/WinterBoss/",Output="Assets/Art/FrozenKnight/Animations/";
        public static string Build()
        {
            Bake("Winter_IceSword","IceSwordMounted","MountedIdle",true);
            Bake("Winter_IceWall","IceWallMounted","MountedIdle",true);
            // 서서 소환할 때도 원본의 다리 들기를 제외하여 양발 접지를 유지합니다.
            Bake("Winter_IceWall","IceWallFull","Idle",true);
            Bake("밀어내기","PushUpper","Idle",true);
            AssetDatabase.SaveAssets();return "Four imported cast clips built";
        }
        static void Bake(string sourceName,string name,string lowerName,bool upperOnly)
        {
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Source+sourceName+".anim");
            var lower=AssetDatabase.LoadAssetAtPath<AnimationClip>(Output+lowerName+".anim");
            var clip=Object.Instantiate(source);clip.name=name;clip.ClearCurves();
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
            {
                bool upper=binding.path.Contains("/Spine");
                // 루트 이동/회전은 AI에 맡기고, 상체 모션은 골반과 다리를 덮어쓰지 않습니다.
                bool preserve=binding.path=="skel/Root"||(upperOnly&&!upper);
                var curve=AnimationUtility.GetEditorCurve(source,binding);
                if(preserve)
                {
                    var rest=AnimationUtility.GetEditorCurve(lower,binding);
                    curve=AnimationCurve.Constant(0,source.length,rest!=null?rest.Evaluate(0):curve.Evaluate(0));
                }
                AnimationUtility.SetEditorCurve(clip,binding,curve);
            }
            AnimationUtility.SetAnimationEvents(clip,new AnimationEvent[0]);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
            clip.EnsureQuaternionContinuity();
            var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(Output+name+".anim");
            if(old){EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,Output+name+".anim");
        }
    }
}
#endif
