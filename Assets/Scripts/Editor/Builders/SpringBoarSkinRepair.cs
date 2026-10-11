using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    public static class SpringBoarSkinRepair
    {
        public static string Build()
        {
            var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/monster/SpringBoarBoss.prefab");
            try
            {
                var so=new SerializedObject(prefab.GetComponent<SpringBoarPhaseTransition>());
                var animator=(Animator)so.FindProperty("skeletonAnimator").objectReferenceValue;
                var skin=animator.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var source=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/SpringBoar/Models/SpringBoar.fbx").OfType<Mesh>().First(m=>m.vertexCount==118023);
                var mesh=Object.Instantiate(source);mesh.name="SpringBoar topology-smoothed skin";
                var vertices=mesh.vertices;int count=vertices.Length,bones=skin.bones.Length;
                var map=new int[count];var ids=new Dictionary<Vector3Int,int>();int groups=0;
                for(int i=0;i<count;i++)
                {
                    var v=vertices[i]*10000000f;var key=new Vector3Int(Mathf.RoundToInt(v.x),Mathf.RoundToInt(v.y),Mathf.RoundToInt(v.z));
                    if(!ids.TryGetValue(key,out map[i])){map[i]=groups++;ids[key]=map[i];}
                }
                var neighbours=new HashSet<int>[groups];for(int i=0;i<groups;i++)neighbours[i]=new HashSet<int>();
                var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)for(int j=0;j<3;j++){int a=map[triangles[i+j]],b=map[triangles[i+(j+1)%3]];if(a!=b){neighbours[a].Add(b);neighbours[b].Add(a);}}
                var w=new float[groups,bones];var n=new int[groups];var original=source.boneWeights;
                for(int i=0;i<count;i++){int g=map[i];n[g]++;var a=original[i];w[g,a.boneIndex0]+=a.weight0;w[g,a.boneIndex1]+=a.weight1;w[g,a.boneIndex2]+=a.weight2;w[g,a.boneIndex3]+=a.weight3;}
                for(int g=0;g<groups;g++)for(int b=0;b<bones;b++)w[g,b]/=n[g];
                var next=new float[groups,bones];
                for(int pass=0;pass<32;pass++)
                {
                    for(int g=0;g<groups;g++)for(int b=0;b<bones;b++)
                    {
                        float sum=0;foreach(int other in neighbours[g])sum+=w[other,b];
                        next[g,b]=neighbours[g].Count==0?w[g,b]:Mathf.Lerp(w[g,b],sum/neighbours[g].Count,.65f);
                    }
                    var swap=w;w=next;next=swap;
                }
                var weights=new BoneWeight[count];var sorted=new int[bones];
                for(int g=0;g<groups;g++)
                {
                    for(int b=0;b<bones;b++)sorted[b]=b;
                    System.Array.Sort(sorted,(a,b)=>w[g,b].CompareTo(w[g,a]));
                    float sum=w[g,sorted[0]]+w[g,sorted[1]]+w[g,sorted[2]]+w[g,sorted[3]];
                    next[g,0]=sorted[0];next[g,1]=sorted[1];next[g,2]=sorted[2];next[g,3]=sorted[3];next[g,4]=sum;
                }
                for(int i=0;i<count;i++){int g=map[i],a=(int)next[g,0],b=(int)next[g,1],c=(int)next[g,2],d=(int)next[g,3];float sum=next[g,4];weights[i]=new BoneWeight{boneIndex0=a,boneIndex1=b,boneIndex2=c,boneIndex3=d,weight0=w[g,a]/sum,weight1=w[g,b]/sum,weight2=w[g,c]/sum,weight3=w[g,d]/sum};}
                mesh.boneWeights=weights;
                const string path="Assets/Art/SpringBoar/Models/SpringBoar_RepairedSkin.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}else{AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
                skin.sharedMesh=saved;PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/monster/SpringBoarBoss.prefab");AssetDatabase.SaveAssets();
                return "Repaired connected skin weights without changing vertices, UVs or triangles";
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
    }
}
