using UnityEngine;
namespace DogLife
{
    public sealed class BoneWeaponVfx : MonoBehaviour
    {
        LineRenderer line;
        Color tint;
        float birth,lifetime=.28f,width;
        bool crescent;
        public LineRenderer HitShape=>line;
        public LineRenderer ImpactShape { get; private set; }
        public static BoneWeaponVfx Line(Vector3[] points,Color color,Material material,float size=.09f,float life=.28f)
        {
            var go=new GameObject("Bone weapon effect");var fx=go.AddComponent<BoneWeaponVfx>();
            fx.line=go.AddComponent<LineRenderer>();fx.line.sharedMaterial=material;fx.line.positionCount=points.Length;fx.line.SetPositions(points);fx.line.numCapVertices=5;fx.line.numCornerVertices=4;fx.line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;fx.line.receiveShadows=false;
            fx.tint=color;fx.birth=Time.time;fx.width=size;fx.lifetime=life;fx.line.startWidth=size;fx.line.endWidth=size*.15f;fx.line.startColor=fx.line.endColor=color;return fx;
        }
        // 동물형/인간형은 같은 사거리를 사용하고 합성형만 일반형의 1.5배로 확대합니다.
        public static float SizeFor(InventoryWeapon item)=>item.weaponForm==InventoryWeapon.WeaponForm.Synthesized?1.5f:1f;
        public static BoneWeaponVfx Strike(Vector3 center,Vector3 forward,float reach,InventoryWeapon.WeaponCategory category,int step,Color color,Material material,float scale=1f,bool secondStrike=false)
        {
            if(category==InventoryWeapon.WeaponCategory.Spear&&step<2)
            {
                return Strike(center,forward,reach,InventoryWeapon.WeaponCategory.Sword,step,color,material,scale,secondStrike);
            }
            reach*=scale;
            var right=Vector3.Cross(Vector3.up,forward);
            if(category==InventoryWeapon.WeaponCategory.Axe)
            {
                float power=step==2?1.6f:step==1?1.2f:1;
                var arc=new Vector3[65];
                for(int i=0;i<arc.Length;i++){float a=Mathf.Lerp(-Mathf.PI*.5f,Mathf.PI,i/(float)(arc.Length-1));arc[i]=center+forward*.2f+(forward*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*reach*.65f*power;}
                var slash=Line(arc,color,material,.6f*scale*power,.32f);slash.crescent=true;
                slash.line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.4f,1),new Keyframe(.7f,1),new Keyframe(1,0));slash.line.widthMultiplier=slash.width;
                Vector3 impact=center+forward*reach*.65f;RaycastHit hit;
                if(Physics.Raycast(impact+Vector3.up*2,Vector3.down,out hit,8,1<<8))impact=hit.point+Vector3.up*.04f;else impact.y=center.y-.7f;
                slash.ImpactShape=Ring(impact,right,forward,.65f*scale*power,color,material,4*scale).HitShape;
                if(step==2)slash.ImpactShape=Ring(impact,right,forward,1.4f*scale,color,material,3*scale).HitShape;
                int rays=step==2?16:8;
                for(int i=0;i<rays;i++){float a=i*Mathf.PI*2/rays;var dir=right*Mathf.Cos(a)+forward*Mathf.Sin(a);Line(new[]{impact+dir*.18f,impact+dir*.65f*power*scale+Vector3.up*.25f,impact+dir*power*scale+Vector3.up*(step==2?.7f:.25f)},color,material,.13f*scale,.38f);}
                return slash;
            }
            if(category==InventoryWeapon.WeaponCategory.Dagger)
            {
                bool finisher=step==2;
                var start=center+right*(step==1?-.08f:.06f)*scale+Vector3.up*(step==1?.06f:-.04f);
                var tip=start+forward*reach*(finisher?1.35f:1f);
                var thrust=Line(new[]{start,tip},color,material,(finisher?.65f:.345f)*scale,finisher?.38f:.22f);
                Line(new[]{tip-forward*.3f*scale+right*.14f*scale,tip,tip-forward*.3f*scale-right*.14f*scale},color,material,.18f*scale,.22f);
                Ring(tip,right,Vector3.up,(finisher?.55f:.15f)*scale,color,material,scale*2.55f);
                if(finisher)
                {
                    var core=Color.Lerp(color,Color.white,.55f);core.a=.85f;
                    Line(new[]{start,tip},core,material,.2f*scale,.18f);
                    Ring(tip-forward*.3f*scale,right,Vector3.up,.8f*scale,color,material,scale*2);
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4;var ray=right*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a);
                        Line(new[]{tip+ray*.22f*scale,tip+ray*.85f*scale-forward*.2f*scale},color,material,.12f*scale,.32f);
                    }
                }
                return thrust;
            }
            if(category==InventoryWeapon.WeaponCategory.Sword)
            {
                var arc=new Vector3[49];
                for(int i=0;i<arc.Length;i++)
                {
                    float u=i/(float)(arc.Length-1);
                    float sweep=step==2?1.55f:1.25f;
                    float angle=Mathf.Lerp(-sweep,sweep,secondStrike?1-u:u);
                    float height=step==2?.06f*Mathf.Sin(angle*2):Mathf.Sin(angle)*(step==0?-.45f:secondStrike?-.5f:.3f);
                    arc[i]=center+(forward*Mathf.Cos(angle)+right*Mathf.Sin(angle))*reach+Vector3.up*height*scale;
                }
                var slash=Line(arc,color,material,(step==2?.85f:.65f)*scale,.3f);
                slash.crescent=true;
                slash.line.numCapVertices=0;
                slash.line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.2f,.65f),new Keyframe(.5f,1),new Keyframe(.8f,.65f),new Keyframe(1,0));
                slash.line.widthMultiplier=slash.width;
                return slash;
            }
            if(category==InventoryWeapon.WeaponCategory.Spear)
            {
                var thrust=Line(new[]{center,center+forward*reach},color,material,.38f*scale,.13f);
                Ring(center+forward*reach,right,Vector3.up,.3f*scale,color,material,2*scale);return thrust;
            }
            bool vertical=category==InventoryWeapon.WeaponCategory.Axe||step==2;
            var points=new Vector3[30];
            for(int i=0;i<points.Length;i++)
            {
                float a=Mathf.Lerp(-1.1f,1.1f,i/(float)(points.Length-1));
                points[i]=center+forward*(Mathf.Cos(a)*reach)+(vertical?Vector3.up:right)*(Mathf.Sin(a)*reach*.65f*(step==1?-1:1));
            }
            var effect=Line(points,color,material,(category==InventoryWeapon.WeaponCategory.Axe?.18f:.09f)*scale);
            if(category==InventoryWeapon.WeaponCategory.Axe)Ring(center+forward*reach-Vector3.up*.6f*scale,right,forward,.55f*scale,color,material,scale);
            if(category==InventoryWeapon.WeaponCategory.Dagger)Line(new[]{center+right*.35f*scale,center+forward*reach-right*.35f*scale},color,material,.055f*scale,.18f);
            return effect;
        }
        public static BoneWeaponVfx Ring(Vector3 center,Vector3 x,Vector3 y,float radius,Color tint,Material mat,float widthScale=1f)
        {
            var p=new Vector3[33];for(int i=0;i<p.Length;i++){float a=i*Mathf.PI/16;p[i]=center+(x*Mathf.Cos(a)+y*Mathf.Sin(a))*radius;}return Line(p,tint,mat,.045f*widthScale,.24f);
        }
        void Update()
        {
            float t=(Time.time-birth)/lifetime;if(t>=1){Destroy(gameObject);return;}
            var c=tint;c.a*=1-t;line.startColor=line.endColor=c;
            if(crescent)line.widthMultiplier=width*(1-t*.6f);else line.startWidth=width*(1-t*.6f);
        }
    }
}
