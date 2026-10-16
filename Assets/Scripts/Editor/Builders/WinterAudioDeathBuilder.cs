using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
    public static class WinterAudioDeathBuilder
    {
        static readonly string[] Prefix={"Golem","SnowSpirit","IceWolf","Mammoth"};
        public static string Build()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Resources/Audio/Winter"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var s=importer.defaultSampleSettings;s.loadType=path.Contains("Wind")?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;s.compressionFormat=AudioCompressionFormat.Vorbis;s.quality=.8f;importer.defaultSampleSettings=s;importer.forceToMono=!path.Contains("Wind");importer.SaveAndReimport();
            }
            for(int kind=0;kind<4;kind++)
            {
                string name=WinterMonsterBuilder.Names[kind],path="Assets/Prefabs/Monster/Winter/"+name+"/"+name+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animator=root.GetComponentInChildren<Animator>();
                    var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Monster/Winter/"+name+"/Idle.anim");idle.SampleAnimation(animator.gameObject,0);
                    var sound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Winter/"+Prefix[kind]+"_Death.wav");
                    float duration=Mathf.Max(1.8f,sound.length);
                    string ap="Assets/Animations/Monster/Winter/"+name+"/Death.anim";
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ap);if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,ap);}clip.ClearCurves();clip.frameRate=60;clip.name=name+"_Death";
                    var bones=animator.GetComponentsInChildren<Transform>().Where(t=>t!=animator.transform).ToArray();
                    foreach(var bone in bones)
                    {
                        var pos=bone.localPosition;var rot=bone.localRotation;string binding=AnimationUtility.CalculateTransformPath(bone,animator.transform);
                        float fold=0,delay=0;
                        string b=bone.name;
                        if(kind<2)
                        {
                            if(b.Contains("Thigh"))fold=-58;if(b.Contains("Shin"))fold=100;if(b.Contains("Foot"))fold=-35;
                            if(b.Contains("UpperArm")){fold=-28;delay=.1f;}if(b.Contains("Forearm"))fold=20;
                            if(b=="Chest"){fold=28;delay=.14f;}if(b=="Head"){fold=24;delay=.22f;}
                        }
                        if(b=="Root")fold=kind==0?38:kind==1?25:8;
                        else
                        {
                            if(b.Contains("FrontUpper"))fold=-38;if(b.Contains("FrontLower"))fold=65;if(b.Contains("FrontFoot"))fold=-22;
                            if(b.Contains("RearUpper")){fold=34;delay=.12f;}if(b.Contains("RearLower")){fold=-55;delay=.12f;}if(b.Contains("RearFoot"))fold=16;
                            if(b=="Neck"){fold=13;delay=.22f;}if(b=="Head"){fold=11;delay=.26f;}if(b=="Trunk"){fold=15;delay=.28f;}
                        }
                        Vector3 axis=bone.parent.InverseTransformDirection(animator.transform.right).normalized;
                        var curves=new AnimationCurve[7];for(int c=0;c<7;c++)curves[c]=new AnimationCurve();
                        for(int frame=0;frame<=60;frame++)
                        {
                            float u=frame/60f;
                            // Hesitation, front support failure, body settling, then the head follows.
                            float k=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f+delay,.62f+delay,u));
                            float settle=Mathf.Sin(Mathf.InverseLerp(.64f,.86f,u)*Mathf.PI)*1.5f;
                            Quaternion q=Quaternion.AngleAxis(fold*k+(b=="Head"?settle:0),axis)*rot;
                            if(b=="Root"&&kind>=2){var rollAxis=bone.parent.InverseTransformDirection(animator.transform.forward).normalized;float roll=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.82f,u));q=Quaternion.AngleAxis((kind==2?68:38)*roll,rollAxis)*q;}
                            Vector3 p=pos;
                            if(b=="Root")
                            {
                                float drop=kind==0?.47f:kind==1?.38f:kind==2?.36f:.40f;
                                p+=bone.parent.InverseTransformVector(-animator.transform.up*drop*animator.transform.lossyScale.y)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.70f,u));
                            }
                            float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};for(int c=0;c<7;c++)curves[c].AddKey(u*duration,values[c]);
                        }
                        string[] props={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
                        for(int c=0;c<7;c++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(binding,typeof(Transform),props[c]),curves[c]);
                    }
                    clip.EnsureQuaternionContinuity();
                    // Bake a ground contact correction from the actual skinned vertices, not renderer bounds.
                    var baseBone=bones.First(b=>b.name=="Root");string basePath=AnimationUtility.CalculateTransformPath(baseBone,animator.transform);
                    var pc=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};var baked=new Mesh();
                    for(int frame=0;frame<=60;frame++)
                    {
                        float time=duration*frame/60f;clip.SampleAnimation(animator.gameObject,time);float bottom=float.MaxValue;
                        foreach(var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(baked,true);foreach(var v in baked.vertices)bottom=Mathf.Min(bottom,skin.transform.TransformPoint(v).y);}
                        var pos=baseBone.localPosition+baseBone.parent.InverseTransformVector(Vector3.up*(root.transform.position.y+.02f-bottom));
                        pc[0].AddKey(time,pos.x);pc[1].AddKey(time,pos.y);pc[2].AddKey(time,pos.z);
                    }
                    UnityEngine.Object.DestroyImmediate(baked);
                    for(int c=0;c<3;c++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(basePath,typeof(Transform),"m_LocalPosition."+"xyz"[c]),pc[c]);
                    var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
                    var ctrl=animator.runtimeAnimatorController as AnimatorController;var sm=ctrl.layers[0].stateMachine;var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Death")??sm.AddState("Death");state.motion=clip;state.speed=1;state.speedParameterActive=false;
                    var presentation=root.GetComponent<WinterMonsterPresentation>()??root.AddComponent<WinterMonsterPresentation>();presentation.deathAnimation=clip;
                    // Restore idle pose before saving; death poses belong only to the animation asset.
                    idle.SampleAnimation(animator.gameObject,0);
                    PrefabUtility.SaveAsPrefabAsset(root,path);EditorUtility.SetDirty(ctrl);EditorUtility.SetDirty(clip);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();return "Four Death clips and presentations saved; 26 audio clips configured.";
        }
    }
}

