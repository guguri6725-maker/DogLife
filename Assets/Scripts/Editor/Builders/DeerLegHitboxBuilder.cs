using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    public static class DeerLegHitboxBuilder
    {
        public static void Apply(GameObject root)
        {
            float scale=root.transform.lossyScale.x;
            foreach(var hoof in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("Hoof")).ToArray())
            {
                var old=hoof.Find("HoofHitCollider");if(old)Object.DestroyImmediate(old.gameObject);
                var foot=new GameObject("HoofHitCollider");foot.layer=root.layer;
                foot.transform.SetParent(hoof,false);foot.transform.localScale=Vector3.one*(scale/hoof.lossyScale.x);
                foot.AddComponent<SphereCollider>().radius=.12f;
                var cannon=hoof.parent;old=cannon.Find("LowerLegHitCollider");if(old)Object.DestroyImmediate(old.gameObject);
                var leg=new GameObject("LowerLegHitCollider");leg.layer=root.layer;leg.transform.SetParent(cannon,false);
                leg.transform.localScale=Vector3.one*(scale/cannon.lossyScale.x);
                leg.transform.position=(hoof.position+cannon.position)*.5f;
                leg.transform.rotation=Quaternion.FromToRotation(Vector3.up,cannon.position-hoof.position);
                var capsule=leg.AddComponent<CapsuleCollider>();capsule.radius=.10f;capsule.height=Vector3.Distance(hoof.position,cannon.position)/scale+.18f;
            }
        }
        public static string Save()
        {
            const string path="Assets/Prefabs/Monster/Spring/FloralDeer/FloralDeer.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Apply(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            return "Four hoof and four lower leg non-trigger hit colliders saved; health remains shared.";
        }
    }
}
