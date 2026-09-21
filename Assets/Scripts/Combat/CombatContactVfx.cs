using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    // 작은 섬광과 꽃잎 모양 파편을 한 메시로 그립니다. 동시 표시 개수를 제한합니다.
    public sealed class CombatContactVfx : MonoBehaviour
    {
        static Material material;
        static int active;
        Mesh mesh;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock properties;
        float born,size;
        bool counted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Warmup(){active=0;material=Resources.Load<Material>("CombatHitFeedback");}
        public static void Spawn(Vector3 point,Vector3 direction,Color color,float power)
        {
            if(active>=16)return;
            if(!material)material=Resources.Load<Material>("CombatHitFeedback");
            if(!material)return;
            var go=new GameObject("Contact blossom spark");go.transform.position=point;
            var camera=Camera.main;
            Vector3 normal=camera?camera.transform.position-point:-direction;
            if(normal.sqrMagnitude<.001f)normal=Vector3.forward;
            go.transform.position+=normal.normalized*.12f;
            go.transform.rotation=Quaternion.LookRotation(normal);
            var fx=go.AddComponent<CombatContactVfx>();fx.Build(color,power);
        }
        void Build(Color color,float power)
        {
            active++;counted=true;born=Time.time;size=Mathf.Lerp(.65f,1.05f,Mathf.InverseLerp(.6f,1.8f,power));
            color=Color.Lerp(color,Color.white,.28f);color.a=1;
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*.2f;Vector3 ray=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),side=new Vector3(-ray.y,ray.x,0);
                int n=vertices.Count;float length=i%2==0?1:.6f;
                vertices.Add(Vector3.zero);vertices.Add(ray*.35f+side*.18f);vertices.Add(ray*length);vertices.Add(ray*.35f-side*.18f);
                colors.Add(new Color(1,.92f,.85f,1));colors.Add(color);colors.Add(new Color(color.r,color.g,color.b,.1f));colors.Add(color);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            mesh=new Mesh{name="Contact petals"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;meshRenderer=gameObject.AddComponent<MeshRenderer>();meshRenderer.sharedMaterial=material;
            meshRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;meshRenderer.receiveShadows=false;
            properties=new MaterialPropertyBlock();transform.localScale=Vector3.one*size;
        }
        void Update()
        {
            float t=(Time.time-born)/.38f;if(t>=1){Destroy(gameObject);return;}
            transform.localScale=Vector3.one*size*(.65f+1.8f*t);
            properties.SetColor("_Color",new Color(1.4f,1.4f,1.4f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,1,t))));meshRenderer.SetPropertyBlock(properties);
        }
        void OnDestroy(){if(counted)active--;if(mesh)Destroy(mesh);}
    }
}
