using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
 // Animator의 주동작 위에 관절별 지연과 촉수의 부력을 더한다. 판정은 변경하지 않는다.
 [DefaultExecutionOrder(120)]
 public sealed class KrakenRigMotion : MonoBehaviour
 {
  [SerializeField,Range(0,8)] float idleBend=1.1f,attackBend=2.8f;
  [SerializeField,Min(.1f)] float flowSpeed=.8f,settleTime=.35f;
  [Header("몸통 호흡")]
  [SerializeField,Min(.005f)] float breathTravel=.07f;
  [SerializeField,Range(0,3)] float shellSway=.7f;
  readonly List<Transform> hull=new List<Transform>();
  readonly List<Vector3> hullPositions=new List<Vector3>();
  readonly List<Quaternion> hullRotations=new List<Quaternion>();
  readonly List<Vector2Int> hullIndices=new List<Vector2Int>();
  readonly List<Transform> joints=new List<Transform>();
  readonly List<Quaternion> rest=new List<Quaternion>();
  readonly List<Vector2Int> indices=new List<Vector2Int>();
  KrakenBoss boss;KrakenTentacle tentacle;bool mantle;float energy,velocity;
  void Awake()
  {
   boss=GetComponentInParent<KrakenBoss>();tentacle=GetComponentInParent<KrakenTentacle>();
   foreach(var t in GetComponentsInChildren<Transform>(true))
   {
    if(t.name.StartsWith("Hull_"))
    {
     var parts=t.name.Split('_');if(parts.Length==3&&int.TryParse(parts[1],out int sector)&&int.TryParse(parts[2],out int level))
     {hull.Add(t);hullPositions.Add(t.localPosition);hullRotations.Add(t.localRotation);hullIndices.Add(new Vector2Int(sector,level));}
    }
    if(t.name.StartsWith("Flex_")&&int.TryParse(t.name.Substring(5),out int flex)){joints.Add(t);rest.Add(t.localRotation);indices.Add(new Vector2Int(0,flex));}
    else if(t.name.StartsWith("Mantle_"))
    {
     var parts=t.name.Split('_');if(parts.Length!=3||!int.TryParse(parts[1],out int arm)||!int.TryParse(parts[2],out int joint))continue;
     mantle=true;joints.Add(t);rest.Add(t.localRotation);indices.Add(new Vector2Int(arm,joint));
    }
   }
  }
  void LateUpdate()
  {
   if(!boss||!boss.isActiveAndEnabled)return;
   bool active=tentacle?tentacle.Busy:boss.State!=KrakenBoss.Phase.Idle;
   energy=Mathf.SmoothDamp(energy,active?1:0,ref velocity,settleTime);
   for(int i=0;i<joints.Count;i++)
   {
    var index=indices[i];float along=index.y/(mantle?5f:23f);
    float phase=Time.time*flowSpeed*2-index.x*.73f-along*2.8f;
    float amplitude=Mathf.Lerp(idleBend,attackBend,energy)*Mathf.Lerp(.2f,1,along);
    // 충돌 후에는 떨림을 가라앉혀 바닥에 닿은 촉수가 계속 출렁이지 않게 한다.
    if(tentacle&&(tentacle.State==KrakenTentacle.Mode.Ground||tentacle.State==KrakenTentacle.Mode.Strike))amplitude*=.3f;
    joints[i].localRotation=rest[i]*Quaternion.Euler(Mathf.Sin(phase)*amplitude,0,Mathf.Cos(phase*.83f)*amplitude*.4f);
   }
   // 앞가슴·옆구리·등판이 서로 다른 위상으로 팽창하며, 외피 끝은 작게 따라온다.
   for(int i=0;i<hull.Count;i++)
   {
    var index=hullIndices[i];float phase=Time.time*flowSpeed-index.y*.4f;
    float breath=Mathf.Sin(phase)*(1+energy*.25f),angle=index.x*Mathf.PI*.5f;
    Vector3 radial=boss.transform.right*Mathf.Sin(angle)-boss.transform.forward*Mathf.Cos(angle);
    Vector3 world=radial*breath*breathTravel*boss.transform.lossyScale.x;
    hull[i].localPosition=hullPositions[i]+hull[i].parent.InverseTransformVector(world);
    hull[i].localRotation=hullRotations[i]*Quaternion.Euler(breath*shellSway,0,Mathf.Sin(phase-.6f)*shellSway*.35f);
   }
  }
  void OnDisable(){for(int i=0;i<joints.Count;i++)if(joints[i])joints[i].localRotation=rest[i];for(int i=0;i<hull.Count;i++)if(hull[i]){hull[i].localPosition=hullPositions[i];hull[i].localRotation=hullRotations[i];}energy=velocity=0;}
 }
}
