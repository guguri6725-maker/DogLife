using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    public sealed partial class StoryBook
    {
        RawImage outgoingArt;
        CanvasGroup readerFade;
        bool closing;
        float NarrationDelay => chapter.pages[page].narrationDelayOverride>=0?chapter.pages[page].narrationDelayOverride:library.narrationDelay;
        float ReadingDuration => Mathf.Max(.1f,NarrationDelay)+chapter.pages[page].text.Length/Mathf.Max(5,library.charactersPerSecond);
        bool NarrationComplete => Time.unscaledTime-pageStarted>=ReadingDuration;

        void BuildPresentation(Transform visual)
        {
            readerFade=reader.AddComponent<CanvasGroup>();
            outgoingArt=R("Previous illustration",reader.transform,Vector2.zero,new Vector2(1920,1080)).gameObject.AddComponent<RawImage>();
            outgoingArt.raycastTarget=false;outgoingArt.transform.SetAsFirstSibling();
            caption.supportRichText=true;
            Panel("Upper cinema matte",reader.transform,new Vector2(0,527),new Vector2(1920,26),Color.black);
            Panel("Lower cinema matte",reader.transform,new Vector2(0,-527),new Vector2(1920,26),Color.black);
        }
        void PreparePresentation()
        {
            closing=false;readerFade.alpha=1;readerFade.interactable=true;
            outgoingArt.texture=art.texture;outgoingArt.uvRect=art.uvRect;
            outgoingArt.color=art.texture?Color.white:Color.clear;
            art.uvRect=new Rect(0,0,1,1);
        }
        void UpdatePresentation(float elapsed)
        {
            float picture=Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.1f,library.pictureTransition));
            pageGroup.alpha=picture;
            // Keep the previous illustration underneath until the new one completely covers it.
            if(picture>=1)outgoingArt.texture=null;
            float title=Mathf.SmoothStep(0,1,(elapsed-.3f)/.85f);
            // 프리팹의 서체 색과 배치를 유지하며 부드럽게 나타냅니다.
            heading.color=new Color(headingTint.r,headingTint.g,headingTint.b,title*headingTint.a);
            subheading.color=new Color(subtitleTint.r,subtitleTint.g,subtitleTint.b,title*subtitleTint.a);
            heading.rectTransform.anchoredPosition=headingPosition-Vector2.up*(6*(1-title));
            rule.rectTransform.localScale=new Vector3(title,1,1);
            var text=chapter.pages[page].text;
            int count=Mathf.Clamp(Mathf.FloorToInt((elapsed-NarrationDelay)*Mathf.Max(5,library.charactersPerSecond)),0,text.Length);
            // Invisible suffix reserves exactly the final line layout during the reveal.
            caption.text=count>=text.Length?text:text.Substring(0,count)+"<color=#FFFFFF00>"+text.Substring(count)+"</color>";
            float narration=Mathf.SmoothStep(0,1,(elapsed-NarrationDelay)/.5f);
            caption.color=new Color(cream.r,cream.g,cream.b,narration);
            caption.rectTransform.anchoredPosition=new Vector2(-40,-326-8*(1-narration));
        }
        IEnumerator ClosePresentation()
        {
            closing=true;readerFade.interactable=false;
            float duration=Mathf.Max(.1f,library.closingFade);
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {readerFade.alpha=1-Mathf.SmoothStep(0,1,t/duration);yield return null;}
            bool returnToAlbum=replay;CancelPresentation();
            if(returnToAlbum)OpenGallery();else Release(true);
        }
        void CancelPresentation()
        {
            closing=false;
            if(readerFade){readerFade.alpha=1;readerFade.interactable=true;}
            if(outgoingArt)outgoingArt.texture=null;
        }
    }
}
