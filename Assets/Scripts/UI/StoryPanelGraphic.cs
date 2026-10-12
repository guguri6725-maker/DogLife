using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StoryPanelGraphic : MaskableGraphic
    {
        public bool accent;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Plate(vh,new Rect(r.x,r.y-6,r.width,r.height),new Color(.08f,.035f,.02f),new Color(.08f,.035f,.02f),18);
            Plate(vh,r,new Color(.72f,.43f,.19f),new Color(.36f,.18f,.08f),18);
            r=new Rect(r.x+3,r.y+3,r.width-6,r.height-6);
            Plate(vh,r,accent?new Color(1,.68f,.23f):new Color(.25f,.19f,.15f),accent?new Color(.8f,.34f,.1f):new Color(.13f,.095f,.08f),15);
        }
        void Plate(VertexHelper vh,Rect r,Color top,Color bottom,float radius)
        {
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.25f);int start=vh.currentVertCount;
            vh.AddVert(r.center,Color.Lerp(bottom,top,.5f)*color,Vector2.zero);
            for(int corner=0;corner<4;corner++)for(int j=0;j<=8;j++)
            {
                float a=(corner*90+j*90f/8)*Mathf.Deg2Rad;
                var c=new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,corner<2?r.yMax-radius:r.yMin+radius);
                var p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                vh.AddVert(p,Color.Lerp(bottom,top,Mathf.InverseLerp(r.yMin,r.yMax,p.y))*color,Vector2.zero);
            }
            for(int i=0;i<36;i++)vh.AddTriangle(start,start+1+(i+1)%36,start+1+i);
        }
    }
}
