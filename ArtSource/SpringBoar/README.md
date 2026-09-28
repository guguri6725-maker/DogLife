# 봄 보스: 거대 뼈 멧돼지

- 현재 원본: Assets/VARCO3DImports/Spring_Boss.usdz. 최초 ZIP 원본은 Original 폴더에 보존.
- 현재 Blender 작업본: SpringBoss_Replacement.blend / 생성 스크립트: rig_replacement.py. 이전 작업본은 SpringBoar.blend에 보존.
- 원본 150,000 삼각형을 그대로 유지하고 20개 뼈대로 새로 리깅. UV/표면 노멀을 보존하고 메시 축소/용접을 적용하지 않습니다. 부위별 가중치와 smoothing, 머리/송곳니 강체 가중치를 적용했으며 앞뒤 다리 관절을 실제 발 위치에 맞췄습니다. 발 접지는 2관절 풀이를 클립에 베이크했습니다.
- 원본 높이 약 0.839 유지. SpringBoarBoss 프리팹 XYZ Scale = 10; 실제 높이 약 8.4m.
- USDZ 원본 색상 4K/노멀 2K를 사용하며 Unity 압축 유지. 보스 한 마리의 원본 디테일을 우선 보존합니다.
- Idle, Walk, Scrape, Charge, TuskReady, Tusk, SlamReady, Slam, Roar, Stunned, Recovery: 11개 동작.

## 배치

Assets/Prefabs/monster/SpringBoarBoss.prefab
봄 스트리밍 순서의 마지막(index 4)은 Spring_Room02.unity입니다. 이 방 RoomMonsterSpawner의 bossPrefab을 연결하여 기존 3마리 일반 소환 대신 보스 한 마리만 소환합니다. 다른 방의 일반 소환 규칙은 유지합니다.

## Inspector 조절

SpringBoarBoss의 SerializeField 값은 월드 단위입니다. HP는 CombatHealth.Max Health(1800).
- 방 입구에서 3m 안쪽으로 들어온 뒤 전투 시작. 방 밖에서는 공격/추적을 멈추고, 보스 이동과 돌진은 방의 입출구 안쪽으로 제한합니다.
- 돌진: 준비 2.2초, 22m/s, 최대 18m, 방향 고정. 종료 후 별도 쿨타임 12초이며 다른 패턴을 한 번 사용해야 다시 선택합니다. 가능한 패턴이 없으면 접근합니다. 벽/문 및 NavMesh 경계에서 정지. 벽 충돌 기절 2.5초.
- 송곳니: 1.2초 경고, 전방 7m/95도. 2하트 피해 + 수평 12/상승 6 넉백. 새 useFullPlayerDamage 옵션은 보스에만 사용하고 기존 일반 공격은 1하트를 유지합니다.
- 내려찍기: 1.35초 경고, 반경 9m까지 확산하는 충격파, 1하트. 옆/뒤 8m 안에서 2초 머무르면 다음 패턴으로 우선 선택.
- 가시: HP 50% 이하부터 랜덤 후보에 추가. 3~5개 고정 위치를 1.1초 예고하고 2.2초 유지, 1하트. 경고 후 플레이어를 따라 움직이지 않습니다.
- 회복 1.1초 후 쿨타임 2.5초 동안 접근/주변 이동. 죽음/비활성화/게임 종료 시 공격과 가시 효과 정리.

## 이펙트

SpringBoarVfx는 경고·꽃잎·돌진 궤적·올려치기·충격파·뼈 가시·기절 꽃송이를 동적 메시로 그립니다. 공용 vertex-color shader를 재사용하고 색은 벚꽃 분홍/아이보리/짙은 적갈색입니다. 별도 Collider로 피해를 주지 않고 Boss의 같은 거리/각도/가시 좌표로 판정합니다.

## 확인

SpringBoarValidationRunner(Editor 전용)의 21개 패턴/피해/기절/정리 검사.
Builds/VfxValidation/SpringBoarValidation.txt 및 Boar_*.png.
실제 Spring 마지막 방에서 적 수 1, scale 10, NavMesh 연결 확인. 보스 처치 후 Cleared=5 및 마지막 출구 개방 확인.
빌드는 생성하지 않았습니다.

2026-09-26 추가 확인: 실제 마지막 방 입구 밖 x=176.6에서 보스 위치/대기 유지 및 Engaged=false, 입장 후 Engaged=true 확인. 돌진 쿨타임 중/직전 돌진/25m 거리에서는 접근 선택, 준비된 15m에서는 돌진, 정면 6m에서는 송곳니 선택 확인. 실제 돌진은 장애물에서 약 11.98m 이동 후 종료. 콘솔 오류 0건.

## 2026-09-26 모델 교체 확인

MonsterTest 플레이 모드에서 새 메시 150,000 삼각형/20개 뼈대, 11개 Animator 상태 재생, Scale 10, 돌진 거리 18/쿨타임 12 유지 확인. 송곳니 올려치기 화면: Builds/VfxValidation/Replacement_PlayTusk.png. 콘솔 오류 0. 현재 씬은 저장 변경하지 않았으며 빌드는 하지 않았습니다.
