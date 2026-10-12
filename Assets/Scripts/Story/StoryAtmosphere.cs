using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    // Lightweight screen-space dust/petals plus soft top/bottom shading. No world VFX survives a page close.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StoryAtmosphere : MaskableGraphic
    {
        public Color tint=new Color(1,.8f,.6f);
        public bool particles=true;
        public string season;
        void Update(){if(particles)SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Strip(vh,r.xMin,r.xMax,r.yMin,r.yMin+r.height*.34f,new Color(.015f,.025f,.03f,.97f),Color.clear);
            Strip(vh,r.xMin,r.xMax,r.yMax-r.height*.23f,r.yMax,Color.clear,new Color(.015f,.025f,.03f,.6f));
            if(!particles)return;
            float t=Time.unscaledTime;
            for(int i=0;i<22;i++)
            {
                float u=Mathf.Repeat(i*.618034f+t*(.002f+i%3*.0006f),1);
                float direction=season=="summer"?-1:1;
                float v=Mathf.Repeat(i*.381966f-direction*t*(.008f+i%4*.002f),1);
                var p=new Vector2(Mathf.Lerp(r.xMin,r.xMax,u)+Mathf.Sin(t*.3f+i)*12,Mathf.Lerp(r.yMin,r.yMax,v));
                float size=1.5f+(i%4)*1.3f;
                var c=tint;c.a=.12f+.12f*Mathf.Sin(i+t*.7f);
                Diamond(vh,p,size*3,new Color(c.r,c.g,c.b,c.a*.13f));
                if(season=="summer")Bubble(vh,p,size*1.7f,c);
                else if(season=="winter")
                {Diamond(vh,p,size,c);Diamond(vh,p,size*.6f,new Color(.9f,.97f,1,c.a));}
                else Petal(vh,p,size,Mathf.Sin(t*.45f+i)*.8f,season=="autumn",c);
            }
        }
        static void Petal(VertexHelper vh,Vector2 p,float size,float angle,bool leaf,Color c)
        {
            int n=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);
            for(int k=0;k<=12;k++)
            {
                float a=k*Mathf.PI*2/12;
                float x=Mathf.Cos(a)*size*(leaf?1.8f:1.2f),y=Mathf.Sin(a)*size*.65f;
                vh.AddVert(p+new Vector2(x*Mathf.Cos(angle)-y*Mathf.Sin(angle),x*Mathf.Sin(angle)+y*Mathf.Cos(angle)),c,Vector2.zero);
                if(k>0)vh.AddTriangle(n,n+k,n+k+1);
            }
        }
        static void Bubble(VertexHelper vh,Vector2 p,float size,Color c)
        {
            int n=vh.currentVertCount;
            for(int k=0;k<=16;k++)
            {
                float a=k*Mathf.PI*2/16;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                vh.AddVert(p+d*size,c,Vector2.zero);vh.AddVert(p+d*(size-.6f),Color.clear,Vector2.zero);
                if(k>0){int j=n+k*2;vh.AddTriangle(j-2,j,j-1);vh.AddTriangle(j,j+1,j-1);}
            }
        }
        static void Strip(VertexHelper vh,float x0,float x1,float y0,float y1,Color bottom,Color top)
        {
            int n=vh.currentVertCount;
            vh.AddVert(new Vector3(x0,y0),bottom,Vector2.zero);vh.AddVert(new Vector3(x1,y0),bottom,Vector2.zero);
            vh.AddVert(new Vector3(x1,y1),top,Vector2.zero);vh.AddVert(new Vector3(x0,y1),top,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void Diamond(VertexHelper vh,Vector2 p,float size,Color c)
        {
            int n=vh.currentVertCount;
            vh.AddVert(p+Vector2.left*size,c,Vector2.zero);vh.AddVert(p+Vector2.up*size*.55f,c,Vector2.zero);
            vh.AddVert(p+Vector2.right*size,c,Vector2.zero);vh.AddVert(p+Vector2.down*size*.55f,c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
