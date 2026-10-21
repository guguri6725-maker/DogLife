using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    // Hierarchy에서 제작한 프리팹을 재생하며, 피해 계산이나 판정에는 관여하지 않습니다.
    public sealed class DamageNumber : MonoBehaviour
    {
        public Text label;
        public RectTransform canvasRect;
        public Color normalColor, strongColor, statusColor;
        public float lifetime=.6f, rise=36;
        static readonly List<DamageNumber> active=new List<DamageNumber>();
        static DamageNumber prefab;
        static int sequence;
        public static bool Visible=>PlayerPrefs.GetInt("DamageNumbers",1)!=0;
        CombatHealth victim;
        Vector3 point;
        float total,born,lastHit,offset;
        int group;
        bool strong,status;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){active.Clear();prefab=null;sequence=0;}
        public static int NextGroup(){return ++sequence;}
        public static void SetVisible(bool value)
        {
            PlayerPrefs.SetInt("DamageNumbers",value?1:0);PlayerPrefs.Save();
            if(!value)foreach(var n in active.ToArray())if(n)Destroy(n.gameObject);
        }
        public static void Show(CombatHealth target,float damage,Vector3 position,bool emphasized=false,int numberGroup=0,string message=null)
        {
            if(!Application.isPlaying||!Visible||!target||target.player||BossCinematicUI.Hidden||(string.IsNullOrEmpty(message)&&damage<=0))return;
            bool isStatus=!string.IsNullOrEmpty(message);
            active.RemoveAll(n=>!n);
            foreach(var n in active)
            {
                if(n.victim!=target)continue;
                if(isStatus&&n.status)return;
                // 창 연타는 콤보 단위로 누적하고, 지속 피해는 짧은 간격의 타격만 묶습니다.
                if(!isStatus&&!n.status&&numberGroup!=0&&n.group==numberGroup&&Time.time-n.lastHit<.4f&&Time.time-n.born<2.5f)
                {n.total+=damage;n.lastHit=Time.time;n.strong|=emphasized;n.Refresh();return;}
            }
            if(!prefab)prefab=Resources.Load<DamageNumber>("UI/DamageNumber");
            if(!prefab)return;
            if(active.Count>=24){var oldest=active[0];active.RemoveAt(0);if(oldest)Destroy(oldest.gameObject);}
            var item=Instantiate(prefab);item.victim=target;item.point=position;item.total=damage;item.group=numberGroup;
            item.born=item.lastHit=Time.time;item.strong=emphasized;item.status=isStatus;
            int nearby=0;foreach(var n in active)if(n.victim==target)nearby++;
            item.offset=(nearby%3-1)*36;active.Add(item);
            if(isStatus)item.label.text=message;else item.Refresh();
        }
        void Refresh()
        {
            // 정수 피해는 깔끔하게, 소수 피해는 최대 두 자리까지 표시합니다.
            label.text=total.ToString("0.##",CultureInfo.InvariantCulture);
        }
        void LateUpdate()
        {
            if(!Visible||Time.time-lastHit>=lifetime||Time.time-born>=3.1f){Destroy(gameObject);return;}
            var camera=Camera.main;
            if(!label||!canvasRect||!camera)return;
            var screen=camera.WorldToViewportPoint(point);
            label.enabled=screen.z>0&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1&&!BossCinematicUI.Hidden&&!GameSettingsWindow.IsOpen;
            if(!label.enabled)return;
            float age=Time.time-born,fade=Mathf.Clamp01((Time.time-lastHit-lifetime*.55f)/(lifetime*.45f));
            var color=status?statusColor:strong?strongColor:normalColor;color.a*=(1-fade)*(group<0?.8f:1);label.color=color;
            var rect=label.rectTransform;
            rect.anchoredPosition=new Vector2((screen.x-.5f)*canvasRect.rect.width+offset,(screen.y-.5f)*canvasRect.rect.height+42+rise*Mathf.Clamp01(age/lifetime));
            float scale=status||group<0?.85f:strong?1.22f:1;
            rect.localScale=Vector3.one*scale*(1+(strong?.1f:.035f)*(1-Mathf.Clamp01(age/.12f)));
        }
        void OnDestroy(){active.Remove(this);}
    }
}
