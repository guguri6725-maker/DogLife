using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DogLife
{
    // 전투 Update/피해 함수를 호출하지 않고 기존 시각 효과에만 미리보기 상태를 전달합니다.
    internal sealed class MonsterPreviewEffects
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly Transform root;
        readonly MonoBehaviour[] visuals;
        readonly FallMonsterEnemy fall;
        readonly SummerMonsterEnemy summer;
        readonly WinterMonsterEnemy winter;
        readonly HarvestReaperBoss harvest;
        readonly SpringBoarBoss boar;
        readonly KrakenBoss kraken;
        readonly FrozenKnightBoss knight;
        string previousClip;
        public MonsterPreviewEffects(Transform model)
        {
            root = model;
            fall = model.GetComponent<FallMonsterEnemy>(); summer = model.GetComponent<SummerMonsterEnemy>();
            winter = model.GetComponent<WinterMonsterEnemy>(); harvest = model.GetComponent<HarvestReaperBoss>();
            boar = model.GetComponent<SpringBoarBoss>(); kraken = model.GetComponent<KrakenBoss>(); knight = model.GetComponent<FrozenKnightBoss>();
            visuals = model.GetComponentsInChildren<MonoBehaviour>(true).Where(v => v is FallAttackVfx || v is SummerWaterVfx ||
                v is WinterAttackVfx || v is HarvestReaperVfx || v is SpringBoarVfx || v is KrakenVfx ||
                v is BlossomSpiritChargeVfx || v is DeerChargeVfx).ToArray();
        }
        static object Get(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            return t.GetField(name, Flags)?.GetValue(o) ?? t.GetProperty(name, Flags)?.GetValue(o);
        }
        static void Set(object o, string name, object value)
        {
            if (o == null) return;
            var t = o.GetType();
            var field = t.GetField(name, Flags) ?? t.GetField("<" + name + ">k__BackingField", Flags);
            if (field == null) return;
            if (field.FieldType.IsEnum && value is string) value = Enum.Parse(field.FieldType, (string)value);
            field.SetValue(o, value);
        }
        static object Call(object o, string method, params object[] args)
        { return o?.GetType().GetMethod(method, Flags)?.Invoke(o, args); }
        static float Number(object o, string name, float fallback = 1) { var v = Get(o, name); return v is float ? (float)v : fallback; }
        static void Phase(object owner, string property, string phase, float progress)
        {
            Set(owner, property, phase);
            var state = Get(owner, property);
            float duration = 1;
            var method = owner.GetType().GetMethod("Duration", Flags);
            if (method != null) duration = (float)method.Invoke(owner, method.GetParameters().Length == 0 ? null : new[] { state });
            else duration = Number(owner, "PhaseDuration", Number(owner, "Duration"));
            Set(owner, "elapsed", progress * duration);
        }
        public void Render(string clip, float progress, bool visible)
        {
            bool ready = clip.Contains("Ready") || clip.Contains("Windup") || clip.Contains("Crouch");
            bool attack = clip.Contains("Attack") || clip.Contains("Charge") || clip.Contains("Slam") || clip.Contains("Tusk") || clip.Contains("Land") || clip.Contains("Claw");
            bool recovery = clip.Contains("Recovery") || clip.Contains("Recover");
            if (!visible) ready = attack = recovery = false;
            Vector3 forward = root.forward, origin = root.position;
            if (fall)
            {
                Set(fall, "LockedDirection", forward); Set(fall, "AttackOrigin", origin);
                Phase(fall, "CurrentPhase", ready ? "Anticipation" : attack ? "Active" : recovery ? "Recovery" : "Idle", progress);
                var presentation = root.GetComponent<FallAttackPresentation>();
                if (presentation) presentation.Apply(fall);
            }
            if (summer)
            {
                Set(summer, "facing", forward);
                string phase = ready ? "Warning" : attack ? "Attack" : recovery ? "Recovery" : "Idle";
                if (summer.Kind == SummerMonsterEnemy.Species.Mermaid)
                {
                    // 수심 이동은 생략하고 실제 잠수/출현 기울기를 제자리에서 확인합니다.
                    float pitch = 0;
                    if (clip.EndsWith("_Dive")) { phase = "Dive"; }
                    else if (clip.EndsWith("_Burrow")) { phase = "Burrow"; }
                    else if (clip.EndsWith("_AttackReady")) { phase = "Warning"; }
                    else if (clip.EndsWith("_Emerge"))
                    {
                        phase = "Attack";
                        float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .30f));
                        pitch = Mathf.Lerp(0, -24, rise) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 1, progress)));
                    }
                    var visual = (Transform)Get(summer, "visualRoot");
                    if (visual)
                    {
                        visual.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(pitch, 0, 0);
                        // 잔상 없이 원본 수영 자세를 미리 봅니다.
                        visual.localPosition = (Vector3)Get(summer, "visualHome");
                        foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            skin.enabled = true;
                    }
                }
                if (visible && clip.Contains("Guard")) phase = "Guard";
                Phase(summer, "State", phase, progress);
            }
            if (winter)
            {
                Set(winter, "Forward", forward); Set(winter, "Origin", origin);
                Set(winter, "end", origin + forward * winter.Range);
                Phase(winter, "State", ready ? "Warning" : attack ? (clip.Contains("Claw") ? "SecondAttack" : "Attack") : recovery ? "Recovery" : "Idle", progress);
            }
            if (harvest)
            {
                Set(harvest, "engaged", visible); Set(harvest, "Forward", forward);
                Set(harvest, "CurrentPattern", clip.Contains("Sweep") ? "Sweep" : clip.Contains("Cleave") ? "Cleave" :
                    clip.Contains("Slam") ? "Shockwave" : clip.Contains("Summon") ? "Pumpkins" : clip.Contains("Wedge") || clip.Contains("Gather") ? "SafeWedge" : "Storm");
                // 손짓 클립의 방향과 미리보기 안전 부채꼴을 일치시킵니다.
                bool wedge = clip.Contains("Wedge"), gather = clip.Contains("Gather");
                Set(harvest, "gathering", gather);
                int sector = wedge && char.IsDigit(clip[clip.Length - 1]) ? clip[clip.Length - 1] - '0' : 0;
                Set(harvest, "SafeYaw", root.eulerAngles.y + sector * 45);
                Set(harvest, "stormWarningDuration", harvest.CurrentAttack.warning);
                Phase(harvest, "State", !visible ? "Idle" : ready ? "Warning" : attack ? "Active" : recovery ? "Recovery" : "Idle", progress);
                if (visible && wedge && clip.Contains("Hold")) Phase(harvest, "State", "Gap", progress);
                var patches = (System.Collections.Generic.List<HarvestReaperBoss.Patch>)Get(harvest, "patches");
                patches.Clear();
                if (visible && clip.Contains("Summon"))
                    for (int i = 0; i < harvest.Pumpkin.count; i++)
                    {
                        var point = origin + Quaternion.AngleAxis(i * 360f / harvest.Pumpkin.count, Vector3.up) * forward * 5;
                        float age = ready ? progress * harvest.Pumpkin.explosionWarning : harvest.Pumpkin.explosionWarning + progress * harvest.Pumpkin.explosionDuration;
                        patches.Add(new HarvestReaperBoss.Patch { point = point, born = Time.time - age,
                            exploded = attack && progress > .6f });
                    }
            }
            if (boar)
            {
                Set(boar, "Forward", forward); Set(boar, "Origin", origin);
                Set(boar, "AttackPattern", clip.Contains("Tusk") || clip.Contains("Sweep") ? "Tusk" : clip.Contains("Slam") || clip.Contains("Bloom") ? "Slam" : "Charge");
                Phase(boar, "State", ready ? "Warning" : attack ? "Attack" : recovery ? "Recovery" : "Move", progress);
                RenderBoarTwo(clip, progress, visible, ready);
            }
            if (kraken)
            {
                Set(kraken, "engaged", visible); Set(kraken, "Forward", forward);
                Set(kraken, "CurrentPattern", clip.Contains("Roar") ? "Fury" : "Wave"); Set(kraken, "round", -1);
                Phase(kraken, "State", visible && (clip.Contains("Wave") || clip.Contains("Roar") || clip.Contains("Command")) ? "Warning" : "Idle", progress);
            }
            var skeleton = root.GetComponentInChildren<BlossomSkeletonAttackVfx>(true);
            if (skeleton) skeleton.RenderAt(visible && clip == "Attack" ? progress * skeleton.duration : -1);
            var spirit = root.GetComponent<BlossomSpiritEnemy>();
            if (spirit) { Set(spirit, "casting", visible && clip == "Attack"); Set(spirit, "fired", false); Set(spirit, "castStarted", Time.time - progress * spirit.attackDuration); }
            var deer = root.GetComponent<FloralDeerEnemy>();
            if (deer)
            {
                Set(deer, "phase", attack ? "Charge" : ready ? "Windup" : "Track"); Set(deer, "lockedDirection", forward);
                if (ready || attack) Call(deer, "DrawWarning");
                else if (deer.warning) deer.warning.enabled = false;
            }
            var rabbit = root.GetComponent<SakuraRabbitEnemy>();
            if (rabbit)
            {
                Set(rabbit, "landingPoint", origin + forward * 2 + Vector3.up * Number(rabbit, "Offset", .35f));
                if (ready || attack) Call(rabbit, "DrawMarker", rabbit.landingRadius);
                else if (rabbit.landingMarker) rabbit.landingMarker.enabled = false;
            }
            foreach (var visual in visuals)
                if (visual && visual.gameObject.activeInHierarchy) Call(visual, "LateUpdate");
            foreach (var decoration in root.GetComponentsInChildren<BlossomTelegraphVfx>())
                if (decoration.GetComponent<LineRenderer>().enabled) decoration.RenderPreview(progress);
            if (knight) RenderKnight(clip, progress, visible);
            previousClip = clip;
        }
        void RenderBoarTwo(string clip, float p, bool visible, bool ready)
        {
            var phase = root.GetComponent<SpringBoarPhaseTwo>();
            if (!phase) return;
            phase.Hazards.Clear();
            bool active = visible && clip.Contains("P2");
            Set(phase, "Active", active);
            var fx = root.GetComponentInChildren<SpringBoarVfx>(true);
            if (fx) fx.phaseTwo = phase;
            if (active) Phase(boar, "State", "Move", 0);
            if (!active || clip.Contains("Death") || clip.Contains("Brake")) return;
            bool bloom = clip.Contains("Bloom"), storm = clip.Contains("Storm");
            var strike = (SpringBoarPhaseTwo.Strike)Get(phase, bloom ? "bloom" : storm ? "storm" : "sweep");
            int count = bloom ? Convert.ToInt32(Get(phase, "bloomBranches")) : storm ? 8 : 1;
            for (int i = 0; i < count; i++) phase.Hazards.Add(new SpringBoarPhaseTwo.Hazard {
                shape = SpringBoarPhaseTwo.Shape.Sector, effect = bloom || storm ? SpringBoarPhaseTwo.Effect.Roots : SpringBoarPhaseTwo.Effect.Sweep,
                origin = root.position, forward = Quaternion.AngleAxis(i * 360f / count, Vector3.up) * root.forward,
                range = strike.range, angle = strike.angle, width = strike.width, warning = strike.warning, active = strike.active,
                age = ready ? p * strike.warning : strike.warning + p * strike.active, visualOnly = true });
        }
        void RenderKnight(string clip, float p, bool visible)
        {
            var fx = root.GetComponent<FrozenKnightVfx>();
            if (!fx) return;
            bool slash = clip.Contains("1타2타"), spin = clip.Contains("Spin3");
            bool slam = clip.Contains("Slam"), burst = clip.Contains("Push") || clip.Contains("Roar"), stomp = clip.Contains("Stomp");
            bool summon = clip.Contains("IceWall"), blade = clip.Contains("IceSword"), cold = clip.Contains("FrostChannel"), spear = clip.Contains("Spear");
            // 회전베기는 실제 시전 시점과 초당 확산 속도로 경고와 칼날 고리를 표시합니다.
            if (spin)
            {
                var animator = root.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.name == "Knight");
                var motion = animator ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == clip) : null;
                float length = motion ? motion.length : 1;
                float cast = Number(knight, "spinCastMoment", .55f);
                float age = (p - cast) * length;
                float radius = Mathf.Max(0, age) * Number(knight, "waveSpeed", 8);
                float arena = Number(knight, "arenaRadius", 18);
                if (!visible || (age >= 0 && radius >= arena)) fx.Hide();
                else if (age < 0)
                {
                    fx.Show(FrozenKnightVfx.Shape.Ring, root.position, root.forward, arena, 0, FrozenKnightVfx.Theme.Crack);
                    fx.SetWarningProgress(Mathf.Clamp01(p / Mathf.Max(.001f, cast)));
                }
                else
                {
                    fx.Show(FrozenKnightVfx.Shape.Ring, root.position + Vector3.up * Number(knight, "waveHeight", 1.5f),
                        root.forward, radius, Mathf.Max(0, radius - Number(knight, "waveWidth", .6f)), FrozenKnightVfx.Theme.BladeWave);
                    fx.Activate();
                    Set(fx, "started", Time.time - age);
                }
                Call(fx, "LateUpdate");
                return;
            }
            if (visible && (slash || slam || burst || stomp || summon || blade || spin || cold || spear))
            {
                var attack = (FrozenKnightBoss.Attack)Get(knight, stomp ? "stomp" : "frostBurst");
                var combo = (FrozenKnightBoss.ComboSettings)Get(knight, "combo");
                var crack = (FrozenKnightBoss.CrackSettings)Get(knight, "crack");
                float range = slash ? (p < .5f ? combo.first.range : combo.second.range) : slam ? crack.length : attack.range;
                float width = slash ? Number(knight, "meleeSectorAngle", 120) : slam ? crack.width : 360;
                Vector3 origin = root.position;
                if (summon) { range = 3; width = 0; }
                if (blade) { range = 1.4f; width = .9f; origin += root.forward * 6; }
                if (cold) { range = Number(knight, "arenaRadius", 18) * 2; width = 0; }
                if (spear) { range = Number(knight, "spearMarkerLength", 6); width = 1; origin += root.forward * 6; }
                if (spin) { range = Mathf.Lerp(.5f, Number(knight, "arenaRadius", 18), p); width = Mathf.Max(0, range - Number(knight, "waveWidth", .6f)); origin += Vector3.up * Number(knight, "waveHeight", 1.5f); }
                var theme = slash ? FrozenKnightVfx.Theme.Slash : slam ? FrozenKnightVfx.Theme.Crack : stomp ? FrozenKnightVfx.Theme.Stomp :
                    summon ? FrozenKnightVfx.Theme.SwordConjure : blade ? FrozenKnightVfx.Theme.BladeCharge : spin ? FrozenKnightVfx.Theme.BladeWave :
                    cold ? FrozenKnightVfx.Theme.ColdField : spear ? FrozenKnightVfx.Theme.SpearAim : FrozenKnightVfx.Theme.Burst;
                fx.Show(slash ? FrozenKnightVfx.Shape.Sector : slam || spear ? FrozenKnightVfx.Shape.Line : FrozenKnightVfx.Shape.Ring,
                    origin, root.forward, range, width, theme);
                // 대검은 실제 보스에 연결된 정규화 타격 구간을 그대로 사용합니다.
                float impact = burst ? Number(knight, "pushCastMoment", .55f) : slam ? Number(knight, "slamImpactTime", .4f) : .55f;
                float end = 1;
                // 밀쳐올리기의 실제 공격 직전 지연을 미리보기에도 반영합니다.
                if (clip.Contains("Push"))
                {
                    var animator = root.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.name == "Knight");
                    var motion = animator ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == clip) : null;
                    if (motion) impact += Number(knight, "attackLeadTime", .12f) / Mathf.Max(.001f, motion.length);
                }
                int swing = 0;
                if (slash)
                {
                    var first = (Vector2)Get(knight, "firstHitWindow");
                    var second = (Vector2)Get(knight, "secondHitWindow");
                    swing = p > first.y ? 1 : 0;
                    var window = swing == 0 ? first : second;
                    impact = window.x; end = window.y;
                }
                fx.SetWarningProgress(Mathf.Clamp01(p / Mathf.Max(.001f, impact)));
                if (p >= impact && p <= end) { fx.Activate(); Set(fx, "started", Time.time - (p - impact)); }
                if (slash && p > end) fx.Hide();
                if (previousClip != clip) fx.BeginSlash(swing);
            }
            else fx.Hide();
            Call(fx, "LateUpdate");
        }
    }
}
