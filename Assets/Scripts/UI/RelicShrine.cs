using UnityEngine;
namespace DogLife
{
    // 방 클리어 시 바닥에서 솟아오르는 석상. 다가가 F키를 누르면 유물 카드 3장을 보여줍니다.
    // 지금은 자리표시용 실린더로 세우고, 나중에 실제 모델로 교체하면 됩니다.
    public sealed class RelicShrine : MonoBehaviour
    {
        const float RiseHeight = 2f, RiseDuration = 1f, InteractDistance = 2.5f;
        GameObject player;
        RelicLoadout loadout;
        WeaponRelic[] choices;
        Vector3 risenPosition;
        float riseStarted;
        bool consumed;
        GUIStyle prompt;
        public static RelicShrine Spawn(GameObject player, Vector3 position)
        {
            if (!player) return null;
            var loadout = RelicLoadout.Get(player);
            var choices = RelicDraftUI.PrepareChoices(player, loadout);
            if (choices == null || choices.Length == 0) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Relic Shrine (placeholder)";
            go.transform.localScale = new Vector3(1f, 1.1f, 1f);
            var collider = go.GetComponent<Collider>(); if (collider) Destroy(collider);
            var shrine = go.AddComponent<RelicShrine>();
            shrine.player = player; shrine.loadout = loadout; shrine.choices = choices;
            shrine.risenPosition = position; shrine.riseStarted = Time.time;
            go.transform.position = position - Vector3.up * RiseHeight;
            return shrine;
        }
        void Update()
        {
            if (consumed || !player) return;
            float t = Mathf.Clamp01((Time.time - riseStarted) / RiseDuration);
            transform.position = Vector3.Lerp(risenPosition - Vector3.up * RiseHeight, risenPosition, Mathf.SmoothStep(0, 1, t));
            if (RelicDraftUI.IsOpen) return;
            bool inReach = Vector3.Distance(player.transform.position, transform.position) <= InteractDistance;
            if (inReach && Input.GetKeyDown(KeyCode.F))
            {
                consumed = true;
                RelicDraftUI.Show(player, loadout, choices, () => { if (this) Destroy(gameObject); });
            }
        }
        void OnGUI()
        {
            if (consumed || !player || RelicDraftUI.IsOpen) return;
            if (Vector3.Distance(player.transform.position, transform.position) > InteractDistance) return;
            if (prompt == null) prompt = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width * .5f - 160, Screen.height * .72f, 320, 48), "[F] 유물 고르기", prompt);
        }
    }
}
