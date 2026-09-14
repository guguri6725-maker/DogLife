using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        public SideViewMotor motor;
        public SideViewWeapons weapons;
        public CombatHealth health;
        public string objective = "TRAINING  /  Test movement and all four weapons";
        public bool completed;
        public string completionTitle = "STAGE 1 CLEAR";
        public string nextScene;
        GUIStyle title, body;
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5)) SceneManager.LoadScene(SceneManager.GetActiveScene().path);
            if (Input.GetKeyDown(KeyCode.E)) ContinueToNextStage();
        }
        public bool ContinueToNextStage()
        {
            if (!completed || string.IsNullOrEmpty(nextScene)) return false;
            SceneManager.LoadScene(nextScene); return true;
        }
        void OnGUI()
        {
            if (title == null) { title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold }; body = new GUIStyle(GUI.skin.label) { fontSize = 16 }; }
            GUI.Box(new Rect(18, 18, 680, 150), GUIContent.none);
            GUI.Label(new Rect(34, 28, 650, 35), "DOG LIFE  /  SIDE VIEW PROTOTYPE", title);
            GUI.Label(new Rect(34, 67, 650, 25), objective, body);
            GUI.Label(new Rect(34, 95, 650, 25), $"HP {health.Current:0}/{health.maxHealth:0}   |   {weapons.Current.displayName}   |   Dash {motor.DashRemaining:0.0}s   |   Charge {weapons.Charge:P0}", body);
            GUI.Label(new Rect(34, 127, 650, 25), "A/D Move   SPACE Jump   SHIFT Dash   Mouse Aim / Fire", body);
            GUI.Label(new Rect(24, Screen.height - 42, 900, 30), "1 Rifle   2 Shotgun   3 Sword   4 Charge sword (hold/release)   R Ammo color   F5 Restart", body);
            if (health.Dead || completed)
            {
                GUI.Box(new Rect(Screen.width / 2 - 200, Screen.height / 2 - 55, 400, 110), GUIContent.none);
                GUI.Label(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 40, 370, 40), completed ? completionTitle : "TRY AGAIN", title);
                GUI.Label(new Rect(Screen.width / 2 - 180, Screen.height / 2, 370, 35), completed && !string.IsNullOrEmpty(nextScene) ? "E: Next stage   /   F5: Restart" : "Press F5 to restart", body);
            }
        }
    }
}
