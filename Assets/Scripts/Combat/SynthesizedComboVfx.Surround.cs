using UnityEngine;

namespace DogLife
{
    public sealed partial class SynthesizedComboVfx
    {
        Transform followTarget;
        Vector3 followOrigin,followAxis;
        bool following;
        static readonly Color Gold=new Color(1,.58f,.08f,.95f);
        static readonly Color PaleGold=new Color(1,.88f,.38f,1);

        // 공통 공격 궤적 위에 합성형 전용 장식을 얹습니다. 이 선들은 피해 판정에 전달하지 않습니다.
        public void InitializeSurround(LineRenderer hit,LineRenderer impact,Vector3 origin,Vector3 forward,InventoryWeapon.WeaponCategory category,int step,int thrust)
        {
            StartEffect();forward.Normalize();var right=Vector3.Cross(Vector3.up,forward).normalized;
            var path=Copy(hit);bool last=step==2&&(category!=InventoryWeapon.WeaponCategory.Spear||thrust==12);
            if(category==InventoryWeapon.WeaponCategory.Axe&&impact)
            {
                var ring=Copy(impact);var center=Vector3.zero;foreach(var p in ring)center+=p;center/=ring.Length;
                float radius=last?1.65f:1+step*.2f;
                Add(ring,Gold,last?.11f:.07f,.4f,0,false,Vector3.zero,.28f);
                for(int i=0;i<8;i++)
                {
                    float a=i*2.39996f;var d=right*Mathf.Cos(a)+forward*Mathf.Sin(a);
                    Add(new[]{center+d*.12f,center+d*radius*.4f+right*.1f,center+d*radius*.7f-right*.06f,center+d*radius},Gold,.065f,.42f,i%2*.018f);
                    var p=center+d*radius*.45f+Vector3.up*.1f;
                    var lift=(Vector3.up*(last?.8f:.45f)+d*.4f);
                    Add(new[]{p,p+right*.09f+Vector3.up*.16f,p+Vector3.up*.32f,p-right*.09f+Vector3.up*.16f,p},PaleGold,.07f,.36f,i%3*.015f,false,lift);
                }
            }
            else if(category==InventoryWeapon.WeaponCategory.Spear&&step==2)
            {
                Vector3 tip=path[path.Length-1];
                for(int n=0;n<2;n++)Add(Helix(origin,tip,right,Vector3.up,last?.23f:.15f,n*Mathf.PI),n==0?Gold:PaleGold,last?.075f:.045f,last?.27f:.14f);
                if(last){Add(Circle(tip,right,Vector3.up,.42f),Gold,.055f,.26f,0,false,Vector3.zero,.6f);Shards(tip,forward,right,Gold,9,.4f,.25f,1.3f);}
            }
            else if(category==InventoryWeapon.WeaponCategory.Dagger)
            {
                Add(Offset(path,right*.12f+Vector3.up*.1f),Gold,.10f,last?.25f:.16f,0,false,-forward*.18f);
                Add(Offset(path,-right*.12f-Vector3.up*.08f),PaleGold,.065f,last?.22f:.13f,.02f,false,-forward*.25f);
                Shards(origin, -forward,right,Gold,last?10:6,last?.45f:.28f,.2f,1.3f);
            }
            else
            {
                // 검·창의 휘두르기에는 원래 궤적과 같은 방향의 이중 잔광을 남깁니다.
                for(int n=0;n<2;n++)
                {
                    var echo=new Vector3[path.Length];
                    for(int i=0;i<path.Length;i++)echo[i]=origin+(path[i]-origin)*(1.05f+n*.055f)+Vector3.up*(n==0?.13f:-.13f);
                    Add(echo,n==0?Gold:PaleGold,last?.15f:.095f,last?.4f:.3f,n*.02f,true);
                }
                for(int i=0;i<(last?12:8);i++)
                {
                    var p=path[Mathf.Min(path.Length-1,(i+1)*path.Length/(last?14:10))];var d=(p-origin).normalized;
                    Add(new[]{p,p+d*.12f+Vector3.up*.07f,p+d*.28f},Gold,.06f,.28f,i%3*.012f,false,d*.35f);
                }
            }
            Render(0);
        }
        static Vector3[] Helix(Vector3 start,Vector3 end,Vector3 right,Vector3 up,float radius,float phase)
        {
            var points=new Vector3[65];for(int i=0;i<points.Length;i++){float u=i/(float)(points.Length-1),a=u*Mathf.PI*6+phase;points[i]=Vector3.Lerp(start,end,u)+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius*Mathf.Sin(u*Mathf.PI);}return points;
        }
        public void ShowChargeSurround(Vector3 point,Vector3 forward,float charge)
        {
            StartEffect();var right=Vector3.Cross(Vector3.up,forward).normalized;var up=Vector3.Cross(forward,right).normalized;
            float radius=Mathf.Lerp(.3f,.6f,charge);var tip=point+forward*.55f;
            for(int n=0;n<3;n++)
            {
                float a=Time.time*3+n*Mathf.PI*2/3;var side=right*Mathf.Cos(a)+up*Mathf.Sin(a);
                Add(Helix(point+side*radius-forward*.12f,tip,right,up,radius*.25f,a),n==0?PaleGold:Gold,.055f+charge*.035f,.17f);
                var p=point+side*radius;
                Add(new[]{p-up*.09f,p+right*.07f,p+up*.09f,p-right*.07f,p-up*.09f},PaleGold,.045f,.16f);
            }
            Render(0);
        }
        public void InitializeProjectileSurround(Transform target,bool bow,Vector3 forward)
        {
            StartEffect();following=true;followTarget=target;followOrigin=target.position;followAxis=bow?forward.normalized:Vector3.up;
            var right=Vector3.Cross(Vector3.up,forward).normalized;var up=Vector3.Cross(forward,right).normalized;
            if(bow)
            {
                Add(Helix(followOrigin-forward*.65f,followOrigin+forward*.15f,right,up,.18f,0),Gold,.045f,.5f);
                Add(Helix(followOrigin-forward*.55f,followOrigin+forward*.15f,right,up,.14f,Mathf.PI),PaleGold,.03f,.5f);
            }
            else for(int n=0;n<3;n++)
            {
                var arc=new Vector3[25];for(int i=0;i<arc.Length;i++){float a=n*Mathf.PI*2/3+i/(float)(arc.Length-1)*1.55f;arc[i]=followOrigin+(Vector3.right*Mathf.Cos(a)+Vector3.forward*Mathf.Sin(a))*.78f+Vector3.up*.08f;}
                Add(arc,n==0?PaleGold:Gold,.075f,.5f);
            }
            Shards(followOrigin,followAxis,right,Gold,7,bow?.2f:.5f,.5f);
            Render(.045f);
        }
    }
}
