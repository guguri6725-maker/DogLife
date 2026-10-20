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
        bool deathStarted;
        void Awake(){health=GetComponent<CombatHealth>();}
        void OnEnable()
        {
            // 재활성화/스크립트 재로드 뒤에도 사망 이벤트를 빠짐없이 다시 연결합니다.
            health=GetComponent<CombatHealth>();
            health.Died-=OnDeath;health.Died+=OnDeath;
            if(!health.Dead)deathStarted=false;
        }
        void OnDisable()
        {
            // 체력 처리에서 본체가 꺼지더라도 사망 전용 모델은 한 번만 남깁니다.
            if(Application.isPlaying&&health&&health.Dead)OnDeath();
            if(health)health.Died-=OnDeath;
        }
        void OnDeath()
        {
            if(deathStarted||!visual||!deathClip)return;
            deathStarted=true;
            // 기존 사망/보상 처리를 유지하고 시각 모델만 방에 남깁니다.
            var body=Instantiate(deathVisual?deathVisual:visual.gameObject,visual.transform.position,visual.transform.rotation);
            body.name="HarvestReaper_Corpse";
            body.transform.localScale=visual.transform.lossyScale;
            var original=visual.GetComponentsInChildren<Transform>(true);
            foreach(var bone in body.GetComponentsInChildren<Transform>(true))
            {
                if(bone==body.transform)continue;
                foreach(var source in original)if(source.name==bone.name)
                {bone.localPosition=source.localPosition;bone.localRotation=source.localRotation;bone.localScale=source.localScale;break;}
            }
            // 몸체와 분리된 낫의 재질을 각각 유지해 사망 시 무기 텍스처가 바뀌지 않도록 합니다.
            var originalSkins=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach(var originalSkin in originalSkins)
                    if(originalSkin.name==skin.name){skin.sharedMaterials=originalSkin.sharedMaterials;break;}
                // 카메라가 생전 모델을 숨긴 프레임에 처치돼도 시체는 보이게 합니다.
                skin.enabled=true;skin.forceRenderingOff=false;skin.SetPropertyBlock(null);
                skin.updateWhenOffscreen=true;
            }
            var room=GetComponentInParent<ForestRoom>();
            if(room)body.transform.SetParent(room.transform,true);
            else UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(body,gameObject.scene);
            foreach(var a in body.GetComponentsInChildren<Animator>(true))a.enabled=false;
            foreach(var c in body.GetComponentsInChildren<Collider>(true))c.enabled=false;
            body.SetActive(true);
            body.AddComponent<HarvestReaperCorpse>().Begin(deathClip,poseBlend);
        }
    }
}
