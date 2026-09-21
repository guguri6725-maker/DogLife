using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    public sealed class VitalityPanelGraphic : MaskableGraphic
    {
        void Joint(VertexHelper vh,float x,float y,Color c)
        {
            int n=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,Vector2.zero);
            for(int i=0;i<=16;i++){float a=i*Mathf.PI/8;vh.AddVert(new Vector3(x+Mathf.Cos(a)*5,y+Mathf.Sin(a)*5),c,Vector2.zero);if(i>0)vh.AddTriangle(n,n+i,n+i+1);}
        }
        void Quad(VertexHelper vh,float x,float y,float w,float h,Color c)
        {
            int n=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y+h),c,Vector2.zero);vh.AddVert(new Vector3(x,y+h),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();Color edge=new Color(.65f,.56f,.41f,.9f);
            Quad(vh,r.x,r.y,r.width,r.height,new Color(.055f,.064f,.061f,.84f));
            Quad(vh,r.x,r.y,r.width,1,edge);Quad(vh,r.x,r.yMax-1,r.width,1,edge);
            Quad(vh,r.x,r.y,2,r.height,edge);Quad(vh,r.xMax-2,r.y,2,r.height,edge);
            Quad(vh,r.x+48,r.y+12,1,r.height-24,new Color(.65f,.56f,.41f,.45f));
            // 네 개의 관절 끝과 축으로 그린 작은 뼈 문장.
            Color ivory=new Color(.91f,.84f,.70f,1);
            Quad(vh,r.x+23,r.center.y-14,6,28,ivory);
            Joint(vh,r.x+22,r.center.y+14,ivory);Joint(vh,r.x+30,r.center.y+14,ivory);
            Joint(vh,r.x+22,r.center.y-14,ivory);Joint(vh,r.x+30,r.center.y-14,ivory);
        }
    }
}
