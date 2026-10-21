using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // Shared painted artwork for title cards and the encounter HUD.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SeasonEmblemGraphic : MaskableGraphic
    {
        [Range(0,3)] public int season;
        static readonly string[] Names={"Spring","Summer","Autumn","Winter"};
        Texture2D artwork;
        int loadedSeason=-1;
        public static Color Accent(int s) => s == 0 ? new Color(.96f,.57f,.65f) : s == 1 ? new Color(.3f,.77f,.84f) : s == 2 ? new Color(1,.57f,.25f) : new Color(.66f,.82f,1);
        public override Texture mainTexture
        {
            get
            {
                int selected=Mathf.Clamp(season,0,3);
                if(!artwork||loadedSeason!=selected){artwork=Resources.Load<Texture2D>("UI/SeasonArt/"+Names[selected]);loadedSeason=selected;}
                return artwork ? artwork : Texture2D.whiteTexture;
            }
        }
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var tex=mainTexture;if(!artwork)return;
            Rect r=GetPixelAdjustedRect();float ratio=(float)tex.width/tex.height;
            float w=Mathf.Min(r.width,r.height*ratio),h=w/ratio;
            Vector2 p=r.center-new Vector2(w,h)*.5f;
            v.AddVert(p,color,new Vector2(0,0));v.AddVert(p+new Vector2(0,h),color,new Vector2(0,1));
            v.AddVert(p+new Vector2(w,h),color,new Vector2(1,1));v.AddVert(p+new Vector2(w,0),color,new Vector2(1,0));
            v.AddTriangle(0,1,2);v.AddTriangle(2,3,0);
        }
    }
}
