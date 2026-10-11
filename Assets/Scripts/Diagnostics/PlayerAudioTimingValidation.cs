#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace DogLife
{
    public sealed class PlayerAudioTimingValidation:MonoBehaviour
    {
        readonly List<string> report=new List<string>(),played=new List<string>();
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        void Heard(string key){played.Add(key);}
        void Check(bool pass,string text){report.Add((pass?"PASS ":"FAIL ")+text);}
        void Set(object obj,string field,object value){obj.GetType().GetField(field,Flags).SetValue(obj,value);}
        void Call(object obj,string name,params object[] args){obj.GetType().GetMethod(name,Flags).Invoke(obj,args);}
        IEnumerator Start()
        {
            while(ForestRoomStreaming.Instance&&!ForestRoomStreaming.Instance.Ready&&string.IsNullOrEmpty(ForestRoomStreaming.Instance.LoadError))yield return null;
            var motor=FindFirstObjectByType<PlayerMotor>();var combat=motor.GetComponent<BasicWeaponCombat>();
            var weapons=motor.GetComponent<PlayerWeapons>();var inventory=motor.GetComponent<PlayerInventory>();
            var health=motor.GetComponent<CombatHealth>();var body=motor.GetComponent<Rigidbody>();
            var saved=GameAudio.Current;var position=motor.transform.position;var rotation=motor.transform.rotation;
            var original=inventory.EquippedWeapon;bool mi=motor.readInput,wi=weapons.readInput,iv=health.invulnerable,ce=combat.enabled;
            bool motorEnabled=motor.enabled,weaponsEnabled=weapons.enabled,kinematic=body.isKinematic;
            var added=new List<InventoryWeapon>();GameObject ground=null;
            GameAudio.EffectStarted+=Heard;
            try
            {
                var muted=saved;muted.master=0;GameAudio.Preview(muted);
                motor.readInput=false;weapons.readInput=false;health.invulnerable=true;motor.MoveInput=Vector2.zero;
                if(health.Dead)health.Restore();
                motor.enabled=true;weapons.enabled=true;body.isKinematic=false;
                foreach(var move in combat.movesets)
                {
                    var item=AssetDatabase.FindAssets("t:InventoryWeapon").Select(g=>AssetDatabase.LoadAssetAtPath<InventoryWeapon>(AssetDatabase.GUIDToAssetPath(g))).First(w=>w.weaponCategory==move.category&&w.weaponForm==InventoryWeapon.WeaponForm.Basic);
                    if(!inventory.OwnedWeapons.Contains(item)){inventory.AddWeapon(item);added.Add(item);}
                    inventory.Equip(item);combat.enabled=false;
                    for(int step=0;step<move.attacks.Length;step++)
                    {
                        combat.Cancel();Set(combat,"item",item);Set(combat,"move",move);Call(combat,"Begin",step);
                        bool flurry=move.category==InventoryWeapon.WeaponCategory.Spear&&step==2;
                        float lead=move.attackAudioLeadTimes[step],trigger=Mathf.Max(0,move.strikeTimes[step]-lead);
                        string sound=move.category.ToString()+((step==2&&(move.category==InventoryWeapon.WeaponCategory.Sword||move.category==InventoryWeapon.WeaponCategory.Dagger))?"Final":flurry?"Thrust":"Attack");
                        played.Clear();
                        Set(combat,"started",Time.time-(trigger-.02f)/move.attackSpeed);
                        Call(combat,"UpdateAttackAudio",flurry);
                        Check(played.Count(k=>k==sound)==0,move.category+" "+step+" no premature sound");
                        yield return new WaitForSecondsRealtime(.04f);
                        Set(combat,"started",Time.time-(trigger+.002f)/move.attackSpeed);
                        Call(combat,"UpdateAttackAudio",flurry);
                        Check(played.Count(k=>k==sound)==1,move.category+" "+step+" sound at authored swing time ("+string.Join(",",played)+")");
                        Call(combat,"UpdateAttackAudio",flurry);
                        Check(played.Count(k=>k==sound)==1,move.category+" "+step+" no duplicate sound");
                        combat.Cancel();played.Clear();Call(combat,"Update");
                        Check(played.Count==0,move.category+" "+step+" cancel suppresses future audio");
                    }
                }
                combat.enabled=ce;combat.Cancel();
                // Exercise actual final blended foot poses on an isolated level floor.
                ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Audio cadence validation floor";ground.layer=8;
                ground.transform.position=new Vector3(1000,-.5f,1000);ground.transform.localScale=new Vector3(60,1,60);
                motor.transform.position=new Vector3(1000,2,1000);body.linearVelocity=Vector3.zero;
                yield return new WaitForSeconds(1);
                foreach(var direction in new[]{Vector2.up,Vector2.right,Vector2.down,Vector2.left,new Vector2(1,1).normalized,new Vector2(-1,1).normalized})
                {
                    played.Clear();motor.MoveInput=direction;
                    float until=Time.time+2.2f,min=float.MaxValue,max=float.MinValue;
                    var foot=motor.GetComponentsInChildren<Transform>().First(t=>t.name=="LeftFoot");
                    while(Time.time<until){float h=motor.transform.InverseTransformPoint(foot.position).y;min=Mathf.Min(min,h);max=Mathf.Max(max,h);yield return null;}
                    int count=played.Count(k=>k.StartsWith("Step"));
                    Check(count>=2&&count<=7,"Walking "+direction+" contact count="+count+" foot travel="+(max-min).ToString("F3"));
                }
                motor.MoveInput=Vector2.zero;yield return new WaitForSeconds(.2f);played.Clear();
                yield return new WaitForSeconds(.6f);
                Check(!played.Any(k=>k.StartsWith("Step")),"No footsteps when stopped");
                played.Clear();SkillWindow.Instance.SetOpen(true);
                Check(played.Contains("BagOpen"),"Bag open sound triggered on same call");
                SkillWindow.Instance.SetOpen(false);
                int buffer,countBuffers;AudioSettings.GetDSPBufferSize(out buffer,out countBuffers);
                Check(buffer<=512,"DSP buffer="+buffer);
            }
            finally
            {
                GameAudio.EffectStarted-=Heard;Time.timeScale=1;combat.Cancel();combat.enabled=ce;
                motor.MoveInput=Vector2.zero;motor.transform.SetPositionAndRotation(position,rotation);body.linearVelocity=Vector3.zero;
                motor.readInput=mi;weapons.readInput=wi;health.invulnerable=iv;
                motor.enabled=motorEnabled;weapons.enabled=weaponsEnabled;body.isKinematic=kinematic;
                foreach(var item in added)inventory.RemoveWeapon(item);if(original)inventory.Equip(original);else inventory.Unequip();
                if(ground)Destroy(ground);GameAudio.Preview(saved);
                System.IO.File.WriteAllLines("Builds/AudioValidation/timing-runtime.txt",report);
                Debug.Log("Audio timing validation: "+string.Join("; ",report));Destroy(gameObject);
            }
        }
    }
}
#endif
