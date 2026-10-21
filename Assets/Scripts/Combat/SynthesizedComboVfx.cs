using UnityEngine;

namespace DogLife
{
    // 모든 렌더러는 Hierarchy에서 제작한 프리팹을 사용하며 좌표와 수명만 갱신합니다.
    public sealed partial class SynthesizedComboVfx : MonoBehaviour
    {
        public LineRenderer[] strokes;
        sealed class Stroke
        {
            public Vector3[] points;
            public Color color;
            public float width,delay,life,expansion;
            public Vector3 center,drift;
            public bool reveal;
        }
        Stroke[] animation;
        float born,totalLife;
        int used;
        void StartEffect()
        {
            born=Time.time;used=0;totalLife=0;following=false;followTarget=null;animation=new Stroke[strokes.Length];
            foreach(var line in strokes)if(line){line.enabled=false;line.positionCount=0;}
        }
        void Add(Vector3[] points,Color color,float width,float life,float delay=0,bool reveal=false,Vector3 drift=default, float expansion=0)
        {
            if(used>=strokes.Length||!strokes[used])return;
            // 두 점짜리 찌르기도 중간 꼭짓점이 있어야 가운데 두께와 흰 중심이 나타납니다.
            if(points.Length==2)
            {
                var start=points[0];var end=points[1];points=new Vector3[17];
                for(int i=0;i<points.Length;i++)points[i]=Vector3.Lerp(start,end,i/(float)(points.Length-1));
            }
            Vector3 center=Vector3.zero;foreach(var p in points)center+=p;center/=points.Length;
            animation[used]=new Stroke{points=points,color=color,width=width,life=life,delay=delay,reveal=reveal,center=center,drift=drift,expansion=expansion};
            var line=strokes[used++];line.positionCount=points.Length;line.SetPositions(points);
            line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.18f,.65f),new Keyframe(.55f,1),new Keyframe(1,0));
            totalLife=Mathf.Max(totalLife,delay+life);
        }
        static Vector3[] Copy(LineRenderer line)
        {var p=new Vector3[line.positionCount];line.GetPositions(p);return p;}
        static Vector3[] Offset(Vector3[] p,Vector3 offset)
        {var result=new Vector3[p.Length];for(int i=0;i<p.Length;i++)result[i]=p[i]+offset;return result;}
        static Vector3[] Circle(Vector3 center,Vector3 right,Vector3 up,float radius)
        {
            var p=new Vector3[49];for(int i=0;i<p.Length;i++){float a=i*Mathf.PI*2/(p.Length-1);p[i]=center+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius;}return p;
        }
        static Color Alpha(Color c,float a){c.a=a;return c;}
        public void Initialize(LineRenderer hit,LineRenderer impact,Vector3 origin,Vector3 forward,Color tint,InventoryWeapon.WeaponCategory category,int step,int thrust)
        {
            StartEffect();forward=forward.normalized;var right=Vector3.Cross(Vector3.up,forward).normalized;
            bool axe=category==InventoryWeapon.WeaponCategory.Axe;
            bool thrusting=category==InventoryWeapon.WeaponCategory.Dagger||(category==InventoryWeapon.WeaponCategory.Spear&&step==2);
            bool last=step==2&&(category!=InventoryWeapon.WeaponCategory.Spear||thrust==12);
            float life=axe?.38f:thrusting?(last?.19f:.105f):.27f;
            var path=Copy(hit);float width=hit.widthMultiplier;
            var white=Color.Lerp(tint,Color.white,.88f);white.a=1;
            // 색 테두리, 흰 중심선, 늦게 사라지는 보조선을 같은 판정 궤적에 맞춥니다.
            Add(path,Alpha(tint,.8f),width*.95f,life,0,!thrusting);
            Add(path,white,width*.28f,life*.68f,0,!thrusting);
            if(!thrusting||last)Add(Offset(path,Vector3.up*.045f-right*.035f),Alpha(tint,.5f),width*.13f,life,.025f,!thrusting);
            Vector3 tip=path[path.Length-1];
            if(thrusting)
            {
                Add(Circle(tip,right,Vector3.up,last?.2f:.07f),Alpha(tint,.65f),last?.035f:.016f,life,0,false,forward*.05f,.5f);
                if(last)Shards(tip,forward,right,tint,6,.24f,.16f);
            }
            else if(axe&&impact)
            {
                var ring=Copy(impact);Vector3 center=Vector3.zero;foreach(var p in ring)center+=p;center/=ring.Length;
                Add(ring,Alpha(tint,.7f),last?.12f:.065f,.32f,0,false,Vector3.zero,.12f);
                Add(ring,white,.025f,.14f);
                // 균열은 바닥에만 남고, 파편은 짧게 솟은 뒤 사라집니다.
                for(int i=0;i<(last?8:5);i++)
                {
                    float a=(i+.3f)*2.39996f;Vector3 radial=right*Mathf.Cos(a)+forward*Mathf.Sin(a);float r=last?1.1f:.5f+step*.2f;
                    Add(new[]{center+radial*.12f,center+radial*r*.45f+right*.08f,center+radial*r*.7f-right*.07f,center+radial*r},Alpha(tint,.7f),.045f,.34f,.01f);
                }
                Shards(center+Vector3.up*.06f,Vector3.up,right,tint,last?10:6,last?.65f:.32f,.32f);
            }
            else
            {
                // 베기 끝의 파편은 궤적에만 붙이고 적중 섬광은 별도 호출합니다.
                for(int i=0;i<6;i++)
                {
                    int index=Mathf.Clamp((i+1)*path.Length/8,0,path.Length-1);var p=path[index];var radial=(p-origin).normalized;
                    Add(new[]{p,p+radial*.08f+Vector3.up*.025f,p+radial*.2f},Alpha(tint,.75f),.035f,.19f,i*.008f,false,radial*.2f);
                }
            }
            Render(0);
        }
        public void InitializeContact(Vector3 point,Vector3 direction,float power,float scale=1)
        {
            StartEffect();
            // 카메라를 향한 흰 섬광과 금빛 파편으로 모든 뼈 소재의 적중을 통일합니다.
            var camera=Camera.main;
            var axis=camera?camera.transform.position-point:-direction;
            axis=axis.sqrMagnitude>.001f?axis.normalized:Vector3.forward;
            point+=axis*.1f;
            var right=Vector3.Cross(Vector3.up,axis).normalized;if(right.sqrMagnitude<.01f)right=Vector3.right;
            var up=Vector3.Cross(axis,right).normalized;
            // 공통 적중 효과만 약 1.6배 확대하고 섬광과 잔향의 가독성을 높입니다.
            float size=.62f*Mathf.Clamp(power,1,1.4f)*Mathf.Clamp(scale,.5f,2);
            var tint=new Color(1,.64f,.16f,1);var white=new Color(1,.98f,.88f,1);
            Add(new[]{point-right*size*1.3f,point,point+right*size*1.3f},white,.15f,.18f);
            Add(new[]{point-up*size,point,point+up*size*1.6f},white,.135f,.19f);
            var diagonal=(right+up).normalized;
            Add(new[]{point-diagonal*size*.85f,point,point+diagonal*size*.85f},white,.085f,.15f);
            diagonal=(right-up).normalized;
            Add(new[]{point-diagonal*size*.7f,point,point+diagonal*size*.7f},white,.075f,.14f);
            Shards(point,axis,right,tint,17,size*1.4f,.34f,1.5f);
            Add(Circle(point,right,up,size*.4f),Alpha(white,.95f),.07f,.2f,0,false,Vector3.zero,1.5f);
            Add(Circle(point,right,up,size*.75f),Alpha(tint,.8f),.045f,.3f,.025f,false,Vector3.zero,.9f);
            Add(Circle(point,right,up,size*.5f),Alpha(tint,.45f),.11f,.18f,0,false,Vector3.zero,.7f);
            Render(0);
        }
        void Shards(Vector3 center,Vector3 forward,Vector3 right,Color tint,int count,float size,float life,float widthScale=1)
        {
            var up=Vector3.Cross(forward,right).normalized;
            for(int i=0;i<count;i++)
            {
                float a=i*2.39996f;var radial=right*Mathf.Cos(a)+up*Mathf.Sin(a);var drift=(radial+forward*.5f)*size;
                var p=center+radial*size*.2f;float length=size*(.3f+(i%3)*.14f);
                Add(new[]{p,p+radial*length*.45f+forward*.025f,p+radial*length},Color.Lerp(tint,Color.white,(i%3)*.28f),(.035f+(i%3)*.012f)*widthScale,life,i%3*.009f,false,drift);
            }
        }
        void Render(float elapsed)
        {
            for(int i=0;i<used;i++)
            {
                var s=animation[i];var line=strokes[i];float age=elapsed-s.delay,t=age/s.life;
                line.enabled=t>=0&&t<1;if(!line.enabled)continue;
                var c=s.color;c.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,1,t));line.startColor=line.endColor=c;line.widthMultiplier=s.width*(1-.55f*t);
                // 보이는 선만 펼치며 실제 피해 판정의 좌표와 두께는 변경하지 않습니다.
                float head=s.reveal?Mathf.Clamp01(.22f+t*6):1;
                for(int j=0;j<s.points.Length;j++)
                {
                    float index=Mathf.Min(j,head*(s.points.Length-1));int lo=Mathf.FloorToInt(index),hi=Mathf.Min(lo+1,s.points.Length-1);
                    var p=Vector3.Lerp(s.points[lo],s.points[hi],index-lo);
                    var position=s.center+(p-s.center)*(1+s.expansion*t)+s.drift*t;
                    if(following&&followTarget)position=followTarget.position+Quaternion.AngleAxis((Time.time-born)*240,followAxis)*(position-followOrigin);
                    line.SetPosition(j,position);
                }
            }
        }
        void Update()
        {
            if(animation==null)return;float t=Time.time-born;
            if(following){if(!followTarget){Destroy(gameObject);return;}Render(.045f+Mathf.Repeat(t,.2f));return;}
            if(t>=totalLife){Destroy(gameObject);return;}Render(t);
        }
    }
}
