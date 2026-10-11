using UnityEngine;
using UnityEngine.Rendering;

namespace DogLife
{
    [CreateAssetMenu(menuName="Dog Life/Season Sky Profile")]
    public sealed class SeasonSkyProfile:ScriptableObject
    {
        public string timeOfDay;
        public Material sky;
        public Cubemap reflection;
        public Color ambientSky,ambientHorizon,ambientGround;
        public Color lightColor=Color.white,fogColor=Color.gray;
        public Vector3 lightRotation=new Vector3(25,115,0);
        [Min(0)] public float lightIntensity=1;
        [Range(0,1)] public float reflectionIntensity=.45f;
        [Min(0)] public float fogStart=60;
        [Min(1)] public float fogEnd=240;
        public void Apply(Light light)
        {
            RenderSettings.skybox=sky;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=ambientHorizon;RenderSettings.ambientGroundColor=ambientGround;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=fogColor;
            RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=Mathf.Max(fogStart+1,fogEnd);
            RenderSettings.reflectionIntensity=reflectionIntensity;
            if(reflection){RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=reflection;}
            if(light){light.color=lightColor;light.intensity=lightIntensity;light.transform.rotation=Quaternion.Euler(lightRotation);RenderSettings.sun=light;}
        }
    }
}
