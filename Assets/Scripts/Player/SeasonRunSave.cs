using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife {
public static class SeasonRunSave {
public const string Key="DogLife.SeasonCheckpoint.v1";
public static readonly string[] Scenes={"Assets/Scenes/Stage1_Spring.unity","Assets/Scenes/Stage3_Summer.unity","Assets/Scenes/Stage3_Fall.unity","Assets/Scenes/Stage4_Winter.unity"};
[Serializable] public sealed class Data {public int version=1;public string scene;public string[] weapons;public int equipped,gold,healingPotions;public int[] slots;public int activeSlot;public float health,maxHealth,karma,dashCooldown=1;public string[] relics;}
static Data pending, seasonEntry;
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){pending=null;seasonEntry=null;}
static void Write(Data d){PlayerPrefs.SetString(Key,JsonUtility.ToJson(d));PlayerPrefs.Save();}
public static void NewGame(){seasonEntry=null;RunEconomy.ClearCarry();pending=new Data{scene=Scenes[0],weapons=new[]{"dog_sword_basic"},equipped=0,health=5,maxHealth=5};Write(pending);}
public static bool PrepareLoad(out string scene){
scene=null;Data d;try{d=JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key,""));}catch{return false;}
var catalog=Resources.Load<RunWeaponCatalog>("RunWeaponCatalog");
if(d==null||d.version!=1||!Scenes.Contains(d.scene)||!Application.CanStreamedLevelBeLoaded(d.scene)||d.health<=0||d.maxHealth<d.health||d.weapons==null||!catalog||d.weapons.Any(id=>!catalog.Find(id)))return false;
// Older checkpoints may contain the previous season's remaining health.
d.health=5;d.maxHealth=5;RunEconomy.ClearCarry();pending=d;scene=d.scene;return true;
}
public static bool Apply(RunEconomy economy){
if(pending==null||economy.gameObject.scene.path!=pending.scene)return false;
var d=pending;var catalog=Resources.Load<RunWeaponCatalog>("RunWeaponCatalog");if(!catalog)return false;
economy.inventory.RestoreRunInventory(d.weapons.Select(catalog.Find),d.equipped,d.slots,d.activeSlot);economy.SetGold(d.gold);
// 기존 저장에는 수량이 없으므로 0개로 호환됩니다.
economy.SetHealingPotions(d.healingPotions);
var hp=economy.inventory.GetComponent<CombatHealth>();hp.RestoreCheckpoint(d.health,d.maxHealth);
var motor=economy.inventory.GetComponent<PlayerMotor>();if(motor)motor.dashCooldown=d.dashCooldown;
var karma=economy.inventory.GetComponent<PlayerKarma>();if(karma)karma.SetValue(d.karma);
RelicLoadout.Get(economy.inventory.gameObject).RestoreIds(d.relics);
pending=null;return true;
}
public static string Next(string scene){int i=Array.IndexOf(Scenes,scene);return i>=0&&i<Scenes.Length-1?Scenes[i+1]:null;}
static Data Capture(RunEconomy economy,string scene){
var inventory=economy.inventory;int equipped=-1;
for(int i=0;i<inventory.OwnedWeapons.Count;i++)if(inventory.GetInstanceId(i)==inventory.EquippedInstanceId)equipped=i;
var karma=inventory.GetComponent<PlayerKarma>();var motor=inventory.GetComponent<PlayerMotor>();
return new Data{scene=scene,weapons=inventory.OwnedWeapons.Select(w=>w.weaponId).ToArray(),equipped=equipped,slots=inventory.SlotIndices(),activeSlot=inventory.ActiveSlot,gold=economy.Gold,healingPotions=economy.HealingPotions,health=5,maxHealth=5,karma=karma?karma.Value:0,dashCooldown=motor?motor.dashCooldown:1,relics=inventory.GetComponent<RelicLoadout>()?.Ids()??new string[0]};
}
// Keep an immutable entry snapshot separate from the next-season LOAD checkpoint.
public static void RecordEntry(RunEconomy economy){
if(!economy||!economy.inventory||!Scenes.Contains(economy.gameObject.scene.path))return;
economy.inventory.GetComponent<CombatHealth>().RestoreCheckpoint(5,5);
seasonEntry=Capture(economy,economy.gameObject.scene.path);
}
public static bool RestartSeason(string scene){
if(SeasonLoadingScreen.IsLoading||seasonEntry==null||seasonEntry.scene!=scene)return false;
RunEconomy.ClearCarry();pending=JsonUtility.FromJson<Data>(JsonUtility.ToJson(seasonEntry));Write(pending);
return SeasonLoadingScreen.Load(scene);
}
public static void SaveNext(StageOneDirector director){
string next=Next(director.gameObject.scene.path);var economy=RunEconomy.Instance;if(next==null||!economy||!economy.inventory)return;
Write(Capture(economy,next));
}
public static void ContinueAfterMemory(StageOneDirector director){
if(!Scenes.Contains(director.gameObject.scene.path))return;
string next=Next(director.gameObject.scene.path);
if(next!=null){MetaProgress.AddPawprints(20);SaveNext(director);string destination;if(PrepareLoad(out destination))SeasonLoadingScreen.Load(destination);}
else{MetaProgress.AddPawprints(100);PlayerPrefs.DeleteKey(Key);PlayerPrefs.Save();RunEconomy.ClearCarry();SeasonLoadingScreen.Load("Assets/Scenes/Title.unity");}
}
}}
