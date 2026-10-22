using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
namespace DogLife.EditorTools {
 public static class MemoryCollarBuilder {
  public static string Build(){
   const string path="Assets/Resources/VFX/MemoryCollar/";
   var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);var colors=new Color[4096];
   for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;colors[y*64+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2));}
   tex.SetPixels(colors);tex.Apply();System.IO.File.WriteAllBytes(path+"SoulLight.png",tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path+"SoulLight.png");
   var soft=AssetImporter.GetAtPath(path+"SoulLight.png") as TextureImporter;soft.alphaIsTransparency=true;soft.mipmapEnabled=false;soft.wrapMode=TextureWrapMode.Clamp;soft.SaveAndReimport();
   var soul=AssetDatabase.LoadAssetAtPath<Material>(path+"Soul.mat");if(!soul){soul=new Material(Shader.Find("DogLife/Memory Soul"));AssetDatabase.CreateAsset(soul,path+"Soul.mat");}soul.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path+"SoulLight.png");EditorUtility.SetDirty(soul);
   var palette=new Dictionary<string,Color>{{"Memory_Leather",Color.white},{"Memory_Brass",new Color(.64f,.42f,.18f)},{"Memory_Thread",new Color(.65f,.46f,.29f)},{"Memory_Stone",new Color(.19f,.23f,.24f)},{"Memory_Glow",new Color(.15f,.65f,1)}};
   var materials=new Dictionary<string,Material>();
   foreach(var pair in palette){var m=AssetDatabase.LoadAssetAtPath<Material>(path+pair.Key+".mat");if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path+pair.Key+".mat");}m.color=pair.Value;m.SetFloat("_Glossiness",pair.Key=="Memory_Brass"?.42f:.18f);m.SetFloat("_Metallic",pair.Key=="Memory_Brass"?.75f:0);if(pair.Key=="Memory_Leather")m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/MemoryCollar/LeatherGrain.png");if(pair.Key=="Memory_Stone"){m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/MemoryCollar/StoneGrain.png");m.color=Color.white;}if(pair.Key=="Memory_Glow"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",pair.Value*1.8f);}EditorUtility.SetDirty(m);materials[pair.Key]=m;}
   var root=new GameObject("Nameless Memory Collar");
   try {
    var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/MemoryCollar/MemoryCollar.fbx"),root.transform);
    var floating=new GameObject("Floating collar").transform;floating.SetParent(root.transform,false);
    var ground=new GameObject("Resting stone").transform;ground.SetParent(root.transform,false);
    foreach(var r in model.GetComponentsInChildren<MeshRenderer>()){
     var src=r.sharedMaterials;for(int i=0;i<src.Length;i++){foreach(var pair in materials)if(src[i].name.StartsWith(pair.Key)){src[i]=pair.Value;break;}}r.sharedMaterials=src;
     r.transform.SetParent(r.name.StartsWith("Ground_")?ground:floating,true);
    }
    Object.DestroyImmediate(model);
    var fx=root.AddComponent<MemoryCollarVfx>();fx.floatingCollar=floating;fx.restingStone=ground;fx.soulMaterial=soul;
    PrefabUtility.SaveAsPrefabAsset(root,path+"MemoryCollar.prefab");AssetDatabase.SaveAssets();return "Saved prefab with "+root.GetComponentsInChildren<MeshRenderer>().Length+" mesh renderers";
   } finally {Object.DestroyImmediate(root);}
  }
 }
}
