using UnityEngine;
namespace DogLife
{
    public sealed class BubbleDebuff : MonoBehaviour
    {
        float until,moveMultiplier=1,rollMultiplier=1;
        public bool Active=>Time.time<until;
        public float VisualOpacity=>Mathf.Clamp01((until-Time.time)/.4f);
        Collider body;
        public Vector3 EffectFootPosition=>body?new Vector3(transform.position.x,body.bounds.min.y+.06f,transform.position.z):transform.position;
        void Awake(){body=GetComponent<Collider>();}
        public float MoveMultiplier=>Active?moveMultiplier:1;
        public float RollMultiplier=>Active?rollMultiplier:1;
        SummerWaterVfx effect;
        public void Apply(float moveReduction,float rollReduction,float duration,Material material)
        {
            moveMultiplier=1-Mathf.Clamp(moveReduction,0,.85f);
            rollMultiplier=1-Mathf.Clamp(rollReduction,0,.85f);
            until=Time.time+Mathf.Max(.05f,duration); // 재적중은 강도 곱셈 없이 기간만 갱신합니다.
            if(!effect){var go=new GameObject("BubbleDebuffVFX");go.transform.SetParent(transform,false);effect=go.AddComponent<SummerWaterVfx>();effect.Initialize(material);effect.debuff=this;}
        }
        void Update(){var h=GetComponent<CombatHealth>();if(h&&h.Dead)until=0;}
        void OnDisable(){until=0;}
    }
}
