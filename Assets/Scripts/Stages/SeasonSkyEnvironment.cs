using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife
{
    // The owning season applies once. Underwater fog / camera overrides retain ownership during combat.
    public sealed class SeasonSkyEnvironment:MonoBehaviour
    {
        public SeasonSkyProfile profile;
        public Light sun;
        void OnEnable(){SceneManager.activeSceneChanged+=Changed;}
        void OnDisable(){SceneManager.activeSceneChanged-=Changed;}
        void Start(){if(SceneManager.GetActiveScene()==gameObject.scene)Apply();}
        void Changed(Scene previous,Scene next){if(next==gameObject.scene)Apply();}
        [ContextMenu("Apply season sky")]
        public void Apply()
        {
            if(!profile||SceneManager.GetActiveScene()!=gameObject.scene)return;
            profile.Apply(sun);
            foreach(var root in gameObject.scene.GetRootGameObjects())
                foreach(var camera in root.GetComponentsInChildren<Camera>(true))
                    if(camera.CompareTag("MainCamera")||camera.name.Contains("Overview"))camera.clearFlags=CameraClearFlags.Skybox;
        }
    }
}
