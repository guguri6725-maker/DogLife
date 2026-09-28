# Kraken — 여름 보스

Assets/Prefabs/monster/KrakenBoss.prefab
여름 마지막 방 Assets/Scenes/SeasonRooms/SummerJourney7/Summer_Room07.unity의 RoomMonsterSpawner에 연결 완료. 얕은 수면 추가, 기존 바닥 충돌 유지.

## 구현
- 1: 길고 좁은 촉수 내려찍기, 경고 후 30% 타격 프레임부터 공격 Trigger, 노출 중 HitCollider 활성화.
- 2: 상세 명세를 따라 사방 촉수 내려찍기. Count / Simultaneous / Delay 조절. 최초 위치 고정.
- 3: 위치 고정 원형 포획. 회피 가능. 피해 성공 시 선택적 구속 후 Throw 애니메이션과 기존 PlayerMotor 넉백으로 던짐.
- 4: 두 촉수 내려치기 + 이동 파도. 좌우 Box Trigger 사이의 실제 안전 틈. 빠른 파도는 이동 경로를 포함한 OverlapBox 검사.
- 5: HP 50% 이하 1회. 현재 공격/후딜 종료 후 포효, 시간차 촉수 묶음 반복, 마지막 대형 촉수, 그로기. 일반 공격과 동시 실행하지 않음.
- 동일 공격의 여러 촉수/파도 양쪽/Trigger와 Overlap 검사는 본체 단위 HashSet으로 중복 피해 방지. 각 특수 웨이브 시작 시 초기화.
- 노출 촉수는 본체 자식. 별도 CombatHealth 없음. 기존 플레이어 근접/화살/부메랑의 GetComponentInParent<CombatHealth>로 본체에 피해 전달.
- 플레이어의 구르기·피격 무적은 기존 CombatFeedback/CombatHealth 그대로 적용.
- 사망/비활성/방 이탈 시 피해 판정, 노출 Collider, 파도, 구속 정리.

## 스크립트
신규: KrakenBoss, KrakenTentacle, KrakenAttackTrigger, KrakenWave, KrakenVfx.
에디터 제작: KrakenBuilder. 테스트: KrakenValidation.
수정: RoomMonsterSpawner (BindRoom), StageOneDirector (종료 시 정지). 기존 Player/HP/Damage 스크립트 변경 없음.

## 프리팹 구조와 연결
KrakenBoss: CombatHealth(2500), Rigidbody(kinematic), CapsuleCollider, NavMeshAgent(스폰 검색만), KrakenBoss, CombatFeedback, SeasonEnemyHealthBar.
Body Animator 및 RestingTentacle Animator 2개, KrakenWaterEffects(KrakenVfx/MeshFilter/MeshRenderer).
KrakenTentacle: Animator, KrakenTentacle, AttackColliderLine(Box Trigger), AttackColliderCircle(Sphere Trigger), HitColliderLine(Box), HitColliderCircle(Sphere).
KrakenWave: KrakenWave, 좌/우 Box Trigger 각각 KrakenAttackTrigger.
모든 Reference 연결 완료. HitCollider는 노출 중만 활성, 플레이어와 물리 충돌을 IgnoreCollision으로 제외하고 무기 공간 질의에는 유지.

## 애니메이션
공통 Animator float: PhaseSpeed. 코드에서 상태 CrossFade, 별도 Trigger 불필요.
본체 6개 뼈 / 5개 상태: Idle, Command, Roar, Wave, Groggy.
촉수 13개 뼈 / 9개 상태: Idle, Ready, Strike, Ground, Retract, Dive, Emerge, Bind, Throw.
긴 촉수의 내려찍기 방향/폭은 KrakenTentacle의 visual 회전·스케일과 리깅 애니메이션을 함께 사용.
FBX/컨트롤러/클립/재질: Assets/Art/Kraken.
원본 보관: ArtSource/Kraken/Original. 작업본: Kraken.blend, 제작 스크립트 rig_and_animate.py.

## Inspector
KrakenBoss: Encounter, Normal Patterns, Surrounding, Binding, Tide, Fury.
KrakenTentacle: authoredLength/width/depth, slamLean, groundThickness, retractTime, impactTime.
KrakenVfx: 색, 테두리, 채움 투명도, 물보라 높이, 파도 흰 포말 두께.
파도 안전 틈은 Tide.safeGapWidth/safeGapOffset. 특수 패턴 횟수와 촉수 수는 Fury.waveCount/tentaclesPerWave.

## Unity 직접 작업
추가 연결 불필요. Stage3_Summer 실행 후 7번째 방에서 확인. 수치 조절은 KrakenBoss 원본 프리팹에서 변경.
제작 프리팹을 재생성할 때만 DogLife.EditorTools.KrakenBuilder.Build 사용(기본값으로 재생성되므로 수동 조정 후 임의 실행 금지).

## 검증
Builds/VfxValidation/KrakenValidation.txt: 28개 런타임 검사 PASS.
예고 중 무피해, 방향/위치 고정, 노출 촉수 본체 HP 전달, 비노출 Collider 해제, 중복 피해 방지, 사방 동시/순차, 포획 회피/구속/던짐, 파도 틈/외곽 피해, HP 대기/1회/최종 공격/그로기, 본체 제자리, 종료 정리.
실제 Summer_Room07: (287.4,0.33,0) 소환, 입장 전 비전투, 입장 후 전투, encounter HP 객체 1개, 사망 비활성 확인.
기존 MonsterTest의 저장하지 않은 편집을 보존하고 런타임 임시 공간에서 검증. 빌드 생성 없음.

