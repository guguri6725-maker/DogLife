#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
namespace DogLife
{
    public sealed class EconomyAudioValidation:MonoBehaviour
    {
        readonly List<string> events=new List<string>(),report=new List<string>();
        void Heard(string key){events.Add(key);}
        void Check(bool value,string label){report.Add((value?"PASS ":"FAIL ")+label);}
        bool Playing(string key)=>FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Any(s=>s.clip&&s.clip.name==key&&s.isPlaying);
        IEnumerator Start()
        {
            var streaming=FindFirstObjectByType<ForestRoomStreaming>();if(streaming)while(!streaming.Ready)yield return null;
            var window=FindFirstObjectByType<EconomyWindow>();var inv=window.wallet.inventory;var hp=inv.GetComponent<CombatHealth>();hp.invulnerable=true;
            var settings=GameAudio.Current;GameAudio.EffectStarted+=Heard;
            var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/Data/Weapons/WeaponCatalog.asset");
            var go=new GameObject("Audio validation shop");go.transform.position=inv.transform.position;var shop=go.AddComponent<EconomyStation>();shop.catalog=catalog;shop.price=0;shop.displays=new[]{new GameObject("display").transform};shop.displays[0].SetParent(go.transform,false);
            yield return null;
            Check(Resources.LoadAll<AudioClip>("Audio/UI").Length==13,"13 edited UI clips imported");
            window.Open(shop);Check(events.Contains("UI_Window_Open")&&Time.timeScale==0,"Shop opening sound starts on open while gameplay paused");
            events.Clear();var button=window.buyButtons[0];var relay=button.GetComponent<EconomyItemAudio>();var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};relay.OnPointerDown(pointer);window.Buy(0);relay.OnPointerClick(pointer);
            Check(events.Count(k=>k=="Shop_Purchase")==1&&!events.Contains("UI_Action_Denied"),"Purchase succeeds with one success sound and no false denial");
            yield return new WaitForSecondsRealtime(.1f);events.Clear();window.Buy(0);Check(events.Contains("UI_Action_Denied"),"Invalid purchase gives denial sound");window.Close();
            var weapon=catalog.weapons.First(w=>w&&w.weaponForm==InventoryWeapon.WeaponForm.Basic&&w.sameWeaponEvolution);
            var seq=Instantiate(window.fusionSequencePrefab,inv.transform.position+Vector3.forward*4,Quaternion.identity);seq.duration=2;
            events.Clear();Time.timeScale=0;seq.Begin(weapon,weapon,weapon.sameWeaponEvolution);Check(events.Count(k=>k=="Synthesis_Start")==1,"Fusion start fires once at Begin");
            yield return new WaitForSecondsRealtime(.65f);Check(Playing("Synthesis_Loop"),"Fusion loop follows unscaled material-gather timing");
            GameAudio.Preview(new AudioPreferences{master=1,music=1,effects=0});Check(FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Where(s=>s.isPlaying).All(s=>s.volume==0),"Effects slider mutes active UI sounds");GameAudio.Preview(settings);
            yield return new WaitForSecondsRealtime(1.05f);Check(events.Count(k=>k=="Synthesis_Result")==1&&!Playing("Synthesis_Loop"),"Result reveal fires once and stops loop at 80 percent");seq.Skip();seq.Skip();Check(events.Count(k=>k=="Synthesis_Result")==1,"Repeated skip cannot duplicate result sound");seq.Release();Destroy(seq.gameObject);
            yield return new WaitForSecondsRealtime(.15f);seq=Instantiate(window.fusionSequencePrefab,inv.transform.position+Vector3.forward*4,Quaternion.identity);seq.duration=2;seq.Begin(weapon,weapon,weapon.sameWeaponEvolution);yield return new WaitForSecondsRealtime(.6f);seq.Release();Check(!Playing("Synthesis_Loop"),"Closing during fusion stops loop immediately");Destroy(seq.gameObject);
            yield return new WaitForSecondsRealtime(.1f);events.Clear();seq=Instantiate(window.fusionSequencePrefab,inv.transform.position+Vector3.forward*4,Quaternion.identity);seq.Begin(weapon,weapon,weapon.sameWeaponEvolution);seq.Skip();Check(events.Count(k=>k=="Synthesis_Result")==1&&!Playing("Synthesis_Loop"),"Early skip reveals result without starting a stray loop");seq.Release();Destroy(seq.gameObject);
            Time.timeScale=1;GameAudio.Preview(settings);GameAudio.EffectStarted-=Heard;Destroy(go);hp.invulnerable=false;
            System.IO.File.WriteAllLines("Builds/AudioValidation/economy-runtime-checks.txt",report);Debug.Log(string.Join("\n",report));Destroy(gameObject);
        }
    }
}
#endif
