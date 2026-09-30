#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 기존 검을 잡은 자세를 보존하면서 반동·무릎 굽힘을 베이크한 짧은 후퇴 클립입니다.
    public static class FrozenKnightBackDashAuthoring
    {
        const string Folder="Assets/Art/FrozenKnight/Animations/";
        public static string Build()
        {
            BuildClip("Idle","BackDash",false,false);
            BuildClip("MountedIdle","MountedBackDash",true,false);
            BuildClip("Horse_Idle","Horse_BackDash",true,true);
            var mesh=Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx"));mesh.name="IceWallRectangle";
            const string path="Assets/Art/FrozenKnight/Models/IceWallRectangle.asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);
            AssetDatabase.SaveAssets();return "Authored three back-dash clips and rectangular wall mesh";
        }
        static void BuildClip(string sourceName,string name,bool mounted,bool horse)
        {
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+sourceName+".anim");
            var clip=new AnimationClip{name=name,frameRate=60};var bindings=AnimationUtility.GetCurveBindings(source);
            foreach(var binding in bindings)
            {
                if(binding.propertyName.StartsWith("m_LocalRotation"))continue;
                var sourceCurve=AnimationUtility.GetEditorCurve(source,binding);float value=sourceCurve.Evaluate(0);
                var curve=new AnimationCurve();
                for(int i=0;i<=18;i++)
                {
                    float u=i/18f,pulse=Mathf.Sin(u*Mathf.PI);float v=value;
                    if(binding.propertyName=="m_LocalPosition.y"&&binding.path==(horse?"IceHorseRig/Root":"skel/Root/Hips"))v+=pulse*(horse?.06f:mounted?-.025f:-.09f);
                    curve.AddKey(u*.3f,v);
                }
                AnimationUtility.SetEditorCurve(clip,binding,curve);
            }
            foreach(var binding in bindings.Where(b=>b.propertyName=="m_LocalRotation.x"))
            {
                var values=new float[4];for(int c=0;c<4;c++){var b=binding;b.propertyName="m_LocalRotation."+"xyzw"[c];values[c]=AnimationUtility.GetEditorCurve(source,b).Evaluate(0);}
                Quaternion rest=new Quaternion(values[0],values[1],values[2],values[3]);var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                for(int i=0;i<=18;i++)
                {
                    float u=i/18f,pulse=Mathf.Sin(u*Mathf.PI),angle=0;string bone=binding.path.Split('/').Last();
                    if(horse){if(bone.Contains("Upper"))angle=18*pulse;if(bone.Contains("Lower"))angle=-22*pulse;if(bone=="Chest")angle=-5*pulse;}
                    else{if(bone=="Spine")angle=-12*pulse;if(!mounted&&bone.EndsWith("UpLeg"))angle=-24*pulse;if(!mounted&&(bone=="LeftLeg"||bone=="RightLeg"))angle=35*pulse;}
                    Quaternion q=rest*Quaternion.Euler(angle,0,0);for(int c=0;c<4;c++)curves[c].AddKey(u*.3f,q[c]);
                }
                for(int c=0;c<4;c++){var b=binding;b.propertyName="m_LocalRotation."+"xyzw"[c];AnimationUtility.SetEditorCurve(clip,b,curves[c]);}
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+name+".anim");if(existing){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,Folder+name+".anim");
        }
    }
}
#endif
