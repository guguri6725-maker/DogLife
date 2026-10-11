#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
namespace DogLife
{
    // Manual Editor-only regression runner; never added to a shipping scene/prefab.
    public sealed class KrakenFloodRegression:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
        readonly List<string> report=new List<string>(),sounds=new List<string>();
        public bool Finished{get;private set;}
        void Heard(string key){sounds.Add(key);}
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);System.IO.Directory.CreateDirectory("Builds/KrakenValidation");System.IO.File.WriteAllLines("Builds/KrakenValidation/flood-regression.txt",report);Debug.Log(report.Last());}
        bool Playing(string key)=>FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Any(s=>s.isPlaying&&s.clip&&s.clip.name==key);
        void Shot(string name)
        {
            var c=Camera.main;var rt=RenderTexture.GetTemporary(1280,720,24);var previous=c.targetTexture;var active=RenderTexture.active;
            c.targetTexture=rt;c.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes("Builds/KrakenValidation/"+name+".png",tex.EncodeToPNG());Destroy(tex);c.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
        }
        IEnumerator Start()
        {
            var stream=FindFirstObjectByType<ForestRoomStreaming>();while(!stream.Ready)yield return null;stream.StopAllCoroutines();stream.enabled=false;
            foreach(var enemy in FindObjectsByType<CombatHealth>(FindObjectsSortMode.None))if(!enemy.player)enemy.gameObject.SetActive(false);
            var player=FindFirstObjectByType<PlayerMotor>();var hp=player.GetComponent<CombatHealth>();hp.invulnerable=true;
            var window=FindFirstObjectByType<EconomyWindow>();var go=new GameObject("Shop audio regression");go.transform.position=player.transform.position;
            var shop=go.AddComponent<EconomyStation>();shop.catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/Data/Weapons/WeaponCatalog.asset");shop.price=0;shop.displays=new[]{new GameObject("display").transform};shop.displays[0].SetParent(go.transform,false);yield return null;
            GameAudio.EffectStarted+=Heard;GameAudio.Play("SwordAttack",player.transform.position);GameAudio.Play("Synthesis_Loop",Vector3.zero,ui:true,loop:true);
            window.Open(shop);Check(!Playing("SwordAttack")&&!Playing("Synthesis_Loop"),"Shop clears previous combat one-shots and stale fusion loops");
            sounds.Clear();GameAudio.Play("SwordAttack",player.transform.position);Check(!sounds.Contains("SwordAttack"),"Paused gameplay cannot start world sounds");
            var relay=window.buyButtons[0].GetComponent<EconomyItemAudio>();var pointer=new PointerEventData(EventSystem.current){position=new Vector2(600,350)};
            float until=Time.unscaledTime+3;while(Time.unscaledTime<until){relay.OnPointerEnter(pointer);yield return null;}
            Check(sounds.Count(s=>s=="Shop_Item_Select")==1,"Stationary-pointer re-entry cannot repeat selection sound");
            sounds.Clear();yield return new WaitForSecondsRealtime(3);Check(sounds.Count==0&&!FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Any(s=>s.isPlaying),"Idle shop has no repeated events or playing effects after tails finish");
            window.Close();GameAudio.EffectStarted-=Heard;Destroy(go);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/SeasonRooms/Summer/Summer_Room04.unity",LoadSceneMode.Additive);
            var room=FindObjectsByType<ForestRoom>(FindObjectsSortMode.None).First(r=>r.gameObject.scene.name=="Summer_Room04");yield return room.PrepareGeometry();room.BeginEncounter(false);yield return null;yield return null;
            var boss=FindFirstObjectByType<KrakenPhaseOne>();player.readInput=false;var rb=player.GetComponent<Rigidbody>();rb.position=player.transform.position=new Vector3(room.entryX+7,1,0);rb.linearVelocity=Vector3.zero;room.BeginEncounter();
            float deadline=Time.time+30;while(boss.State!=KrakenPhaseOne.EncounterState.Combat&&Time.time<deadline)yield return null;
            foreach(var n in new[]{"nextSlam","nextCapture","nextSweep"})typeof(KrakenPhaseOne).GetField(n,F).SetValue(boss,Time.time+10000);
            Check(boss.Slots.Count==5&&boss.RequiredKills==5,"Actual summer boss spawns five slots and requires five kills");
            var a=boss.Slots[0];var survivor=boss.Slots[1];survivor.SetPose(KrakenPhaseTentacle.Pose.Ready,.3f);
            a.Health.Damage(10000);boss.TentacleKilled(a);a.Health.Damage(10000);
            Check(boss.Kills==1&&boss.State==KrakenPhaseOne.EncounterState.Combat,"One kill plus duplicate collider/death reports stays at 1/5");
            Check(survivor.State==KrakenPhaseTentacle.Pose.Ready,"One death does not reset another tentacle's attack");survivor.SetPose(KrakenPhaseTentacle.Pose.Idle,0);
            var old=a.transform.position;yield return new WaitForSeconds(7);
            Check(a.Alive&&a.Health.Current>0&&Vector3.Distance(old,a.transform.position)>3&&boss.Slots.Count==5,"Dead slot replaces at a new position; five-slot pool maintained");
            // Kill the replacement too: cumulative defeats, not five unique pool objects.
            a.Health.Damage(10000);Check(boss.Kills==2&&boss.State==KrakenPhaseOne.EncounterState.Combat,"Replacement counts as second kill and does not flood");
            boss.Slots[1].Health.Damage(10000);Check(boss.Kills==3&&boss.State==KrakenPhaseOne.EncounterState.Combat,"Third kill remains phase one");
            boss.Slots[2].Health.Damage(10000);Check(boss.Kills==4&&boss.State==KrakenPhaseOne.EncounterState.Combat,"Fourth kill remains phase one");
            boss.Slots[3].Health.Damage(10000);Check(boss.Kills==5&&boss.State==KrakenPhaseOne.EncounterState.Transition,"Only fifth kill begins flood transition");
            var water=boss.GetComponent<KrakenWaterVolume>();yield return new WaitForSeconds(6);Shot("flood-early");float y=water.Height;Vector3 cam=Camera.main.transform.position;
            yield return new WaitForSeconds(5);Shot("flood-middle");Check(water.Height>y&&Vector3.Distance(cam,Camera.main.transform.position)>2,"Water and Cinemachine shot rise/move continuously");
            Check(water.SurfaceBounds.min.z<=-26&&water.SurfaceBounds.max.z>=26&&water.SurfaceBounds.min.x<room.entryX&&water.SurfaceBounds.max.x>room.exitX,"Flood covers full original room water plus wall overlap on every edge");
            yield return new WaitForSeconds(7);Shot("flood-underwater");Check(boss.State==KrakenPhaseOne.EncounterState.Underwater&&player.GetComponent<PlayerWaterMovement>().Submerged,"Flood finishes in underwater movement state");
            Check(Camera.main.transform.position.y<water.Height,"Camera returns underwater without exposing a cut surface edge");Finished=true;
        }
        void OnDestroy(){GameAudio.EffectStarted-=Heard;}
    }
}
#endif
