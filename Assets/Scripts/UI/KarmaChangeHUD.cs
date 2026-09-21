using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    public sealed class KarmaChangeHUD : MonoBehaviour
    {
        public PlayerKarma source;
        public Text currentValue, changeLabel;
        [SerializeField, Min(.2f)] float displayDuration=1.4f;
        Vector2 origin;
        Tween animation;
        float lastChange=-10, accumulated;
        void OnEnable()
        {
            if(!source||!currentValue||!changeLabel)return;
            origin=changeLabel.rectTransform.anchoredPosition;
            changeLabel.text="";
            RefreshValue();source.Changed+=ShowChange;
        }
        void RefreshValue(){currentValue.text=source.Value.ToString("+0;-0;0");}
        void ShowChange(float delta)
        {
            RefreshValue();
            bool combine=Time.unscaledTime-lastChange<.6f&&Mathf.Sign(delta)==Mathf.Sign(accumulated);
            accumulated=combine?accumulated+delta:delta;lastChange=Time.unscaledTime;
            animation?.Kill();
            changeLabel.text=accumulated.ToString("+0;-0;0");
            var tint=delta>0?new Color(1,.30f,.37f,1):new Color(.40f,.86f,1,1);
            changeLabel.color=tint;changeLabel.rectTransform.anchoredPosition=origin;
            animation=DOTween.To(()=>0f,t=>{
                changeLabel.rectTransform.anchoredPosition=origin+Vector2.up*(22*t);
                var c=tint;c.a=1-Mathf.InverseLerp(.5f,1,t);changeLabel.color=c;
            },1,displayDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }
        void OnDisable()
        {
            if(source)source.Changed-=ShowChange;
            animation?.Kill();animation=null;
            if(changeLabel){changeLabel.text="";changeLabel.rectTransform.anchoredPosition=origin;}
            lastChange=-10;accumulated=0;
        }
    }
}
