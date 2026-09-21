using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    public static class SummerCoastalDecor
    {
        const string Art="Assets/Art/SeasonCrafted";
        public static Material Ground(int index)
        {
            SeasonCraftedArt.Init();string path=Art+"/Materials/SummerGround"+index+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("DogLife/Coastal Ground"));AssetDatabase.CreateAsset(m,path);}
            m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/SeasonAtlas.png");
            var grass=AssetDatabase.LoadAssetAtPath<Material>("Assets/MapDesigne/Spring/Materials/Spring_Room01_TexturedGround.mat");
            m.SetTexture("_Grass",grass.mainTexture);m.SetFloat("_Summer",.15f+index*.14f);EditorUtility.SetDirty(m);return m;
        }
        static bool Fits(Bounds b,float x,float plus,float minus,int index)
        {
            Vector2[] p={new Vector2(x,5),new Vector2(x+8,plus),new Vector2(x+30,plus),new Vector2(x+38.4f,5),new Vector2(x+38.4f,-5),new Vector2(x+30,-minus),new Vector2(x+8,-minus),new Vector2(x,-5)};
            foreach(float xx in new[]{b.min.x,b.max.x})foreach(float zz in new[]{b.min.z,b.max.z})for(int j=0;j<8;j++){
                var a=p[j];var d=p[(j+1)%8]-a;var v=new Vector2(xx,zz)-a;if(d.x*v.y-d.y*v.x>-.95f*d.magnitude)return false;
            }
            if(b.min.z<3.4f&&b.max.z>-3.4f)return false;
            if((index==1&&b.max.z>0||index==5&&b.min.z<0)&&b.min.x<x+24&&b.max.x>x+14.4f)return false;
            return true;
        }
        static bool Overlap(Bounds a,Bounds b){return a.min.x<b.max.x+.35f&&a.max.x>b.min.x-.35f&&a.min.z<b.max.z+.35f&&a.max.z>b.min.z-.35f;}
        public static void Build(Transform decor,Transform shore,int index,float x,float plus,float minus)
        {
            SeasonCraftedArt.Init();var occupied=new List<Bounds>();
            var names=new List<string>();for(int j=0;j<2+index/2;j++)names.Add("CoastalPalm_"+(j%2+1));
            names.Add("LimestoneCluster");names.Add(index<2?"AncientSnag":"CoastalDriftwood");
            if(index>=2)names.Add("CoastalRuinArch");
            if(index>=3){names.Add("BeachParasol");names.Add("BeachLounger");names.Add("BeachParasol");names.Add("BeachLounger");}
            if(index>=5){names.Add("CoastalRuinArch");names.Add("LimestoneCluster");}
            int serial=0;
            foreach(string name in names)
            {
                float size=name.Contains("Palm")?.9f:name=="AncientSnag"?.8f:name=="CoastalRuinArch"?.9f:1f;
                var preferred=new Vector3(x+10+(serial%3)*8,.25f,(serial%2==0?1:-1)*(8+serial%3*2));
                var go=SeasonCraftedArt.Model(name,decor,preferred,size,index*23+serial*73,true);
                var relative=AutumnPlacementRepair.BoundsOf(go.transform);relative.center-=go.transform.position;
                float score=float.MaxValue;Vector3 target=preferred;
                for(float px=x+5;px<x+34;px+=.7f)for(float pz=-minus+2;pz<plus-2;pz+=.7f){
                    var p=new Vector3(px,go.transform.position.y,pz);var b=relative;b.center+=p;
                    if(!Fits(b,x,plus,minus,index)||occupied.Any(o=>Overlap(b,o)))continue;
                    float d=(p-preferred).sqrMagnitude;if(d<score){score=d;target=p;}
                }
                if(score==float.MaxValue)UnityEngine.Object.DestroyImmediate(go);
                else{go.transform.position=target;occupied.Add(AutumnPlacementRepair.BoundsOf(go.transform));}
                serial++;
            }
            var random=new System.Random(431+index);
            for(int j=0;j<15+index*5;j++){
                float px=x+7+(float)random.NextDouble()*24,pz=(j%2==0?1:-1)*(4+(float)random.NextDouble()*(Mathf.Min(plus,minus)-6));
                var pos=new Vector3(px,.26f,pz);var b=new Bounds(pos,new Vector3(.6f,.1f,.6f));
                if(!Fits(b,x,plus,minus,index)||occupied.Any(o=>Overlap(b,o)))continue;
                SeasonCraftedArt.Model("Seashell",shore,pos,.5f+(float)random.NextDouble()*.4f,random.Next(360),false);
            }
            Water(shore,index,x,minus);
        }
        static void Water(Transform parent,int index,float x,float width)
        {
            string path=Art+"/Materials/CoastalWater.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("DogLife/Coastal Water"));AssetDatabase.CreateAsset(m,path);}
            var mesh=new Mesh();var verts=new List<Vector3>();var tri=new List<int>();var uv=new List<Vector2>();
            int nx=24,nz=32;float near=-width-1.5f;
            for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++){verts.Add(new Vector3(x+i*44.4f/nx,-.7f,near-j*65f/nz));uv.Add(new Vector2(i/(float)nx,j/(float)nz));}
            for(int j=0;j<nz;j++)for(int i=0;i<nx;i++){int a=j*(nx+1)+i;tri.AddRange(new[]{a,a+1,a+nx+1,a+1,a+nx+2,a+nx+1});}
            mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string mp=Art+"/SummerWater"+index+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(mp);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,mp);
            var go=new GameObject("Turquoise_Sea");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=m;
            // A sand bank joins the low seawall to the water; the playable floor remains level.
            var bank=GameObject.CreatePrimitive(PrimitiveType.Cube);bank.name="Sandy_Shore_Bank";bank.transform.SetParent(parent,false);bank.transform.position=new Vector3(x+22.2f,-.6f,-width-1);
            bank.transform.localScale=new Vector3(44.4f,.45f,4);bank.GetComponent<Renderer>().sharedMaterial=SeasonCraftedArt.Make("BeachSand",Color.white,1);UnityEngine.Object.DestroyImmediate(bank.GetComponent<Collider>());
        }
        public static string Audit(int index)
        {
            string path="Assets/Scenes/SeasonRooms/SummerJourney7/Summer_Room"+(index+1).ToString("00")+".unity";
            var s=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try{
                var room=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                foreach(Transform t in room.transform)if(t.name.StartsWith("Summer_"))t.gameObject.SetActive(true);
                float[] widths={16,17,18,21,20,22,24};float plus=widths[index],minus=plus-(index%3-1)*3;
                var props=room.transform.Find("Summer_Coastal_Decor").Cast<Transform>().ToArray();int overlaps=0,outside=0;
                for(int i=0;i<props.Length;i++){var b=AutumnPlacementRepair.BoundsOf(props[i]);if(!Fits(b,room.entryX,plus,minus,index))outside++;for(int j=0;j<i;j++)if(Overlap(b,AutumnPlacementRepair.BoundsOf(props[j])))overlaps++;}
                room.navigation.AddData();UnityEngine.AI.NavMeshHit a,bh;var nav=new UnityEngine.AI.NavMeshPath();
                bool main=UnityEngine.AI.NavMesh.SamplePosition(new Vector3(room.entryX+5,.3f,0),out a,2,-1)&&UnityEngine.AI.NavMesh.SamplePosition(new Vector3(room.entryX+33,.3f,0),out bh,2,-1)&&UnityEngine.AI.NavMesh.CalculatePath(a.position,bh.position,-1,nav)&&nav.status==UnityEngine.AI.NavMeshPathStatus.PathComplete;
                bool side=true;if(index==1||index==5){UnityEngine.AI.NavMeshHit h;float z=index==1?plus+7:-minus-7;side=UnityEngine.AI.NavMesh.SamplePosition(new Vector3(room.entryX+19.2f,.3f,z),out h,2,-1)&&UnityEngine.AI.NavMesh.CalculatePath(a.position,h.position,-1,nav)&&nav.status==UnityEngine.AI.NavMeshPathStatus.PathComplete;}
                return "Summer "+(index+1)+": props="+props.Length+" overlaps="+overlaps+" outside="+outside+" mainPath="+main+" sidePath="+side;
            }finally{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s,true);}
        }
    }
}
