using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DogLife
{
    // 준비·공격·회복 클립을 하나의 선택 항목으로 묶는 미리보기 전용 목록입니다.
    internal static class MonsterPreviewCatalog
    {
        internal sealed class Action
        {
            public string Name;
            public AnimationClip[] Clips;
            public string[] Companion;
            public string RuntimePattern;
            public float RuntimeLength;
            public float[] PhaseDurations;
            // 구간 미리보기는 원본 클립 시간을 유지하여 자세와 이펙트를 함께 맞춥니다.
            public float Start = 0, End = 1;
            public float Length => RuntimeLength > 0 ? RuntimeLength : Clips.Sum(c => c.length) * (End - Start);
        }

        public static Action[] Build(Animator actor, string model, int phase)
        {
            var source = actor.runtimeAnimatorController.animationClips.Where(c => c).Distinct().ToArray();
            var list = new List<Action>();
            void Add(string label, string sequence, string companion = null)
            {
                var clips = sequence.Split('|').Select(n => Find(source, n)).Where(c => c).ToArray();
                if (clips.Length > 0) list.Add(new Action { Name = label, Clips = clips, Companion = companion?.Split('|') });
            }
            void Part(string label, string clip, float start, float end)
            {
                var found = Find(source, clip);
                if (found) list.Add(new Action { Name = label, Clips = new[] { found }, Start = start, End = end });
            }
            if (model.Contains("CoralMermaid"))
            {
                Add("대기 · 호흡과 꼬리 균형", "Idle");
                Add("이동 · 수면 유영", "Walk");
                Part("입수 ① · 허리 숙이기", "Dive", 0, .23f);
                Part("입수 ② · 상체부터 잠기기", "Dive", .23f, .65f);
                Part("입수 ③ · 꼬리 따라 잠김", "Dive", .65f, 1);
                Add("입수 · 전체 이어보기", "Dive");
                Add("추적 · 수중 유영", "Burrow");
                Add("준비 · 몸과 팔 모으기", "AttackReady");
                Part("출수 ① · 상승 준비", "Emerge", 0, .15f);
                Part("출수 ② · 수면 돌파", "Emerge", .15f, .35f);
                Part("출수 ③ · 몸 펼치기", "Emerge", .35f, 1);
                Add("출수 ④ · 자세 안정", "Recovery");
                Add("출수 · 전체 이어보기", "Emerge|Recovery");
                Add("공격 · 양팔 펼치기", "Attack");
                Add("연속 · 잠복 기습 전체", "Dive|Burrow|AttackReady|Emerge|Recovery");
                Add("연속 · 팔 공격 전체", "AttackReady|Attack|Recovery");
            }
            else if (model.Contains("KrakenBoss"))
            {
                if (phase == 0)
                {
                    Add("대기", "Idle");
                    Add("등장 / 잠수", "Emerge|Idle|Dive");
                    Add("패턴 · 내려찍기", "Ready|Strike|Ground|Retract");
                    Add("패턴 · 붙잡아 던지기", "Ready|Bind|Throw|Retract");
                    Add("패턴 · 촉수 휘두르기", "Ready|Strike|Retract");
                }
                else
                {
                    var two = actor.GetComponentInParent<KrakenPhaseTwo>();
                    // 2페이즈 촉수는 클립 조합 대신 인게임의 긴 촉수 리그로 평가합니다.
                    foreach (var pattern in new[] { "대기", "협공", "포격", "소용돌이", "휩쓸기", "심장 노출", "그로기", "사망" })
                        list.Add(new Action { Name = pattern == "대기" || pattern == "그로기" || pattern == "사망" ? pattern : "패턴 · " + pattern,
                            Clips = source, RuntimePattern = pattern, RuntimeLength = two.PreviewLength(pattern) });
                }
            }
            else if (model.Contains("FrozenKnight"))
            {
                if (phase == 0)
                {
                    Add("대기", "Idle"); Add("이동", "Walk"); Add("대쉬 · 후퇴", "BackDash");
                    Add("공격 · 연속 베기", "1타2타|Idle");
                    Add("패턴 · 지면 강타", "SlamForward|PlantRetrieve");
                    Add("패턴 · 밀쳐 올리기", "PushUpper|Idle");
                    Add("패턴 · 회전 베기", "Spin3Sword|Idle");
                    Add("패턴 · 창 던지기", "SpearThrow|SpearRelease|Idle");
                    Add("패턴 · 얼음 장벽", "IceWallFull|Idle");
                    Add("등장", "ThroneIdle|ThroneRise|ThroneStand|ThroneDraw1|ThroneDraw2");
                    // 발도 직전의 정지 시간을 실제 연출과 맞춥니다.
                    var introduction = list[list.Count - 1];
                    introduction.PhaseDurations = introduction.Clips.Select(c => c.name == "ThroneStand" ? .4f : c.length).ToArray();
                    introduction.RuntimeLength = introduction.PhaseDurations.Sum();
                    Add("그로기 / 기상", "Kneel|StandUp");
                }
                else
                {
                    Add("대기", "MountedIdle", "Idle"); Add("이동", "MountedWalk", "Walk");
                    Add("대쉬 · 후퇴", "MountedBackDash", "Horse_BackDash");
                    Add("공격 · 기승 베기", "MountedReady|IceSwordMounted|MountedIdle", "Rear|Gallop|Idle");
                    Add("패턴 · 돌진", "MountedReady|MountedCharge|MountedIdle", "Rear|Gallop|Idle");
                    Add("패턴 · 발 구르기", "MountedStompReady|MountedStompImpact|MountedIdle", "Rear|Stomp|Idle");
                    Add("패턴 · 얼음 장벽", "MountedReady|IceWallMounted|MountedIdle", "Rear|Stomp|Idle");
                    Add("패턴 · 소환 지휘", "MountedCommand|MountedIdle", "Rear|Idle");
                    Add("방향 전환 · 좌 / 우", "MountedTurnLeft|MountedTurnRight", "Horse_TurnLeft|Horse_TurnRight");
                    Add("그로기", "MountedGroggy", "Groggy"); Add("사망", "MountedDeath", "Groggy");
                }
            }
            else if (model.Contains("SpringBoarBoss"))
            {
                Add("대기", "Idle"); Add("이동", "Walk");
                if (phase == 0)
                {
                    Add("대쉬 · 돌진", "Scrape|Charge|Recovery");
                    Add("공격 · 엄니", "TuskReady|Tusk|Recovery");
                    Add("패턴 · 내려찍기", "SlamReady|Slam|Recovery");
                    Add("포효", "Roar"); Add("그로기", "Stunned"); Add("페이즈 전환", "Metamorphosis");
                }
                else
                {
                    Add("대쉬 · 돌진", "Scrape|Charge|P2Brake");
                    Add("패턴 · 휩쓸기", "P2SweepReady|P2Sweep|P2Brake");
                    Add("패턴 · 개화", "P2BloomReady|P2Bloom|Recovery");
                    Add("패턴 · 꽃 폭풍", "P2Storm|Recovery");
                    Add("사망 · 다리 무너짐", "P2DeathLeft");
                    var death = actor.GetComponentInParent<SpringBoarDeath>();
                    foreach (var action in list.Where(a => a.Name.StartsWith("사망"))) action.RuntimeLength = death ? death.Duration : 5.2f;
                }
            }
            else if (model.Contains("HarvestReaper"))
            {
                Add("대기", "Idle");
                Add("공격 · 직선 올려치기", "CleaveReady|CleaveAttack|CleaveRecovery");
                Add("패턴 · 자루 찍기 / 충격파", "SlamReady|SlamAttack|SlamRecovery");
                Add("패턴 · 수확 휩쓸기", "SweepReady|SweepAttack|SweepRecovery");
                Add("패턴 · 호박 씨앗 소환", "SummonReady|SummonAttack|SummonRecovery");
                Add("패턴 · 낙엽 폭풍", "SpecialReady|SpecialAttack|SpecialRecovery");
                // 긴 폭풍 구간에서 충격파 복사 모션을 천천히 끝까지 재생합니다.
                var reaper = actor.GetComponentInParent<HarvestReaperBoss>();
                if (reaper)
                {
                    var storm = reaper.StormAttack;
                    list[list.Count - 1].PhaseDurations = new[] { storm.warning, storm.duration, storm.recovery };
                    list[list.Count - 1].RuntimeLength = storm.warning + storm.duration + storm.recovery;
                }
                // 여덟 방향의 예고와 폭발을 한 패턴으로 묶어 보여줍니다.
                Add("패턴 · 안전지대 회전", "GatherReady|GatherAttack|" + string.Join("|", Enumerable.Range(0, 8).Select(i => "WedgeReady"+i+"|WedgeAttack"+i+"|WedgeHold"+i)) + "|WedgeRecovery7");
                Add("사망", "Death");
            }
            else
            {
                Add("대기", "Idle"); Add("이동", Find(source, "Walk") ? "Walk" : "Move");
                if (Find(source, "Attack")) Add("공격", "AttackReady|Attack|Claw|Reaim|Recovery");
                Add("대쉬", "Dash");
                if (Find(source, "Charge")) Add("대쉬 · 돌진", "Windup|Charge|Impact|Recover");
                Add("공격 · 도약", "Crouch|Jump|Land|Retreat");
                Add("방어", "GuardIn|GuardLoop|GuardOut");
                if (Find(source, "Burrow")) Add("잠복 / 출현", "Burrow|Dive|Emerge|Recovery"); Add("사망", "Death");
            }
            return list.ToArray();
        }

        public static AnimationClip Find(AnimationClip[] clips, string name) =>
            clips.FirstOrDefault(c => c.name == name) ?? clips.FirstOrDefault(c => c.name.EndsWith("_" + name, StringComparison.Ordinal));
    }
}
