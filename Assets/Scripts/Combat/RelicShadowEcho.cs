using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace DogLife
{
    // Only copies rendered geometry. No player logic, audio, hitboxes or extra Animator.
    [DefaultExecutionOrder(210)]
    public sealed class RelicShadowEcho : MonoBehaviour
    {
        Transform source; SkinnedMeshRenderer[] skins; Mesh[] meshes;
        Material material; MaterialPropertyBlock properties; MeshFilter[] attachments;
        Vector3 start, direction; Quaternion facing; float born;
        const float Lifetime=.42f;
        public static void Create(Transform player,Vector3 position,Vector3 forward)
        {
            var mat=Resources.Load<Material>("VFX/RelicShadowEcho");if(!mat)return;
            var go=new GameObject("Relic - shadow strike");var echo=go.AddComponent<RelicShadowEcho>();
            echo.source=player;echo.material=mat;echo.start=position;echo.direction=forward.normalized;
            echo.facing=Quaternion.LookRotation(echo.direction);echo.born=Time.time;
            var visible=new List<SkinnedMeshRenderer>();
            foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.enabled&&skin.sharedMesh)visible.Add(skin);
            echo.attachments=player.GetComponentsInChildren<MeshFilter>();
            echo.skins=visible.ToArray();echo.meshes=new Mesh[echo.skins.Length];
            for(int i=0;i<echo.meshes.Length;i++){echo.meshes[i]=new Mesh {name="Relic shadow pose"};echo.meshes[i].MarkDynamic();}
            echo.properties=new MaterialPropertyBlock();
        }
        void LateUpdate()
        {
            if(!source){Destroy(gameObject);return;}
            float age=Time.time-born;if(age>=Lifetime){Destroy(gameObject);return;}
            float alpha=Mathf.SmoothStep(0,.65f,age/.035f)*(1-Mathf.SmoothStep(.13f,Lifetime,age));
            properties.SetColor("_Color",new Color(.012f,.016f,.024f,alpha));
            Vector3 position=start+direction*(Mathf.Sin(Mathf.Clamp01(age/.28f)*Mathf.PI*.8f)*.38f);
            Quaternion turn=facing*Quaternion.Inverse(source.rotation);
            for(int i=0;i<skins.Length;i++)
            {
                var skin=skins[i];if(!skin)continue;
                // Reuse the live dagger attack pose; do not stretch or rescale the skin.
                skin.BakeMesh(meshes[i],false);
                var matrix=Matrix4x4.TRS(position+turn*(skin.transform.position-source.position),turn*skin.transform.rotation,Vector3.one);
                for(int sub=0;sub<meshes[i].subMeshCount;sub++)Graphics.DrawMesh(meshes[i],matrix,material,0,null,sub,properties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
            }
            foreach(var attachment in attachments)
            {
                if(!attachment||!attachment.sharedMesh||!attachment.gameObject.activeInHierarchy)continue;
                var renderer=attachment.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled)continue;
                var matrix=Matrix4x4.TRS(position,turn,Vector3.one)*Matrix4x4.Translate(-source.position)*attachment.transform.localToWorldMatrix;
                for(int sub=0;sub<attachment.sharedMesh.subMeshCount;sub++)Graphics.DrawMesh(attachment.sharedMesh,matrix,material,0,null,sub,properties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
            }
        }
        void OnDestroy(){if(meshes!=null)foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
