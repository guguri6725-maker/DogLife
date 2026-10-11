using UnityEngine;

namespace DogLife
{
    public sealed class SeasonEnemyHealthBar : MonoBehaviour
    {
        [SerializeField,Min(1)] float maxVisibleDistance=18;
        [SerializeField] LayerMask occlusionMask=~0;
        [SerializeField] Color fillColor=new Color(.84f,.42f,.53f,1);
        [SerializeField] Color highlightColor=new Color(1,.76f,.80f,.9f);
        [SerializeField] Color trailColor=new Color(.94f,.70f,.57f,1);
        readonly RaycastHit[] sightHits=new RaycastHit[32];
        Transform player;
        float visibilityAlpha=1;
        CombatHealth health;
        SpringBoarPhaseTransition boarPhase;
        Collider body;
        Transform head;
        Transform worldAnchor;
        public void SetWorldAnchor(Transform value){worldAnchor=value;}
        float trail=1,last=1,delay;
        Vector3 screen;
        bool visible;
        void Awake()
        {
            health=GetComponent<CombatHealth>();body=GetComponent<Collider>();
            boarPhase=GetComponent<SpringBoarPhaseTransition>();
            var motor=FindFirstObjectByType<PlayerMotor>();if(motor)player=motor.transform;
            foreach(var t in GetComponentsInChildren<Transform>())if(t.name=="Head"){head=t;break;}
        }
        void LateUpdate()
        {
            var camera=Camera.main;visible=camera&&health&&!health.Dead&&!SkillWindow.BlocksGameplayInput;
            if(!visible)return;
            float distance=Vector3.Distance(player?player.position:camera.transform.position,transform.position);
            if(distance>=maxVisibleDistance){visible=false;return;}
            visibilityAlpha=Mathf.Clamp01((maxVisibleDistance-distance)/3f);
            float value=Mathf.Clamp01(health.Current/Mathf.Max(1,health.maxHealth));
            if(boarPhase&&boarPhase.Transitioning) value=boarPhase.HealthReveal;
            if(value<last)delay=Time.time+.22f;
            if(value>last)trail=value;
            last=value;if(Time.time>delay)trail=Mathf.MoveTowards(trail,value,Time.deltaTime*1.3f);
            float y=body?body.bounds.max.y:transform.position.y+1;
            if(head)y=Mathf.Max(y,head.position.y+.4f);
            screen=camera.WorldToScreenPoint(worldAnchor?worldAnchor.position+Vector3.up:new Vector3(transform.position.x,y+.25f,transform.position.z));
            visible=screen.z>0&&screen.x>=0&&screen.x<=Screen.width&&screen.y>=0&&screen.y<=Screen.height;
            // 화면 위에 그리는 HP바도 실제 카메라와 몸 사이에 벽이 있으면 숨깁니다.
            if(visible)visible=!Occluded(camera.transform.position,worldAnchor?worldAnchor.position:body?body.bounds.center:transform.position+Vector3.up);
        }
        bool Occluded(Vector3 origin,Vector3 target)
        {
            Vector3 delta=target-origin;float distance=delta.magnitude;
            if(distance<.01f)return false;
            int count=Physics.RaycastNonAlloc(origin,delta/distance,sightHits,distance,occlusionMask,QueryTriggerInteraction.Ignore);
            if(count==sightHits.Length)return true;
            for(int i=0;i<count;i++)
            {
                var hit=sightHits[i].collider;
                // 자기 몸과 캐릭터는 제외하고 벽·문·구조물에만 가려집니다.
                if(hit.transform.IsChildOf(transform)||hit.GetComponentInParent<CombatHealth>())continue;
                return true;
            }
            return false;
        }
        void Box(Rect r,Color color){color.a*=visibilityAlpha;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);}
        void Diamond(float x,float y,float size,Color color)
        {
            var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(45,new Vector2(x,y));Box(new Rect(x-size*.5f,y-size*.5f,size,size),color);GUI.matrix=matrix;
        }
        void OnGUI()
        {
            if(!visible||!health||health.Dead)return;
            var previous=GUI.color;
            float s=Mathf.Clamp(Screen.height/1080f,.8f,1.4f),w=72*s,h=6*s,x=screen.x-w*.5f,y=Screen.height-screen.y;
            Color bone=new Color(.87f,.78f,.65f,.95f),pink=fillColor;
            Box(new Rect(x-3*s,y-3*s,w+6*s,h+6*s),new Color(.12f,.10f,.12f,.8f));
            Box(new Rect(x,y-s,w,h+2*s),bone);
            Box(new Rect(x,y,w,h),new Color(.22f,.16f,.19f,1));
            Box(new Rect(x,y,w*trail,h),trailColor);
            Box(new Rect(x,y,w*last,h),pink);
            Box(new Rect(x,y,w*last,s),highlightColor);
            Diamond(x-5*s,y+h*.5f,5*s,bone);Diamond(x+w+5*s,y+h*.5f,5*s,bone);
            Diamond(x+w*.5f,y-5*s,3*s,pink);
            GUI.color=previous;
        }
    }
}
