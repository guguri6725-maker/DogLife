using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
namespace DogLife
{
    // Hierarchy에 배치한 보호 아이콘과 숫자만 갱신합니다. 일반 피격 무적은 표시하지 않습니다.
    public sealed class FrostProtectionHUD:MonoBehaviour
    {
        [SerializeField] CombatHealth health;
        [SerializeField] CanvasGroup panel;
        [SerializeField] Text seconds;
        [SerializeField] RectTransform icon;
        [SerializeField] Image fill;
        float previous,maximum;
        void LateUpdate()
        {
            float remaining=health?health.FrostProtectionRemaining:0;
            if(remaining>previous+.1f){maximum=remaining;icon.DOKill();icon.localScale=Vector3.one;icon.DOPunchScale(Vector3.one*.3f,.35f,5,.5f);}
            panel.alpha=remaining>0?1:0;panel.interactable=false;panel.blocksRaycasts=false;
            seconds.text=remaining>0?"무적 "+remaining.ToString("0.0")+"초":"";
            if(fill){float fraction=Mathf.Clamp01(remaining/Mathf.Max(.01f,maximum));fill.fillAmount=fraction;fill.rectTransform.localScale=new Vector3(fraction,1,1);}previous=remaining;
        }
        void OnDisable(){if(icon)icon.DOKill();}
    }
}
