using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DogLife.EditorTools
{
    public static class BlossomSkeletonVfxBuilder
    {
        [MenuItem("DogLife/Monsters/Update Blossom Slash VFX")]
        public static void Build()
        {
            const string path="Assets/Prefabs/monster/BlossomSkeleton.prefab";
            const string folder="Assets/Art/Enemies/BlossomSkeleton/";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"Attack.anim");
            var points=new Vector3[100];var grips=new Vector3[100];Bounds bounds=default;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var enemy=root.GetComponent<BlossomSkeletonEnemy>();var animator=enemy.modelAnimator;
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();bounds=skin.localBounds;
                int handIndex=System.Array.FindIndex(skin.bones,b=>b.name=="RightHand");var hand=skin.bones[handIndex];
                var mesh=skin.sharedMesh;var weights=mesh.boneWeights;var vertices=mesh.vertices;
                Vector3 tip=Vector3.zero;float longest=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    if(weights[i].boneIndex0!=handIndex||weights[i].weight0<.99f)continue;
                    Vector3 local=mesh.bindposes[handIndex].MultiplyPoint3x4(vertices[i]);
                    if(local.sqrMagnitude>longest){tip=local;longest=local.sqrMagnitude;}
                }
                var baked=new Mesh();
                try{for(int i=0;i<100;i++)
                {
                    clip.SampleAnimation(animator.gameObject,clip.length*i/99f);
                    points[i]=animator.transform.InverseTransformPoint(hand.TransformPoint(tip));
                    grips[i]=animator.transform.InverseTransformPoint(hand.position);
                    skin.BakeMesh(baked);var scale=skin.transform.lossyScale;var b=baked.bounds;
                    bounds.Encapsulate(new Vector3(b.min.x/scale.x,b.min.y/scale.y,b.min.z/scale.z));
                    bounds.Encapsulate(new Vector3(b.max.x/scale.x,b.max.y/scale.y,b.max.z/scale.z));
                }}finally{Object.DestroyImmediate(baked);}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"BlossomSlash.mat");
            if(!material){material=new Material(Shader.Find("Sprites/Default")){name="BlossomSlash"};AssetDatabase.CreateAsset(material,folder+"BlossomSlash.mat");}
            root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var enemy=root.GetComponent<BlossomSkeletonEnemy>();enemy.hitTime=1.25f;enemy.attackDuration=clip.length;
                var vfx=root.GetComponentInChildren<BlossomSkeletonAttackVfx>(true);
                if(!vfx){var child=new GameObject("BlossomAttackVFX");child.transform.SetParent(root.transform,false);vfx=child.AddComponent<BlossomSkeletonAttackVfx>();}
                vfx.animator=enemy.modelAnimator;vfx.material=material;vfx.grips=grips;vfx.tips=points;vfx.duration=clip.length;vfx.impactTime=enemy.hitTime;
                root.GetComponentInChildren<SkinnedMeshRenderer>().localBounds=bounds;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
    }
}
