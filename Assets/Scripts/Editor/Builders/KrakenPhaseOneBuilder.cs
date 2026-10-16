using UnityEngine;
using UnityEditor;
namespace DogLife.EditorTools
{
    public static class KrakenPhaseOneBuilder
    {
        public static string Build()
        {
            const string path="Assets/Prefabs/Monster/Summer/Kraken/PhaseOneTentacle.prefab";
            var root=new GameObject("KrakenPhaseOneTentacle");
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monster/Summer/Kraken/Tentacle_NaturalRig.prefab"));visual.transform.SetParent(root.transform,false);
            root.AddComponent<CombatHealth>();var tentacle=root.AddComponent<KrakenPhaseTentacle>();var so=new SerializedObject(tentacle);so.FindProperty("visual").objectReferenceValue=visual.transform;so.FindProperty("idleClip").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Monster/Summer/Kraken/NaturalTentacle_Idle.anim");so.ApplyModifiedPropertiesWithoutUndo();
            foreach(var bone in visual.GetComponentsInChildren<Transform>())if(bone.name=="Tendril12"){var head=new GameObject("Head");head.transform.SetParent(bone,false);break;}
            var tentacleBar=root.AddComponent<SeasonEnemyHealthBar>();var barSettings=new SerializedObject(tentacleBar);barSettings.FindProperty("fillColor").colorValue=new Color(.08f,.62f,.7f,1);barSettings.FindProperty("highlightColor").colorValue=new Color(.65f,1,1,1);barSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
            const string bossPath="Assets/Prefabs/Monster/Summer/Kraken/Boss.prefab";
            var boss=PrefabUtility.LoadPrefabContents(bossPath);
            var phase=boss.GetComponent<KrakenPhaseOne>();if(!phase)phase=boss.AddComponent<KrakenPhaseOne>();var settings=new SerializedObject(phase);settings.FindProperty("tentaclePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<KrakenPhaseTentacle>();settings.FindProperty("warningMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Monster/Summer/Kraken/KrakenLivingWater.mat");settings.ApplyModifiedPropertiesWithoutUndo();
            var legacy=boss.GetComponent<KrakenBoss>();legacy.enabled=false;foreach(var v in boss.GetComponentsInChildren<KrakenVfx>(true))v.enabled=false;
            if(!boss.GetComponent<KrakenSequenceCamera>())boss.AddComponent<KrakenSequenceCamera>();if(!boss.GetComponent<KrakenWaterVolume>())boss.AddComponent<KrakenWaterVolume>();
            var bar=boss.GetComponent<SeasonEnemyHealthBar>();if(bar)bar.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(boss,bossPath);PrefabUtility.UnloadPrefabContents(boss);AssetDatabase.SaveAssets();return "Natural tentacle prefab and phase one installed in existing KrakenBoss prefab";
        }
    }
}
