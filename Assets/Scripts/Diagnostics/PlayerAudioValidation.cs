#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    public sealed class PlayerAudioValidation:MonoBehaviour
    {
        readonly StringBuilder report=new StringBuilder();
        void Check(string name,bool pass){report.AppendLine((pass?"PASS ":"FAIL ")+name);}
        AudioSource[] Sources()=>FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>();
        bool Playing(string key)=>Sources().Any(s=>s.clip&&s.clip.name==key&&s.isPlaying);
        IEnumerator Start()
        {
            bool existed=PlayerPrefs.HasKey("DogLife.AudioSettings");string backup=PlayerPrefs.GetString("DogLife.AudioSettings","");var saved=GameAudio.Saved;
            var motor=FindFirstObjectByType<PlayerMotor>();var combat=motor.GetComponent<BasicWeaponCombat>();var weapons=motor.GetComponent<PlayerWeapons>();var inventory=motor.GetComponent<PlayerInventory>();var health=motor.GetComponent<CombatHealth>();
            var original=inventory.EquippedWeapon;var added=new System.Collections.Generic.List<InventoryWeapon>();bool motorInput=motor.readInput,weaponInput=weapons.readInput,invulnerable=health.invulnerable;var cursor=Cursor.lockState;
            foreach(InventoryWeapon.WeaponCategory cat in System.Enum.GetValues(typeof(InventoryWeapon.WeaponCategory)))
                if(!inventory.OwnedWeapons.Any(w=>w&&w.weaponCategory==cat))
                {var w=UnityEditor.AssetDatabase.FindAssets("t:InventoryWeapon").Select(g=>UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryWeapon>(UnityEditor.AssetDatabase.GUIDToAssetPath(g))).First(x=>x.weaponCategory==cat&&x.weaponForm==InventoryWeapon.WeaponForm.Basic);inventory.AddWeapon(w);added.Add(w);}
            motor.readInput=false;weapons.readInput=false;health.invulnerable=true;motor.MoveInput=Vector2.zero;
            try
            {
                Check("30 processed clips",Resources.LoadAll<AudioClip>("Audio/Player").Length==30);
                Check("single listener",FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1);
                var window=GameSettingsWindow.Instance;window.Open();
                var sliders=window.GetComponentsInChildren<Slider>(true);Check("three audio sliders",sliders.Length==3);
                sliders[2].value=0;Check("effects preview mute",GameAudio.Current.effects==0);
                window.Close();Check("close cancels audio preview",Mathf.Approximately(GameAudio.Current.effects,saved.effects));
                window.Open();sliders[2].value=37;window.Apply();
                yield return new WaitForSecondsRealtime(.5f);
                Check("apply saves sound preferences",Mathf.Approximately(GameAudio.Saved.effects,.37f)&&PlayerPrefs.HasKey("DogLife.AudioSettings"));
                window.Close();window.Open();Check("reopen keeps saved volume",Mathf.Approximately(sliders[2].value,37));window.Close();
                GameAudio.Preview(new AudioPreferences{master=1,effects=1,music=1});
                var loop=GameAudio.Play("BoomerangLoop",motor.transform.position,follow:motor.transform,loop:true,minimumInterval:0);
                yield return new WaitForSecondsRealtime(.1f);
                Check("loop playback",Playing("BoomerangLoop"));
                var source=Sources().First(s=>s.clip&&s.clip.name=="BoomerangLoop"&&s.isPlaying);
                float[] samples=new float[source.clip.samples];source.clip.GetData(samples,0);Check("imported clip has nonzero signal",samples.Any(s=>Mathf.Abs(s)>.000001f));
                GameAudio.Preview(new AudioPreferences{master=0,effects=1,music=1});Check("master zero mutes active effects",Sources().Where(s=>s.isPlaying).All(s=>s.volume==0));
                GameAudio.Preview(new AudioPreferences{master=1,effects=0,music=1});Check("effects zero mutes active effects",source.volume==0);
                GameAudio.Preview(new AudioPreferences{master=1,effects=1,music=1});
                Time.timeScale=0;yield return null;yield return null;Check("gameplay audio pauses",!source.isPlaying);
                GameAudio.Play("BagOpen",Vector3.zero,ui:true,minimumInterval:0);yield return new WaitForSecondsRealtime(.03f);Check("UI audio works while paused",Playing("BagOpen"));
                Time.timeScale=1;yield return null;yield return null;Check("gameplay loop resumes",source.isPlaying);loop.Stop();Check("loop handle stops",!source.isPlaying);
                foreach(InventoryWeapon.WeaponCategory category in System.Enum.GetValues(typeof(InventoryWeapon.WeaponCategory)))
                {
                    combat.Cancel();var item=inventory.OwnedWeapons.FirstOrDefault(w=>w&&w.weaponCategory==category);
                    if(!item){Check(category+" owned weapon",false);continue;}inventory.Equip(item);weapons.readInput=category==InventoryWeapon.WeaponCategory.Bow;Cursor.lockState=CursorLockMode.Locked;combat.PressAttack();
                    if(category==InventoryWeapon.WeaponCategory.Bow){yield return new WaitForSecondsRealtime(.06f);Check("bow charging sound",Playing("BowCharge"));combat.ReleaseAttack();Check("release stops charge sound",!Playing("BowCharge"));}
                    float timeout=Time.realtimeSinceStartup+3;bool heard=false;
                    while(Time.realtimeSinceStartup<timeout){if(Playing(category+"Attack")){heard=true;break;}yield return null;}
                    Check(category+" actual attack triggers audio",heard);combat.Cancel();
                    GameAudio.WeaponHit(item,motor.transform.position);Check(category+" hit clip mapped",Playing(category+"Hit"));
                    yield return new WaitForSecondsRealtime(.15f);
                }
                combat.Cancel();var bow=inventory.OwnedWeapons.First(w=>w&&w.weaponCategory==InventoryWeapon.WeaponCategory.Bow);inventory.Equip(bow);weapons.readInput=true;Cursor.lockState=CursorLockMode.Locked;combat.PressAttack();yield return new WaitForSecondsRealtime(.08f);Check("charge active before cancel",Playing("BowCharge"));combat.CancelForDash();Check("dash cancel stops bow sound",!Playing("BowCharge"));
                var moving=new GameObject("Audio follow test");var h=GameAudio.Play("BoomerangLoop",moving.transform.position,follow:moving.transform,loop:true,minimumInterval:0);yield return null;Destroy(moving);yield return null;yield return null;Check("destroyed emitter stops loop",!Sources().Any(s=>s.clip&&s.clip.name=="BoomerangLoop"&&s.isPlaying&&s.transform.position==Vector3.zero));h.Stop();
                int before=GameAudio.ActiveVoices;GameAudio.Play("SpearHit",motor.transform.position,minimumInterval:.2f);GameAudio.Play("SpearHit",motor.transform.position,minimumInterval:.2f);Check("repeated contact sound rate limited",GameAudio.ActiveVoices<=before+1);
            }
            finally
            {
                Time.timeScale=1;combat.Cancel();foreach(var w in added)inventory.RemoveWeapon(w);if(original)inventory.Equip(original);else inventory.Unequip();motor.readInput=motorInput;weapons.readInput=weaponInput;health.invulnerable=invulnerable;Cursor.lockState=cursor;
                GameAudio.Preview(saved);GameAudio.Commit();if(existed)PlayerPrefs.SetString("DogLife.AudioSettings",backup);else PlayerPrefs.DeleteKey("DogLife.AudioSettings");PlayerPrefs.Save();
                System.IO.File.WriteAllText("Builds/AudioValidation/runtime-checks.txt",report.ToString());Debug.Log("Player audio validation completed\n"+report);
            }
            Destroy(gameObject);
        }
    }
}
#endif
