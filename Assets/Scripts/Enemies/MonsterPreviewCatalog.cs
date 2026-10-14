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
            // 구간 미리보기는 원본 클립 시간을 유지하여 자세와 이펙트를 함께 맞춥니다.
            public float Start = 0, End = 1;
            public float Length => Clips.Sum(c => c.length) * (End - Start);
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
                    Add("대기", "Idle", "Idle");
                    Add("패턴 · 협공", "Command|Idle", "Ready|Strike|Ground|Retract");
                    Add("패턴 · 포격", "Command|Wave|Idle", "Ready|Throw|Retract");
                    Add("패턴 · 소용돌이", "Roar|Wave|Idle", "Emerge|Bind|Retract");
                    Add("패턴 · 휩쓸기", "Wave|Idle", "Ready|Strike|Retract");
                    Add("패턴 · 심장 노출", "Roar|Groggy|Idle", "Ground|Retract|Idle");
                    Add("그로기", "Groggy", "Ground");
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
                    Add("패턴 · 서리 폭발", "Roar|FrostChannel|Idle");
                    Add("패턴 · 창 던지기", "SpearThrow|SpearRelease|Idle");
                    Add("패턴 · 얼음 장벽", "IceWallFull|Idle");
                    Add("등장", "ThroneIdle|ThroneRise|ThroneDraw1|ThroneDraw2");
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
                    Add("패턴 · 꽃 폭풍", "P2Storm|Recovery"); Add("사망", "P2Death");
                }
            }
            else if (model.Contains("HarvestReaper"))
            {
                Add("대기", "Idle");
                Add("공격 · 베기", "CleaveReady|CleaveAttack|Recovery");
                Add("패턴 · 내려찍기", "SlamReady|SlamAttack|Recovery");
                Add("패턴 · 휩쓸기", "SweepReady|SweepAttack|Recovery");
                Add("패턴 · 소환", "SummonReady|SummonAttack|Recovery");
                Add("패턴 · 필살기", "SpecialReady|SpecialHold|SpecialAttack|Recovery"); Add("사망", "Death");
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
