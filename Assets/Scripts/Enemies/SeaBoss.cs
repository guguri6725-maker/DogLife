using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(CombatHealth))]
    // 피아누스 보스의 체력 단계와 레이저, 폭탄, 해일, 소환 패턴을 제어합니다.
    public sealed class SeaBoss : MonoBehaviour
    {
        public GameObject boneFishPrefab, miniPianusPrefab;
        public Material hazardMaterial;
        public float arenaLeft = 139, arenaRight = 160;
        // 바다 보스도 폭과 깊이가 있는 3D 전장을 사용합니다.
        public float arenaBack = -8, arenaFront = 8;
        public string State { get; private set; } = "Waiting";
        public int Phase { get; private set; }
        public int Lasers { get; private set; }
        public int Bombs { get; private set; }
        public int Surges { get; private set; }
        public int SummonWaves { get; private set; }
        public bool FinalCleared { get; private set; }
        public float FinalRemaining { get; private set; }
        readonly List<CombatHealth> summons = new List<CombatHealth>();
        CombatHealth health, player;
        float[] thresholds = { .7f, .5f, .4f, .1f, 0f };
        void Awake() { health = GetComponent<CombatHealth>(); health.minimumHealth = health.maxHealth * .7f; }
        // 플레이어가 입장하면 체력 구간에 맞춰 일반 패턴과 페이즈 패턴을 반복합니다.
        IEnumerator Start()
        {
            var motor = FindFirstObjectByType<PlayerMotor>();
            if (!motor) yield break;
            player = motor.GetComponent<CombatHealth>();
            while (player && !player.Dead && !health.Dead)
            {
                if (!InsideArena()) { State = "Enter the arena"; yield return null; continue; }
                if (Phase < 5 && health.Current <= health.minimumHealth + .01f)
                {
                    health.invulnerable = true;
                    if (Phase == 1) yield return Surge();
                    else yield return Summon(Phase == 4);
                    Phase++;
                    health.minimumHealth = Phase < 4 ? health.maxHealth * thresholds[Phase] : Phase == 4 ? 1 : 0;
                    health.invulnerable = false;
                }
                else
                {
                    if ((Lasers + Bombs) % 2 == 0) yield return Laser(); else yield return Bomb();
                }
                State = "Recovering"; yield return new WaitForSeconds(.8f);
            }
        }
        IEnumerator Laser()
        {
            State = "LASER / Jump above the yellow beam"; Lasers++;
            SeaHazard.Create(health, new Vector3((arenaLeft + arenaRight) / 2, .7f, player.transform.position.z), new Vector3(arenaRight - arenaLeft, .65f, 1.5f), 1.2f, 25, hazardMaterial);
            yield return new WaitForSeconds(1.6f);
        }
        IEnumerator Bomb()
        {
            State = "WATER BOMB / Move away from the marker"; Bombs++;
            float x = Mathf.Clamp(player.transform.position.x, arenaLeft + 1, arenaRight - 1);
            float z = Mathf.Clamp(player.transform.position.z, arenaBack + 1, arenaFront - 1);
            SeaHazard.Create(health, new Vector3(x, 1.8f, z), new Vector3(3, 3.6f, 3), 3, 20, hazardMaterial);
            yield return new WaitForSeconds(3.4f);
        }
        IEnumerator Surge()
        {
            State = "TIDAL SURGE / Stand inside GREEN"; Surges++;
            float safeX = arenaLeft + 6, halfSafe = 2;
            float leftWidth = safeX - halfSafe - arenaLeft, rightWidth = arenaRight - safeX - halfSafe;
            float centerZ = (arenaBack + arenaFront) / 2, depth = arenaFront - arenaBack;
            SeaHazard.Create(health, new Vector3(arenaLeft + leftWidth / 2, 3, centerZ), new Vector3(leftWidth, 6, depth), 5, 60, hazardMaterial);
            SeaHazard.Create(health, new Vector3(safeX + halfSafe + rightWidth / 2, 3, centerZ), new Vector3(rightWidth, 6, depth), 5, 60, hazardMaterial);
            SeaHazard.Create(health, new Vector3(safeX, .08f, centerZ), new Vector3(halfSafe * 2, .12f, depth), 5, 0, hazardMaterial, true);
            yield return new WaitForSeconds(5.5f);
        }
        // 소환된 적을 모두 처치할 때까지 보스의 다음 페이즈 진행을 막습니다.
        IEnumerator Summon(bool final)
        {
            SummonWaves++;
            State = final ? "FINAL / Defeat 3 mini Pianus in 20 seconds" : "SHIELD / Defeat the summoned bone fish";
            summons.Clear();
            for (int i = 0; i < 3; i++)
            {
                var go = Instantiate(final ? miniPianusPrefab : boneFishPrefab, new Vector3(arenaLeft + 4 + i * 5, 1.1f, Mathf.Lerp(arenaBack + 2, arenaFront - 2, i / 2f)), Quaternion.identity, transform.parent);
                summons.Add(go.GetComponent<CombatHealth>());
            }
            float deadline = Time.time + 20;
            while (summons.Exists(h => h && !h.Dead))
            {
                FinalRemaining = final ? Mathf.Max(0, deadline - Time.time) : 0;
                if (final && Time.time >= deadline)
                {
                    // A recoverable penalty replaces the original permanent failure loop.
                    player.Damage(60); deadline = Time.time + 20;
                }
                yield return null;
            }
            FinalRemaining = 0;
            if (final) FinalCleared = true;
        }
        void OnDisable()
        {
            StopAllCoroutines();
            foreach (var h in summons) if (h) Destroy(h.gameObject);
            foreach (var h in FindObjectsByType<SeaHazard>(FindObjectsSortMode.None)) if (h.source == health) Destroy(h.gameObject);
        }
        void OnGUI()
        {
            // 메뉴가 열려 있을 때 보스 정보가 UI를 가리지 않게 합니다.
            if (SkillWindow.IsOpen) return;
            if (!health || health.Dead || !player || !InsideArena()) return;
            GUI.Box(new Rect(Screen.width / 2 - 300, 185, 600, 64), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2 - 285, 190, 580, 25), $"PIANUS   {health.Current:0}/{health.maxHealth:0}   {(health.invulnerable ? "SHIELDED" : "VULNERABLE")}");
            GUI.Label(new Rect(Screen.width / 2 - 285, 216, 580, 25), State + (FinalRemaining > 0 ? $"  {FinalRemaining:0.0}s" : ""));
        }
        bool InsideArena()
        {
            var p = player.transform.position;
            return p.x >= arenaLeft && p.x <= arenaRight && p.z >= arenaBack && p.z <= arenaFront;
        }
    }
}
