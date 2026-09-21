using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KarmaOrbGraphic : MaskableGraphic
    {
        [SerializeField] PlayerKarma source;
        [SerializeField, Min(.01f)] float changeSpeed = 1.5f;
        Material instance;
        float lightLevel = .5f, darkLevel = .5f;
        public PlayerKarma Source { get => source; set => source = value; }
        protected override void OnEnable()
        {
            base.OnEnable();
            var template = Resources.Load<Material>("UI/KarmaOrb");
            if (template) { instance = new Material(template); material = instance; }
            if (source) { lightLevel=source.LightRatio;darkLevel=source.DarkRatio; }
        }
        void Update()
        {
            if (!instance) return;
            if (source)
            {
                lightLevel=Mathf.MoveTowards(lightLevel,source.LightRatio,Time.deltaTime*changeSpeed);
                darkLevel=Mathf.MoveTowards(darkLevel,source.DarkRatio,Time.deltaTime*changeSpeed);
            }
            instance.SetFloat("_LightFill",lightLevel);instance.SetFloat("_DarkFill",darkLevel);
            instance.SetFloat("_WaveTime",Time.time);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=GetPixelAdjustedRect();
            vh.AddVert(new Vector3(r.xMin,r.yMin),color,new Vector2(0,0));
            vh.AddVert(new Vector3(r.xMin,r.yMax),color,new Vector2(0,1));
            vh.AddVert(new Vector3(r.xMax,r.yMax),color,new Vector2(1,1));
            vh.AddVert(new Vector3(r.xMax,r.yMin),color,new Vector2(1,0));
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
        }
        protected override void OnDisable()
        {
            material=null;
            if(instance){if(Application.isPlaying)Destroy(instance);else DestroyImmediate(instance);instance=null;}
            base.OnDisable();
        }
    }
}
