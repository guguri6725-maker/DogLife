using UnityEngine;
namespace DogLife {
 public sealed class RelicShrine:MonoBehaviour {
  const float InteractDistance=2.5f;
  static bool introductionSeen;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetIntroduction(){introductionSeen=false;}
  GameObject player; RelicLoadout loadout; bool consumed;
  MemoryCollarVfx presentation;
  public bool repeatable;
  GUIStyle prompt;
  public static RelicShrine Spawn(GameObject player,Vector3 position,bool repeatable=false){
   if(!player)return null;
   var loadout=RelicLoadout.Get(player);if(RelicDraftUI.PrepareChoices(player,loadout)==null)return null;
   var prefab=Resources.Load<GameObject>("VFX/MemoryCollar/MemoryCollar");
   if(!prefab){Debug.LogError("Memory collar prefab missing");return null;}
   // Rest on actual floor, never on an enemy/player collider or water trigger.
   var hits=Physics.RaycastAll(position+Vector3.up*3,Vector3.down,12,~0,QueryTriggerInteraction.Ignore);
   System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
   foreach(var hit in hits)if(hit.normal.y>.65f&&!hit.collider.GetComponentInParent<CombatHealth>()){position=hit.point;break;}
   var go=Instantiate(prefab,position,Quaternion.identity);go.name="이름을 잃은 목줄";
   var towards=player.transform.position-position;towards.y=0;if(towards.sqrMagnitude>.01f)go.transform.rotation=Quaternion.LookRotation(towards);
   var shrine=go.AddComponent<RelicShrine>();shrine.player=player;shrine.loadout=loadout;shrine.repeatable=repeatable;shrine.presentation=go.GetComponent<MemoryCollarVfx>();return shrine;
  }
  void Update(){if(Input.GetKeyDown(KeyCode.F))TryInteract();}
  public bool TryInteract(){
   if(consumed||!player||(presentation&&!presentation.Ready)||SkillWindow.BlocksGameplayInput||BossCinematicUI.Hidden||Time.timeScale<=0)return false;
   if(Vector3.Distance(player.transform.position,transform.position)>InteractDistance)return false;
   var choices=RelicDraftUI.PrepareChoices(player,loadout);if(choices==null)return false;
   consumed=true;bool introduce=!introductionSeen;introductionSeen=true;
   RelicDraftUI.Show(player,loadout,choices,()=>{if(this){if(repeatable)consumed=false;else Destroy(gameObject);}},introduce);
   return true;
  }
  void OnGUI(){
   if(consumed||!player||(presentation&&!presentation.Ready)||SkillWindow.BlocksGameplayInput||BossCinematicUI.Hidden)return;
   if(Vector3.Distance(player.transform.position,transform.position)>InteractDistance)return;
   if(prompt==null)prompt=new GUIStyle(GUI.skin.box){fontSize=22,alignment=TextAnchor.MiddleCenter};
   GUI.Box(new Rect(Screen.width*.5f-210,Screen.height*.72f,420,68),repeatable?"이름을 잃은 목줄\n[F] 기억 이어받기 · 반복 테스트":"이름을 잃은 목줄\n[F] 기억 이어받기",prompt);
  }
 }
}
