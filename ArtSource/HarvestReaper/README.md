# 수확의 사신 — 가을 보스

## 연결 완료
- Prefab: Assets/Prefabs/monster/HarvestReaperBoss.prefab
- 가을 9번째 방 Autumn_Room09의 RoomMonsterSpawner.bossPrefab에 연결.
- CombatHealth, CombatFeedback, PlayerMotor.ApplyKnockback, SeasonEnemyHealthBar, ForestRoomStreaming의 기존 적 수/클리어 기능 재사용.
- Standing Boss: 이동/Root Motion 없음. 공격 전 플레이어 방향 확인 후 고정. 방 입장 전에 공격하지 않음.
- 기본 HP 2200, 전체 모델 높이(낫 포함) 약 7m. 원본 150,000 삼각형과 텍스처 보존, 18개 뼈대.

## 스크립트
- HarvestReaperBoss.cs: 일반 4종, HP 특수 2종, 공격 판정, 독립 수명의 호박/장판.
- HarvestReaperVfx.cs: 낙엽/낫 궤적/경고/3D 호박/폭발/회전 안전지대. 판정 없음.
- HarvestReaperBuilder.cs: 에디터 전용 모델/클립/재질/프리팹 생성.
- HarvestReaperValidation.cs: 에디터 전용 플레이 검증.
- RoomMonsterSpawner.cs: 생성 보스에 ForestRoom 전달.
- StageOneDirector.cs: 전투 종료 시 보스 및 위험지역 정리.

## Inspector
HarvestReaperBoss의 Sweep / Cleave / Shockwave / Pumpkins / Storm에서 damage, range, width, angle, knockback, lift, warning, duration, recovery, cooldown, hitTime 조절.
Pumpkin에서 개수/생성 반경/간격/위치 검색 횟수/폭발 대기/반경/피해/지속/장판 크기/피해/수명/틱/낙하 속도 조절.
Special에서 HP 기준(70/50/30%), 내부 안전 반경 4m, 외부 23m, 안전각45도, 회전 간격45도, 반복8회, 경고/폭발/대기시간/피해/넉백/폭풍 틱 조절.
기타: 회전 속도, 진입 여백, 최초 딜레이, 공통 쿨타임, 근접 유지 시간, 높이 판정, 지면 탐색/마스크.
VFX에는 별도의 색/불투명도/선 굵기/잎 속도/폭풍 높이/잎 개수/호박 크기 설정.

## Animator
Float: PhaseSpeed. Trigger/Bool 불필요. 코드가 CrossFade로 상태 전환.
13 states/clips: Idle, SweepReady, SweepAttack, CleaveReady, CleaveAttack, SlamReady, SlamAttack, SummonReady, SummonAttack, SpecialReady, SpecialAttack, SpecialHold, Recovery.

## References / Components
루트: CombatHealth, Rigidbody(kinematic), CapsuleCollider, NavMeshAgent(스폰 검색용; 런타임 AI가 비활성화), HarvestReaperBoss, CombatFeedback, SeasonEnemyHealthBar.
모델: Animator + SkinnedMeshRenderer. HarvestReaperBoss.animator 연결 완료.
HarvestEffects: HarvestReaperVfx + MeshFilter + MeshRenderer. boss 및 HarvestVfx 재질 연결 완료.
추가 VFX Prefab/Texture 연결 없이 절차적 메시로 표시. 몸체 원본 BaseColor/Normal 연결 완료.

## 정책
- 일반 공격 도중 HP 기준을 통과하면 해당 공격/후딜이 끝난 뒤 특수 패턴 우선 실행.
- 큰 피해로 여러 기준을 동시에 넘으면 70→50→30 순서대로 한 번씩 실행.
- 안전지대 방향은 패턴마다 한 번 랜덤 결정. 표시가 없는 Gap 동안 다음 방향을 계산하며 부드러운 회전 없음. 8개 방향으로 전 원주를 커버.
- 내부 안전 원과 안전 부채꼴은 피해 제외. 한 폭발당 1회 판정.
- 호박 1개는 플레이어 위치를 목표로 경고 후 폭발. 나머지는 NavMesh 위에 최소 간격을 지켜 생성. 공간이 부족하면 겹쳐 만들지 않음.
- 기존 장판은 다른 일반/특수 공격이 시작해도 유지. 사망/비활성화/전투 종료/방 밖 이탈 시 정리.
- 플레이어 구르기 무적/피격 무적을 기존대로 존중. 피해량은 보스 설정값을 적용.

## 검증
Builds/VfxValidation/HarvestReaperValidation.txt: 26개 검증 PASS.
방향/판정/예고 중 무피해/단발 피해/호박 간격/장판 유지·만료/8회 폭발/Gap 표시 정리/HP 순서·일회성/폭풍 틱/제자리 유지/종료 정리.
Blender 작업본: ArtSource/HarvestReaper/HarvestReaper.blend
생성 스크립트: ArtSource/HarvestReaper/rig_and_animate.py
원본: Original 폴더. 빌드는 생성하지 않음.

실제 Stage3_Fall의 9번째 방: 보스 위치 (376.2, 0.33, 0), Enemies=1, 입장 전 Engaged=false, 입장 후 HP70% SafeWedge 실행, 처치 후 Cleared=9 확인. 검증 종료 후 원래 MonsterTest 씬으로 복귀. 콘솔 오류 0.

## 2026-09-29 무기 궤적 / 보스 VFX 개선
- 오른손과 Scythe 피벗을 일치시키고 팔 IK 기반으로 휘두르기/내려베기/내려찍기 동작 재제작. 기존 13개 AnimationClip GUID 유지.
- Blender에서 6개 공격 클립의 전체 프레임 손–낫 피벗 오차 최대 0.00000006 확인.
- 실제 낫 끝을 따라가는 황금 잔상, 단풍 문양의 다중 범위 테두리, 충격파 불꽃, 호박 폭발 낙엽, 낙엽 폭풍 강화.
- HarvestBladeTip / weaponTip / weaponGrip 프리팹 연결 완료. 수치 조절은 HarvestEffects의 HarvestReaperVfx.
- 공격 피해/사거리/쿨타임 유지. 독립 런타임 검증 공간에서 26개 전투 검증 PASS, 마지막 VFX 변경 후 콘솔 오류 0 및 범위 이미지 확인.
- MonsterTest의 저장되지 않은 사용자 편집은 유지하고 플레이 모드 종료. 빌드 생성하지 않음.

낫 동작 2차 개선: 가로베기 220도 궤적, .35 타격 프레임 중심의 가속과 후속 동작, 양팔 IK/골반/몸통 연계. 내려베기 전신 숙임 추가. 무기와 무관한 공중 부채꼴 삭제, 낫 끝의 실제 위치/방향을 보존하는 2겹 잔상으로 변경. 기존 13개 클립 GUID 보존. 4개 클립 160프레임 손–낫 피벗 검사 최대 오차 0.00000003.

낫 방향 수정: 가로베기 궤적 반전, 내려찍기는 자루축 정렬 후 90도 롤로 날의 평면을 전방 수직면에 정렬. 범위 사거리/각도를 직접 사용하는 3겹 가로베기 효과 추가. 기존 내부 타이밍 채우기 유지.

## 70% / 30% 특수 패턴 개편
기존 외곽 45도 안전지대 방식은 아래로 대체.
1. Gather Warning(기본 3초): 보스 주변 반경 7만 안전, 외곽 전체 경고.
2. Gather Duration(기본 2초): 외곽 낙엽 폭풍 활성화, 안쪽 원 전체 안전.
3. 안쪽 원 중 45도만 안전. 나머지는 경고 후 폭발. 기존 8회/45도 간격 이동 유지.
외곽 폭풍은 3단계의 경고·폭발·간격 동안 계속 유지하며 Outer Damage / Outer Tick Interval로 별도 판정. 안쪽 폭발은 넉백 없이 제자리 회피를 허용. 50% 패턴은 유지.
시작 시 SummonReady/SpecialAttack, 안쪽 반복 시 SlamReady/SlamAttack, 간격 시 SpecialHold 사용.
Special Inspector: innerRadius, outerRadius, gatherWarning, gatherDuration, outerDamage, outerTickInterval 및 기존 warning/repeats/safeAngle 조절.
새 판정/단계 전환 포함 28개 런타임 검증 PASS, 콘솔 오류 0.

## 회전 보정
Idle의 일정 속도 회전을 SmoothDampAngle 기반 가속/감속으로 변경. Begin에서 목표 방향으로 즉시 회전하던 코드 제거. 가로/세로 공격은 목표를 8도 이내로 바라본 후 시작하고, 예고부터 타격 종료까지 Forward 고정. 후딜 마지막 35%부터 부드러운 재조준. 근접 1.5 이내 방향 갱신 정지, 1.5도 미세 회전 무시.
Inspector의 Smooth Facing: Turn Smooth Time / Turn Dead Zone / Minimum Facing Distance / Attack Facing Tolerance / Recovery Turn Start. 기존 Turn Speed가 최대 회전 속도.
60fps 90도 회전 검증: 첫 프레임 0.158도, 최대 프레임 1.622도 미만, 목표 도달/근접 안정/공격 시작 순간 회전 없음/공격 방향 일치 확인.
