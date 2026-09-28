# 여름 몬스터 제작 및 조정

사용자가 제공한 소라게, 뼈 거북, 해파리, 인어 ZIP 모델을 사용합니다.
Blender에서 뼈대, 스키닝, 동작 클립을 제작하고 Unity Generic 애니메이션으로 내보냈습니다.
모델은 각각 삼각형 30,000개입니다. 원본 색상 텍스처를 유지합니다.

## 완성 프리팹

- Assets/Prefabs/monster/CoralMermaid.prefab
- Assets/Prefabs/monster/BlueJellyfish.prefab
- Assets/Prefabs/monster/BoneTortoise.prefab
- Assets/Prefabs/monster/HermitCrab.prefab

현재 Stage3_Summer가 사용하는 SeasonRooms/Summer의 전투방과
별도 SummerJourney7 방의 RoomMonsterSpawner에 연결했습니다.
기존 3마리 랜덤 조합 및 동일 종류 3마리 방지 규칙을 유지합니다.
사용자 요청에 따라 Summer_Room05_Synthesis와 Summer_Room07_Shop에도
동일한 랜덤 3마리 소환기를 연결했습니다. 상점/합성 구조물은 유지합니다.

## Inspector 조정

각 프리팹 루트의 SummerMonsterEnemy에서 이동속도, 공격 거리, 경고 시간,
공격 반경, 공격 시간, 후딜, 다음 공격 대기 시간을 조절할 수 있습니다.

| 몬스터 | 기본 동작 및 수치 |
|---|---|
| 인어 | 0.9초 잠수 → 최대 4초 수중 추적(속도 3.5) → 0.85초 경고 → 반경 2 기습 → 1.1초 후딜 |
| 해파리 | 0.85초 발광/범위 경고 → 자신 중심 반경 3.4 낙뢰 → 1.1초 후딜 |
| 뼈 거북 | 속도 1.1 추적, 0.65초 물기 예고, 전방 중심 반경 1.05 물기 |
| 뼈 거북 방어 | 집어넣기 0.25초 → 방어 1.5초 → 나오기 0.25초 → 방어 쿨타임 4초. 물기 준비/공격 중 방어 전환 금지 |
| 소라게 | 0.85초 준비 → 거품 3개. 속도 2, 회전 55도/초, 수명 7초. 적중 시 이동/구르기 속도 30% 감소, 3초 유지 |

거품 재적중은 감속 강도를 곱하지 않고 지속시간만 갱신합니다.
거품 한 개가 적중해도 나머지는 유지됩니다. 구르기 중 거품 접촉은 감속 없이 소멸합니다.
인어는 잠수 중 본체 충돌을 끄고 수면 물결/그림자를 표시합니다.
SubmergedEffectAnchor와 BubbleOrigin을 프리팹에서 조정할 수 있습니다.
Surface Effect Height는 이동 표면보다 높은 수면에 경고 표시가 묻히지 않게 하는 높이입니다.

## 기존 코드 변경 범위

- CombatHealth: 선택적 CombatDamageGate 검사 한 줄 추가. 거북 방어가 이 경로를 사용합니다.
- PlayerMotor: BubbleDebuff가 있을 때 실제 이동/구르기 속도에 감속 계수 적용.
- StageOneDirector: 게임 종료 시 여름 몬스터 AI와 거품 정리.
- StageEnemy는 수정하지 않았습니다.
- VARCO 패키지 참조를 현재 PC에 존재하는 C:/Users/kking/Desktop 경로로 수정했습니다.

## 검증

SummerMonsterValidation은 빌드에 들어가지 않는 Editor 전용 검증 도구입니다.
CreateArena → 플레이 모드 → Run 순서로 임시 테스트 장면에서 실행합니다.
23개 전투 동작 검사를 통과했습니다. 거북 턱의 전방 위치와 인어의 잠수 회전을 포함합니다. 보고서: Builds/VfxValidation/SummerValidation.txt.
실제 Stage3_Summer에서 소환, NavMesh 이동, 첫 방 처치 후 다음 방 생성과
새 몬스터 소환, 수면 위 범위 표시를 확인했습니다. 확인 후 플레이 모드는 종료했습니다.
게임 빌드와 장시간 밸런스 테스트는 이번 작업에 포함하지 않았습니다.

rig_and_animate.py는 Blender 제작 스크립트입니다. 원본 SummerMonsters.blend를 보존하고,
수정한 거북 뼈대와 인어 액션은 SummerMonsters_Refined.blend에 보관했습니다.
refine_mermaid.py는 인어의 Dive/Burrow/Emerge/Recovery 액션을 다시 만드는 스크립트입니다.
Unity 애셋 재생성은 SummerMonsterBuilder.Build, 방 연결 갱신은 ConnectRooms를 사용합니다.
Build는 해당 네 프리팹과 애니메이션 애셋을 다시 생성하므로 수동 Inspector 변경이 있으면 먼저 보관해야 합니다.

## 방향 및 연출 개선

- 거북 원본 메시의 앞뒤를 교정한 뒤 다시 리깅했습니다. 머리와 턱이 전방 공격 영역을 향합니다.
- 인어는 상체, 팔, 꼬리, 지느러미를 함께 움직이며 몸을 기울여 잠수하고 솟구친 뒤 수면으로 돌아옵니다.
- 인어 소용돌이, 물보라 곡면, 포말, 물방울과 안개를 추가했습니다.
- 해파리는 여러 갈래의 긴 낙뢰, 지면 전류, 낙뢰 접촉 섬광과 몸 발광으로 구성했습니다.
- 거북 물기 궤적/방어 방패, 소라게 거품 생성/잔상/파열을 강화했습니다.
- VFX는 시각 표현만 담당하며 기존 별도 범위 판정과 공격 예고 시간을 유지합니다.
- 실제 Stage3_Summer 수면 위에서도 낙뢰와 인어 출수 효과를 확인했습니다. 임시 검사용 몬스터는 저장하지 않았습니다.

## 거북 걷기와 방어 자세 추가 수정

refine_tortoise.py와 SummerMonsters_TortoiseRefined.blend에 최신 거북 작업을 보관합니다.
걷기에서 등껍질의 좌우 회전을 없애고 네 발의 순차 보행으로 변경했습니다.
머리 전체와 발 스키닝을 조정하고 GuardIn/GuardLoop/GuardOut에서 머리와 발을 등껍질 안으로 당깁니다.
스키닝 변경에 맞춰 물기 자세도 보정했습니다. 기존 프리팹 전투 수치는 유지했습니다.
방어 시 몸통을 앞으로 약 18도 숙이고 낮추어 등껍질이 서 보이는 자세를 완화했습니다.
슬로우 피격 효과는 실제 발밑 높이를 기준으로 물결 두 겹과 떠오르는 거품을 표시하며,
종료 전 0.4초 동안 서서히 사라집니다. 감속 수치와 중첩 규칙은 유지합니다.

SummerGateSealing은 여름 9개 방의 문 양옆뿐 아니라 통로 어깨와 문턱 바닥까지 연결합니다.
기존 통로 바닥 폭과 벽 충돌 사이의 빈 공간을 막았으며, 문을 연 상태의 바닥/측면 450개 충돌 검사를 통과했습니다.
