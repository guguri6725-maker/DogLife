using UnityEngine;

namespace DogLife
{
    // 일반 빌드에서도 사용하는 임시 테스트 기능. 제거할 때 이 파일만 삭제하면 됩니다.
    public sealed class SeasonTestWeaponShortcut : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(FindFirstObjectByType<SeasonTestWeaponShortcut>())return;
            var host=new GameObject("SeasonTestWeaponShortcut");
            DontDestroyOnLoad(host);
            host.AddComponent<SeasonTestWeaponShortcut>();
        }

        void Update()
        {
            if(!Input.GetKeyDown(KeyCode.Alpha0)&&!Input.GetKeyDown(KeyCode.Keypad0))return;
            var stream=ForestRoomStreaming.Instance;
            if(!stream||!stream.Ready||stream.Busy||Time.timeScale<=0||SkillWindow.BlocksGameplayInput)return;
            var hud=stream.director.hud;
            if(hud.health.Dead||hud.completed)return;
            TryGrant(hud.motor.GetComponent<PlayerInventory>(),stream.gameObject.scene.path);
        }

        public static bool TryGrant(PlayerInventory inventory,string seasonScene)
        {
            if(!inventory)return false;
            string weaponId;
            switch(seasonScene)
            {
                case "Assets/Scenes/Stage1_Spring.unity": weaponId="dog_bow_synthesized";break;
                case "Assets/Scenes/Stage3_Summer.unity": weaponId="dog_spear_synthesized";break;
                case "Assets/Scenes/Stage3_Fall.unity": weaponId="dog_axe_synthesized";break;
                case "Assets/Scenes/Stage4_Winter.unity": weaponId="dog_sword_synthesized";break;
                default:return false;
            }
            var catalog=Resources.Load<RunWeaponCatalog>("RunWeaponCatalog");
            var weapon=catalog?catalog.Find(weaponId):null;
            if(!weapon){Debug.LogError("테스트 무기를 찾지 못했습니다: "+weaponId);return false;}
            return inventory.AddWeapon(weapon);
        }
    }
}
