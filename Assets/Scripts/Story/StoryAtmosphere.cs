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
                float v=Mathf.Repeat(i*.381966f-t*(.008f+i%4*.002f),1);
                var p=new Vector2(Mathf.Lerp(r.xMin,r.xMax,u)+Mathf.Sin(t*.3f+i)*12,Mathf.Lerp(r.yMin,r.yMax,v));
                float size=1.5f+(i%4)*1.3f;
                var c=tint;c.a=.12f+.12f*Mathf.Sin(i+t*.7f);
                Diamond(vh,p,size*3,new Color(c.r,c.g,c.b,c.a*.13f));Diamond(vh,p,size,c);
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
