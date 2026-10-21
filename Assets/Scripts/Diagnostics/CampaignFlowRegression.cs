#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
namespace DogLife
{
    // Explicit, editor-only input/API regression. Save values are restored on exit.
    public sealed class CampaignFlowRegression:MonoBehaviour
    {
        public string Status{get;private set;}
        public bool Finished{get;private set;}
        public bool OnlyBow;
        readonly List<string> rows=new List<string>();
        readonly Dictionary<string,int> memories=new Dictionary<string,int>();
        string save;bool existed,backed;
        int shopSelectionSounds;
        void Heard(string key){if(key=="Shop_Item_Select")shopSelectionSounds++;}
        const string Folder="Builds/CampaignPlaytest";
        void Awake(){DontDestroyOnLoad(gameObject);}
        void Check(string name,bool ok){rows.Add((ok?"PASS ":"FAIL ")+name);File.WriteAllLines(Folder+"/flow-regression.txt",rows);}
        IEnumerator Wait(Func<bool> ready,float duration=60){float end=Time.realtimeSinceStartup+duration;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;}
        void Restore(){if(!backed)return;backed=false;if(existed)PlayerPrefs.SetString(SeasonRunSave.Key,save);else PlayerPrefs.DeleteKey(SeasonRunSave.Key);foreach(var e in memories){if(e.Value<0)PlayerPrefs.DeleteKey(e.Key);else PlayerPrefs.SetInt(e.Key,e.Value);}PlayerPrefs.Save();Time.timeScale=1;}
        void OnDestroy(){Restore();}
        IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return null;}
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);Application.runInBackground=true;
            existed=PlayerPrefs.HasKey(SeasonRunSave.Key);save=PlayerPrefs.GetString(SeasonRunSave.Key,"");
            foreach(var c in Resources.Load<StoryLibrary>("Story/StoryLibrary").chapters){string k="DogLife.Memories.v1."+c.id;memories[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetInt(k):-1;}backed=true;
            yield return Run();Status="Complete";Finished=true;Restore();
        }
        IEnumerator Run()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Title.unity");yield return null;
            FindFirstObjectByType<TitleScreen>().StartGame();yield return Wait(()=>StoryBook.IsOpen,10);StoryBook.Instance.Finish();
            yield return Wait(()=>ForestRoomStreaming.Instance&&ForestRoomStreaming.Instance.Ready&&!SeasonLoadingScreen.IsLoading,120);
            var stream=ForestRoomStreaming.Instance;var motor=stream.director.hud.motor;var hp=stream.director.hud.health;
            var inventory=motor.GetComponent<PlayerInventory>();var weapons=motor.GetComponent<PlayerWeapons>();var combat=motor.GetComponent<BasicWeaponCombat>();
            motor.readInput=false;weapons.readInput=false;hp.invulnerable=true;
            var camera=Camera.main.GetComponent<PlayerCamera>();camera.readInput=false;camera.yaw=90;camera.pitch=0;
            foreach(var enemy in stream.Current.Enemies)if(enemy)enemy.gameObject.SetActive(false);
            var startWeapons=inventory.OwnedWeapons.Select(x=>x.weaponId).ToArray();int startGold=RunEconomy.Instance.Gold;float startKarma=motor.GetComponent<PlayerKarma>().Value;
            var target=GameObject.CreatePrimitive(PrimitiveType.Capsule);target.name="Regression practice target";target.transform.position=motor.transform.position+Vector3.right*2;target.transform.localScale=new Vector3(1,1.6f,1);
            var health=target.AddComponent<CombatHealth>();health.practiceTarget=true;health.maxHealth=10000;health.Restore();
            foreach(var move in combat.movesets)
            {
                if(OnlyBow&&move.category!=InventoryWeapon.WeaponCategory.Bow)continue;
                Status="Real attack hit test: "+move.category;
                var item=AssetDatabase.FindAssets("t:InventoryWeapon").Select(g=>AssetDatabase.LoadAssetAtPath<InventoryWeapon>(AssetDatabase.GUIDToAssetPath(g))).First(x=>x.weaponCategory==move.category&&x.weaponForm==InventoryWeapon.WeaponForm.Basic);
                inventory.AddWeapon(item);inventory.Equip(item);combat.Cancel();yield return new WaitForSecondsRealtime(.15f);
                // Charging intentionally cancels while raw input is disabled (menus/cinematics).
                // Keep the ordinary input gate open for this real charge/release test.
                weapons.readInput=item.weaponCategory==InventoryWeapon.WeaponCategory.Bow;
                weapons.AimAt(target.transform.position);
                float before=health.Current,until=Time.realtimeSinceStartup+6;
                if(item.weaponCategory==InventoryWeapon.WeaponCategory.Bow)
                {
                    combat.PressAttack();
                    // 차징 상태를 유지한 채 앉기와 실제 물리 점프를 거친 뒤 화살을 발사합니다.
                    Check("Bow charge allows crouching",motor.SetCrouching(true)&&motor.Crouching&&combat.IsCharging);
                    motor.RequestJump();yield return new WaitForFixedUpdate();yield return null;
                    Check("Bow charge allows jump from crouch",!motor.Crouching&&motor.GetComponent<Rigidbody>().linearVelocity.y>0&&combat.IsCharging);
                    yield return Wait(()=>motor.Grounded,3);yield return new WaitForSecondsRealtime(.2f);
                    Check("Bow charge survives jump and landing",combat.IsCharging&&combat.ChargeAmount>.9f);
                    combat.ReleaseAttack();
                }
                while(Time.realtimeSinceStartup<until){weapons.AimAt(target.transform.position);if(item.weaponCategory!=InventoryWeapon.WeaponCategory.Bow){combat.PressAttack();combat.ReleaseAttack();}yield return null;}
                Check(move.category+" real animation/VFX attack damages practice target",health.Current<before);combat.Cancel();
                weapons.readInput=false;
            }
            if(OnlyBow){Destroy(target);yield break;}
            var sword=AssetDatabase.FindAssets("t:InventoryWeapon").Select(g=>AssetDatabase.LoadAssetAtPath<InventoryWeapon>(AssetDatabase.GUIDToAssetPath(g))).First(x=>x.weaponId=="dog_sword_basic");inventory.Equip(sword);yield return new WaitForSecondsRealtime(1.1f);
            motor.SetCrouching(true);float crouchedHeight=motor.GetComponent<CapsuleCollider>().height;
            combat.PressAttack();yield return null;Check("Crouched attack can dash cancel",motor.RequestDash()&&!combat.IsAttacking);
            // 대시가 공격을 취소해도 앉은 몸통과 입력 상태는 유지되어야 합니다.
            Check("Dash preserves crouched stance",motor.Crouching&&Mathf.Approximately(motor.GetComponent<CapsuleCollider>().height,crouchedHeight));
            combat.Cancel();motor.MoveInput=Vector2.zero;yield return new WaitForSecondsRealtime(.5f);
            Check("Crouch stays after dash until released",motor.Crouching);
            Check("Crouch can be released after dash",motor.SetCrouching(false)&&!motor.Crouching);Destroy(target);
            Status="Shop and synthesis transactions";
            var stationGo=new GameObject("Regression shop");stationGo.transform.position=motor.transform.position;
            var shop=stationGo.AddComponent<EconomyStation>();shop.catalog=AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/Data/Weapons/WeaponCatalog.asset");shop.price=10;shop.displays=new[]{new GameObject("display").transform};shop.displays[0].SetParent(stationGo.transform,false);
            RunEconomy.Instance.SetGold(1000);yield return null;
            var ui=EconomyWindow.Instance;GameAudio.EffectStarted+=Heard;ui.Open(shop);
            var ambience=FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().FirstOrDefault(s=>s.isPlaying&&s.clip&&s.clip.name=="Spring_Ambience");float audioTime=ambience?ambience.time:0;
            yield return new WaitForSecondsRealtime(2);
            Check("Stationary shop UI does not repeat selection sound",shopSelectionSounds<=1);GameAudio.EffectStarted-=Heard;
            Check("Exploration background continues while shop pauses gameplay",ambience&&ambience.isPlaying&&ambience.time>audioTime);
            int count=inventory.OwnedWeapons.Count;ui.Buy(0);Check("Shop purchase adds one item and spends exact price",inventory.OwnedWeapons.Count==count+1&&RunEconomy.Instance.Gold==990);
            ui.Buy(0);Check("Sold item cannot be charged twice",inventory.OwnedWeapons.Count==count+1&&RunEconomy.Instance.Gold==990);ui.Close();Destroy(stationGo);yield return null;
            var fusionGo=new GameObject("Regression fusion");fusionGo.transform.position=motor.transform.position;var fusion=fusionGo.AddComponent<EconomyStation>();fusion.kind=EconomyStation.StationKind.Fusion;fusion.catalog=shop?shop.catalog:AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/Data/Weapons/WeaponCatalog.asset");
            var material=AssetDatabase.FindAssets("t:InventoryWeapon").Select(g=>AssetDatabase.LoadAssetAtPath<InventoryWeapon>(AssetDatabase.GUIDToAssetPath(g))).First(x=>x.weaponForm==InventoryWeapon.WeaponForm.Basic&&x.sameWeaponEvolution);
            inventory.AddWeapon(material);string first=inventory.GetInstanceId(inventory.OwnedWeapons.Count-1);inventory.AddWeapon(material);string second=inventory.GetInstanceId(inventory.OwnedWeapons.Count-1);yield return null;
            ui.Open(fusion);var flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(EconomyWindow).GetField("first",flags).SetValue(ui,first);typeof(EconomyWindow).GetField("second",flags).SetValue(ui,second);
            int beforeCount=inventory.OwnedWeapons.Count;ui.Fuse();yield return new WaitForSecondsRealtime(.2f);
            Check("Fusion consumes two items, gives one result and one use",inventory.OwnedWeapons.Count==beforeCount-1&&inventory.OwnedWeapons.Contains(material.sameWeaponEvolution)&&fusion.RemainingFusions==1);ui.Close();Destroy(fusionGo);
            Status="Death animation, retry, title, LOAD";yield return null;
            RunEconomy.Instance.SetGold(123);motor.GetComponent<PlayerKarma>().SetValue(42);hp.invulnerable=false;hp.FatalDamage();
            yield return Wait(()=>FindFirstObjectByType<PlayerDeathSequence>()&&FindFirstObjectByType<PlayerDeathSequence>().MenuVisible,15);
            var death=FindFirstObjectByType<PlayerDeathSequence>();Check("Death animation ends at retry/title menu",death&&death.MenuVisible);yield return Shot("death-result");
            if(!death||!death.MenuVisible)yield break;
            death.GetComponentsInChildren<Button>().First(x=>x.name=="Retry").onClick.Invoke();
            yield return Wait(()=>ForestRoomStreaming.Instance&&ForestRoomStreaming.Instance!=stream&&ForestRoomStreaming.Instance.Ready&&!SeasonLoadingScreen.IsLoading,120);
            stream=ForestRoomStreaming.Instance;inventory=stream.director.hud.motor.GetComponent<PlayerInventory>();
            Check("Retry restores season entry inventory",startWeapons.SequenceEqual(inventory.OwnedWeapons.Select(x=>x.weaponId)));
            Check("Retry restores entry gold/karma and 5 HP",RunEconomy.Instance.Gold==startGold&&Mathf.Approximately(inventory.GetComponent<PlayerKarma>().Value,startKarma)&&Mathf.Approximately(stream.director.hud.health.Current,5));
            stream.director.hud.health.invulnerable=true;GameSettingsWindow.Instance.Open();GameSettingsWindow.Instance.ReturnToTitle();
            yield return Wait(()=>SceneManager.GetActiveScene().name=="Title"&&!SeasonLoadingScreen.IsLoading,60);Check("Settings returns to Title",SceneManager.GetActiveScene().name=="Title");
            FindFirstObjectByType<TitleScreen>().LoadGame();yield return Wait(()=>ForestRoomStreaming.Instance&&ForestRoomStreaming.Instance.Ready&&!SeasonLoadingScreen.IsLoading,120);
            stream=ForestRoomStreaming.Instance;Check("LOAD starts saved season with 5 HP",stream&&stream.gameObject.scene.name=="Stage1_Spring"&&Mathf.Approximately(stream.director.hud.health.Current,5));stream.director.hud.health.invulnerable=true;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Title.unity");yield return null;
            Status="All story pages and three ending branches";var book=StoryBook.Instance;
            foreach(var chapter in book.Library.chapters)
            {
                Check(chapter.id+" has complete story page assets",chapter.pages.All(p=>p.artwork&&!string.IsNullOrWhiteSpace(p.text)));
                Check(chapter.id+" has music",chapter.music);
            }
            foreach(var karma in new[]{-100,0,100})
            {
                string id=book.Library.Ending(karma,100);Check("Karma "+karma+" selects ending",id==(karma<0?"life":karma>0?"revenge":"farewell"));
                book.Play(id,null,true);yield return new WaitForSecondsRealtime(3);yield return Shot("ending-"+id);
                var chapter=book.Library.Find(id);
                for(int i=0;i<chapter.pages.Length-1;i++){for(int attempt=0;attempt<4&&book.CurrentPage==i;attempt++){book.NextPage();yield return null;}Check(id+" reader reaches page "+(i+2),book.CurrentPage==i+1);}
                yield return new WaitForSecondsRealtime(3);yield return Shot("ending-"+id+"-last");book.Finish();yield return Wait(()=>!StoryBook.IsOpen,10);
            }
        }
    }
}
#endif
