using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DogLife
{
    // One presentation for the existing boss HP / phase controllers. No separate HP state.
    public sealed class BossEncounterHUD : MonoBehaviour
    {
        public CanvasGroup group;
        public Text bossName, phaseLabel, progressLabel;
        public Image fill, trail, highlight;
        public SeasonEmblemGraphic emblem;
        CombatHealth health;
        SpringBoarBoss spring;
        SpringBoarPhaseTransition springPhase;
        KrakenPhaseOne summer;
        HarvestReaperBoss autumn;
        FrozenKnightBoss winter;
        float previous = -1, delayed = 1, delayUntil;
        int lastPhase = -1;
        static BossEncounterHUD current;
        public static bool HasTarget(CombatHealth target) => current && current.health == target;
        public static BossEncounterHUD CreateFor(CombatHealth target)
        {
            if (!target || !(target.GetComponent<SpringBoarBoss>() || target.GetComponent<KrakenPhaseOne>() || target.GetComponent<HarvestReaperBoss>() || target.GetComponent<FrozenKnightBoss>())) return null;
            if (HasTarget(target)) return current;
            var prefab = Resources.Load<BossEncounterHUD>("UI/BossEncounterHUD");
            if (!prefab) return null;
            if (current) Destroy(current.gameObject);
            current = Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(current.gameObject, target.gameObject.scene);
            current.health = target;
            current.spring = target.GetComponent<SpringBoarBoss>();
            current.springPhase = target.GetComponent<SpringBoarPhaseTransition>();
            current.summer = target.GetComponent<KrakenPhaseOne>();
            current.autumn = target.GetComponent<HarvestReaperBoss>();
            current.winter = target.GetComponent<FrozenKnightBoss>();
            int season = current.spring ? 0 : current.summer ? 1 : current.autumn ? 2 : 3;
            current.emblem.season = season;
            Color accent = SeasonEmblemGraphic.Accent(season);
            current.fill.color = accent;
            current.highlight.color = Color.Lerp(accent, Color.white, .65f);
            current.bossName.text = new[] { "봄의 수호자 · 거대 멧돼지", "심해의 주인 · 크라켄", "수확의 사신", "설원의 수문장 · 겨울 기사" }[season];
            current.emblem.SetAllDirty();
            return current;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { current = null; }
        void LateUpdate()
        {
            if (!health) { Destroy(gameObject); return; }
            bool engaged = spring ? spring.Engaged : summer ? summer.State != KrakenPhaseOne.EncounterState.Waiting && summer.State != KrakenPhaseOne.EncounterState.Stopped : autumn ? autumn.Engaged : winter && winter.CanDealDamage;
            bool visible = engaged && !health.Dead && !BossCinematicUI.Hidden && !SkillWindow.BlocksGameplayInput && !StoryBook.IsOpen && !SeasonLoadingScreen.IsLoading && !PlayerDeathSequence.IsActive;
            group.alpha = visible ? 1 : 0;
            group.blocksRaycasts = group.interactable = false;
            int phase = springPhase ? springPhase.CurrentPhase : summer ? (summer.State == KrakenPhaseOne.EncounterState.Underwater ? 2 : 1) : winter ? winter.PhaseNumber : 1;
            bool tentacles = summer && phase == 1;
            float value = tentacles ? summer.Kills / (float)summer.RequiredKills : Mathf.Clamp01(health.Current / Mathf.Max(1, health.maxHealth));
            if (lastPhase != phase || previous < 0 || value > previous) delayed = value;
            else if (value < previous) delayUntil = Time.unscaledTime + .3f;
            if (Time.unscaledTime > delayUntil) delayed = Mathf.MoveTowards(delayed, value, Time.unscaledDeltaTime * .55f);
            phaseLabel.text = tentacles ? "PHASE I  /  촉수를 처치하세요" : autumn ? "BOSS ENCOUNTER" : "PHASE " + (phase == 1 ? "I" : "II");
            progressLabel.text = tentacles ? summer.Kills + " / " + summer.RequiredKills : Mathf.CeilToInt(value * 100) + "%";
            SetWidth(fill.rectTransform, value); SetWidth(trail.rectTransform, delayed); SetWidth(highlight.rectTransform, value);
            previous = value; lastPhase = phase;
        }
        static void SetWidth(RectTransform r, float value) { var a = r.anchorMax; a.x = value; r.anchorMax = a; }
        void OnDestroy() { if (current == this) current = null; }
    }
}
