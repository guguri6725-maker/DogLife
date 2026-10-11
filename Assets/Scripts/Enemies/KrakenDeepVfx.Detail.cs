using UnityEngine;
namespace DogLife
{
    public sealed partial class KrakenDeepVfx
    {
        static readonly Color Pearl=new Color(.69f,.97f,.94f,.8f);
        static readonly Color Turquoise=new Color(.10f,.61f,.65f,.28f);
        static Color Alpha(Color c,float a){c.a=a;return c;}
        Vector3 Radial(float angle,float radius,float y=0)=>new Vector3(Mathf.Sin(angle)*radius,y,Mathf.Cos(angle)*radius);
        void Bubble(Vector3 p,float size,float alpha)
        {
            var x=viewRight*size;var y=viewUp*size;
            Quad(p-x-y,p+x-y,p+x+y,p-x+y,new Color(.55f,.92f,.94f,alpha),7);
        }
        void Ribbon(Vector3 a,Vector3 b,float width,Color c)
        {
            var side=Vector3.Cross(b-a,viewForward).normalized*width*.5f;
            Quad(a-side,a+side,b+side,b-side,c,8);
        }
        void Crest(Vector3 p,Vector3 along,Vector3 side,float scale,float alpha)
        {
            // A shell-shaped five-ray crest, not a rune from another season.
            for(int j=-2;j<=2;j++){
                float f=j*.36f;Vector3 tip=p+along*(scale*(1-f*f*.32f))+side*f*scale;
                Stroke(p,tip,.035f,Alpha(Pearl,alpha));
            }
            for(int j=0;j<12;j++){
                float a=Mathf.Lerp(-.72f,.72f,j/12f),b=Mathf.Lerp(-.72f,.72f,(j+1)/12f);
                Stroke(p+along*(scale*(1-a*a*.32f))+side*a*scale,p+along*(scale*(1-b*b*.32f))+side*b*scale,.04f,Alpha(Pearl,alpha));
            }
        }
        void DrawBandDetail(float q,float time)
        {
            Vector3 h=Size*.5f;
            int rows=Mathf.Clamp(Mathf.CeilToInt(Size.z/2.3f),3,18);
            for(int surface=-1;surface<=1;surface+=2){
                float y=surface*h.y;
                // Thin inner contour and shell crests leave the exact outer border legible.
                for(int z=0;z<rows;z++){
                    float depth=Mathf.Lerp(-h.z+.35f,h.z-.35f,(z+.5f)/rows);
                    float alpha=(depth<=Mathf.Lerp(-h.z,h.z,q)? .45f:.15f)*(Impact?1.25f:1);
                    for(int side=-1;side<=1;side+=2){
                        float x=side*(h.x-.28f);
                        Stroke(new Vector3(x,y,depth-.32f),new Vector3(x,y,depth+.32f),.035f,Alpha(Pearl,alpha));
                        Crest(new Vector3(side*h.x*.72f,y,depth),Vector3.forward,Vector3.right,Mathf.Min(.58f,h.x*.17f),alpha);
                    }
                }
                // Fine broken caustic ribbons run inside the band, never into a safe height.
                for(int strip=0;strip<3;strip++)for(int j=0;j<40;j++){
                    float x0=Mathf.Lerp(-h.x,h.x,j/40f),x1=Mathf.Lerp(-h.x,h.x,(j+1)/40f);
                    float z0=Mathf.Clamp(Mathf.Lerp(-h.z,h.z,(strip+1)/4f)+Mathf.Sin(x0*.75f+time*.9f+strip)*.18f,-h.z,h.z);
                    float z1=Mathf.Clamp(Mathf.Lerp(-h.z,h.z,(strip+1)/4f)+Mathf.Sin(x1*.75f+time*.9f+strip)*.18f,-h.z,h.z);
                    Ribbon(new Vector3(x0,y,z0),new Vector3(x1,y,z1),Impact?.22f:.09f,Alpha(Turquoise,Impact?.55f:.25f));
                }
            }
            // Side surfaces communicate a submerged volume without opaque cage walls.
            for(int side=-1;side<=1;side+=2){
                float x=side*h.x;
                Quad(new Vector3(x,-h.y,-h.z),new Vector3(x,h.y,-h.z),new Vector3(x,h.y,h.z),new Vector3(x,-h.y,h.z),new Color(.055f,.38f,.48f,Impact?.16f:.065f),9);
            }
            int count=Impact?68:30;
            if(Impact)for(int wave=0;wave<4;wave++)for(int j=0;j<36;j++){
                float x0=Mathf.Lerp(-h.x,h.x,j/36f),x1=Mathf.Lerp(-h.x,h.x,(j+1)/36f);
                float z=Mathf.Lerp(-h.z,h.z,Mathf.Repeat(time*.8f+wave*.25f,1));
                float y0=Mathf.Sin(x0*.8f+time*4+wave)*h.y*.65f,y1=Mathf.Sin(x1*.8f+time*4+wave)*h.y*.65f;
                Ribbon(new Vector3(x0,y0,z),new Vector3(x1,y1,z),Mathf.Min(.7f,h.y*.6f),new Color(.38f,.82f,.83f,.5f));
            }
            for(int i=0;i<count;i++){
                float cycle=Mathf.Repeat(time*(Impact?.7f:.25f)+i*.618f,1);
                Vector3 p=new Vector3(Mathf.Lerp(-h.x*.94f,h.x*.94f,Mathf.Repeat(i*.754877f,1)),Mathf.Lerp(-h.y,h.y,cycle),Mathf.Lerp(-h.z*.96f,h.z*.96f,Mathf.Repeat(i*.56984f,1)));
                Bubble(p,Mathf.Min(.13f,h.y*.12f)*(1+(i%3)*.22f),Mathf.Sin(cycle*Mathf.PI)*(Impact?.7f:.25f));
            }
        }
        void DrawSector(float q,float time)
        {
            float radius=Size.x,halfHeight=Size.y*.5f,span=Size.z*Mathf.Deg2Rad;
            float front=radius*Mathf.Sqrt(q),head=-span*.5f+span*q;
            for(int surface=-1;surface<=1;surface+=2){
                float y=surface*halfHeight;
                for(int i=0;i<96;i++){
                    float a=-span*.5f+span*i/96,b=-span*.5f+span*(i+1)/96;
                    if(!Impact){
                        Quad(Vector3.up*y,Radial(a,radius,y),Radial(b,radius,y),Vector3.up*y,new Color(.015f,.07f,.12f,.27f),4);
                        Quad(Vector3.up*y,Radial(a,front,y),Radial(b,front,y),Vector3.up*y,new Color(.08f,.52f,.62f,.30f),9);
                        Quad(Radial(a,Mathf.Max(0,front-.1f),y),Radial(a,front,y),Radial(b,front,y),Radial(b,Mathf.Max(0,front-.1f),y),Alpha(Pearl,.72f),6);
                    }else{
                        float behind=head-(a+b)*.5f,fade=behind>=0&&behind<.55f?1-behind/.55f:0;
                        if(fade>0)Quad(Radial(a,.5f,y),Radial(a,radius,y),Radial(b,radius,y),Radial(b,.5f,y),Alpha(Turquoise,fade*.6f),9);
                    }
                    Quad(Radial(a,radius-.08f,y),Radial(a,radius,y),Radial(b,radius,y),Radial(b,radius-.08f,y),Alpha(Pearl,Impact?.24f:.8f),6);
                }
                for(int s=-1;s<=1;s+=2)Stroke(Vector3.up*y,Radial(s*span*.5f,radius,y),.07f,Alpha(Pearl,.65f));
                for(int i=0;i<12;i++){
                    float a=Mathf.Lerp(-span*.5f,span*.5f,(i+.5f)/12);
                    var dir=Radial(a,1);Crest(Radial(a,radius*.88f,y),-dir,Vector3.Cross(dir,Vector3.up),.62f,Impact?.18f:.45f);
                }
                if(Impact)Ribbon(Radial(head,.6f,y),Radial(head,radius,y),.5f,Alpha(Pearl,.8f));
            }
            if(Impact)for(int i=0;i<55;i++){
                float a=head-Mathf.Repeat(i*.618f,1)*.4f;if(a<-span*.5f)continue;
                float r=radius*Mathf.Sqrt((i+.5f)/55);Bubble(Radial(a,r,Mathf.Sin(time*4+i)*halfHeight),.07f+(i%4)*.025f,.65f);
            }
        }
        void DrawBeamDetail(float q,float time)
        {
            float radius=Size.x*.5f;
            int rings=Impact?3:5;
            for(int i=0;i<rings;i++){
                float t=Mathf.Repeat(i/(float)rings-time*(Impact?1.4f:.22f),1);
                Ring(Vector3.forward*Size.z*t,radius*(Impact?.96f:.88f),Impact?.05f:.025f,Quaternion.Euler(90,0,0),Alpha(Pearl,.3f));
            }
            if(Impact){
                // Dense water core and a rounded front make this read as a moving pressure mass.
                float cap=Mathf.Min(radius,Size.z*.4f),start=Mathf.Max(0,Size.z-cap);
                for(int j=0;j<36;j++){
                    float a=j*Mathf.PI/18,b=(j+1)*Mathf.PI/18;
                    Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius*.72f,e=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius*.72f;
                    Quad(d,e,e+Vector3.forward*start,d+Vector3.forward*start,new Color(.12f,.63f,.72f,.72f),9);
                    for(int k=0;k<6;k++){
                        float t=k/6f,u=(k+1)/6f,r0=Mathf.Cos(t*Mathf.PI*.5f),r1=Mathf.Cos(u*Mathf.PI*.5f);
                        Quad(d*r0+Vector3.forward*(start+cap*t),e*r0+Vector3.forward*(start+cap*t),e*r1+Vector3.forward*(start+cap*u),d*r1+Vector3.forward*(start+cap*u),new Color(.42f,.87f,.89f,.68f),9);
                    }
                }
                for(int j=0;j<5;j++){
                    float a=j*Mathf.PI*.4f;var offset=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius*.4f;
                    Ribbon(offset,offset+Vector3.forward*Size.z,.48f,Alpha(Pearl,.6f));
                }
            }
            for(int i=0;i<32;i++){
                float t=Mathf.Repeat(time*(Impact?1.2f:.3f)+i*.618f,1),a=i*2.39996f+time;
                var p=new Vector3(Mathf.Cos(a)*radius*.92f,Mathf.Sin(a)*radius*.92f,t*Size.z);
                Bubble(p,Impact?.10f:.045f,Mathf.Sin(t*Mathf.PI)*(Impact?.7f:.35f));
            }
        }
        void DrawPressureDetail(float q,float time)
        {
            float radius=Size.x;
            if(Shape==Form.Vortex){
                Ring(Vector3.zero,radius,.055f,Quaternion.identity,Alpha(Pearl,.5f));
                // Three helical water curtains give the current mass; the outer boundary stays clear.
                for(int arm=0;arm<3;arm++)for(int j=0;j<64;j++){
                    float t=j/64f,u=(j+1)/64f,a=arm*Mathf.PI*2/3+t*7-time*(Impact?2.3f:1),b=arm*Mathf.PI*2/3+u*7-time*(Impact?2.3f:1);
                    float r=radius*(.13f+.84f*t),s=radius*(.13f+.84f*u);
                    float height=Mathf.Sin(t*Mathf.PI)*Size.y*.5f,height1=Mathf.Sin(u*Mathf.PI)*Size.y*.5f;
                    Vector3 p=Radial(a,r,height),n=Radial(b,s,height1);
                    Quad(p,n,n-Vector3.up*(.35f+u*.7f),p-Vector3.up*(.35f+t*.7f),new Color(.07f,.44f,.51f,Impact?.30f:.14f),9);
                    Ribbon(p,n,.25f+t*.4f,Alpha(Pearl,Impact?.46f:.2f));
                }
                for(int spiral=0;spiral<6;spiral++)for(int j=0;j<48;j++){
                    float t=j/48f,u=(j+1)/48f,a=spiral*Mathf.PI/3+t*5-time*1.6f,b=spiral*Mathf.PI/3+u*5-time*1.6f;
                    float r=radius*(.15f+.83f*t),r1=radius*(.15f+.83f*u),height=Mathf.Sin(t*Mathf.PI)*Size.y*.3f;
                    Ribbon(Radial(a,r,height),Radial(b,r1,Mathf.Sin(u*Mathf.PI)*Size.y*.3f),.14f+.32f*t,Alpha(Turquoise,.35f));
                }
            }else{
                // Transparent pressure shell and a readable equator for the radial damage volume.
                float r=Shape==Form.Burst?radius*q:radius;
                for(int lat=0;lat<12;lat++)for(int j=0;j<40;j++){
                    float a=j*Mathf.PI/20,b=(j+1)*Mathf.PI/20,p=-Mathf.PI*.5f+lat*Mathf.PI/12,p1=p+Mathf.PI/12;
                    Quad(Radial(a,r*Mathf.Cos(p),r*Mathf.Sin(p)),Radial(b,r*Mathf.Cos(p),r*Mathf.Sin(p)),Radial(b,r*Mathf.Cos(p1),r*Mathf.Sin(p1)),Radial(a,r*Mathf.Cos(p1),r*Mathf.Sin(p1)),new Color(.08f,.58f,.64f,Shape==Form.Burst?.17f:.07f+q*.08f),9);
                }
                Ring(Vector3.zero,Mathf.Max(.01f,r),.09f,Quaternion.identity,Alpha(Pearl,.75f));
            }
            for(int i=0;i<70;i++){
                float cycle=Mathf.Repeat(time*.32f+i*.618034f,1),a=i*2.39996f+time*.35f;
                float r=Shape==Form.Vortex?radius*(1-cycle):radius*(Shape==Form.Burst?q:Mathf.Sqrt(Mathf.Repeat(i*.754f,1)));
                float y=Shape==Form.Vortex?(cycle-.5f)*Size.y:Mathf.Sin(i*1.7f)*r*.6f;
                Bubble(Radial(a,r,y),.04f+(i%5)*.018f,Mathf.Sin(cycle*Mathf.PI)*.6f);
            }
        }
    }
}
