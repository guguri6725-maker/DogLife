using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    // 플레이어 상태와 현재 목표, 클리어 화면을 표시합니다.
    public sealed class PrototypeHUD : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerWeapons weapons;
        public CombatHealth health;
        public string objective = "TRAINING  /  Test movement and all four weapons";
        public bool completed;
        public string completionTitle = "STAGE 1 CLEAR";
        public string nextScene;
        public GameObject crosshair;
        public PlayerHeartHUD heartHUD;
        GUIStyle title, body;
        void Update()
        {
            if(GameSettingsWindow.BlocksGameplayInput)return;
            // Hierarchy에 배치한 조준점은 전투 중에만 표시합니다.
            if (crosshair) crosshair.SetActive(!completed && !health.Dead && Cursor.lockState == CursorLockMode.Locked);
            if (Input.GetKeyDown(KeyCode.F5)) SceneManager.LoadScene(SceneManager.GetActiveScene().path);
            if (!SkillWindow.BlocksGameplayInput && Input.GetKeyDown(KeyCode.Return)) ContinueToNextStage();
        }
        // 클리어 상태일 때 설정된 다음 씬으로 이동합니다.
        public bool ContinueToNextStage()
        {
            if (!completed || StoryBook.BlocksInput || StoryBook.AwaitingClear) return false;
            string destination = NextDestination;
            if (string.IsNullOrEmpty(destination)) return false;
            if(RunEconomy.Instance)RunEconomy.Instance.CarryTo(destination);
            SceneManager.LoadScene(destination); return true;
        }
        // A season-only distribution returns to its title instead of loading an omitted scene.
        string NextDestination => !string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene)
            ? nextScene : Application.CanStreamedLevelBeLoaded("Assets/Scenes/Title.unity") ? "Assets/Scenes/Title.unity" : null;
        // 프로토타입용 즉시 모드 GUI를 화면에 그립니다.
        void OnGUI()
        {
            // 스킬창 위로 기존 즉시 모드 HUD가 겹치지 않게 합니다.
            if (SkillWindow.IsOpen || GameSettingsWindow.IsOpen || StoryBook.IsOpen || StoryBook.AwaitingClear) return;
            if (title == null) { title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold }; body = new GUIStyle(GUI.skin.label) { fontSize = 16 }; }
            if (health.Dead || completed)
            {
                GUI.Box(new Rect(Screen.width / 2 - 200, Screen.height / 2 - 55, 400, 110), GUIContent.none);
                GUI.Label(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 40, 370, 40), completed ? completionTitle : "TRY AGAIN", title);
                GUI.Label(new Rect(Screen.width / 2 - 180, Screen.height / 2, 370, 35), completed && !string.IsNullOrEmpty(NextDestination) ? (NextDestination == nextScene ? "Enter: Next stage   /   F5: Restart" : "Enter: Title   /   F5: Restart") : "Press F5 to restart", body);
            }
        }
    }
}

