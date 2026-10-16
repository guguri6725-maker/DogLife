using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(CombatHealth))]
    public sealed class HarvestReaperDeath : MonoBehaviour
    {
        [SerializeField] Animator visual;
        [SerializeField] AnimationClip deathClip;
        [SerializeField] GameObject deathVisual;
        [SerializeField,Min(.05f)] float poseBlend=.3f;
        CombatHealth health;
        void Awake(){health=GetComponent<CombatHealth>();}
        void OnEnable(){if(health)health.Died+=OnDeath;}
        void OnDisable(){if(health)health.Died-=OnDeath;}
        void OnDeath()
        {
            if(!visual||!deathClip)return;
            // 기존 사망/보상 처리를 유지하고 시각 모델만 방에 남깁니다.
            var body=Instantiate(deathVisual?deathVisual:visual.gameObject,visual.transform.position,visual.transform.rotation);
            body.name="HarvestReaper_Corpse";
            body.transform.localScale=visual.transform.lossyScale;
            var original=visual.GetComponentsInChildren<Transform>();
            foreach(var bone in body.GetComponentsInChildren<Transform>())
            {
                if(bone==body.transform)continue;
                foreach(var source in original)if(source.name==bone.name)
                {bone.localPosition=source.localPosition;bone.localRotation=source.localRotation;bone.localScale=source.localScale;break;}
            }
            // 몸체와 분리된 낫의 재질을 각각 유지해 사망 시 무기 텍스처가 바뀌지 않도록 합니다.
            var originalSkins=visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                foreach(var originalSkin in originalSkins)
                    if(originalSkin.name==skin.name){skin.sharedMaterials=originalSkin.sharedMaterials;break;}
                skin.updateWhenOffscreen=true;
            }
            var room=GetComponentInParent<ForestRoom>();
            if(room)body.transform.SetParent(room.transform,true);
            else UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(body,gameObject.scene);
            foreach(var a in body.GetComponentsInChildren<Animator>())a.enabled=false;
            foreach(var c in body.GetComponentsInChildren<Collider>())c.enabled=false;
            body.AddComponent<HarvestReaperCorpse>().Begin(deathClip,poseBlend);
        }
    }
}
