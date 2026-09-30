# 겨울 보스 — 얼어붙은 기사

## 현재 패턴 구성 (2026-09-30)

### 원본 속도 / Blender 대검 자세 보정

- 원형 검기는 원본 `3타.anim`에서 손잡이 지지만 보정한 `Spin3Sword`를 사용한다.
- IceWallFull/Mounted, IceSwordMounted, PushUpper, Spin3Sword는 1배속으로 재생한다. 기존 Warning 초에 클립을 압축하지 않고 Wall/IceSword/Push/Spin Cast Moment(0~1)에서 생성/피해를 시작한다. 원격 검은 Remote First/Second Moment에 맞춰 두 번 휘두른다. 원본 동작이 끝나기 전 Idle로 끊지 않는다.
- 원본 길이: 기둥 5.73초, 얼음검 4.83초, 밀어내기 3.27초, 3타 5.07초. 준비 시간이 기존보다 길어지는 것은 원본 속도를 따른 결과다.
- Blender에서 Idle/Walk, IceWallFull, PushUpper, Roar, FrostChannel, Spin3Sword를 보정했다. Idle/Walk와 양손 공격은 보조 손을 손잡이에 맞추고, 한손 시전은 오른손에 검을 안전하게 들고 왼손으로 표현한다. 본 길이를 늘리거나 실행 중 강제 IK를 사용하지 않는다.
- 편집 원본: `ArtSource/FrozenKnight/WinterKnight_SwordMotion.blend`, 재현 스크립트: `refine_sword_motion.py`. Unity 교환 도구는 `FrozenKnightBlenderBridge.Export/Import`이며 기존 클립 GUID를 유지한다. 시전 클립을 재생성했다면 Blender 보정 Import를 마지막에 적용한다.

### 사용자 제공 시전 애니메이션 적용

| 원본 | 적용 패턴 | 하체 |
| --- | --- | --- |
| Winter_IceSword | 기마 얼음검 발사, 원격 검 | MountedIdle 유지 |
| Winter_IceWall | 1페이즈 얼음 기둥 | 원본 전신 동작 |
| Winter_IceWall | 2페이즈 얼음 기둥, 몬스터 소환 | MountedIdle 유지 |
| 밀어내기 | 빙결 폭발 | Idle 유지, 상체만 적용 |

- 적용용 클립은 `IceSwordMounted`, `IceWallFull`, `IceWallMounted`, `PushUpper`이며 `FrozenKnightImportedCastAuthoring`으로 재생성할 수 있다. 루트 이동은 AI에 맡기고 상체 전용 클립은 Spine 아래 동작만 복사한다.
- 원격 검은 두 번 휘두르는 시간 동안 시전 동작을 이어 재생한다. 얼음검 연속 발사는 매번 시전 클립을 다시 시작한다.
- 미사용 상태 14개, 구형 분리 클립 10개(Attack1/2, Slam, SpinReady, SlamReady, BladeReady/Release, MountedSlashReady/Slash, MountedImpact) 및 미사용 FrozenKnightForwardAuthoring을 제거했다. 재생성 도구에서도 삭제 클립의 생성 항목을 제거했다.
- 후속 3타 적용으로 FrostSpin/SwordReady/구형 Attack3 클립과 두 상태도 제거했다. 원본 3타는 Spin3Sword 제작 소스다. Roar는 한기 패턴, MountedCommand는 최후 패턴에서 계속 사용한다.

### 얼음칼 판정 / 점프 회피 / 투창 표시 수정

- 비행 얼음칼은 메시 경계를 판정 상자 로컬 좌표로 변환해 보이는 크기와 위치에 맞춘다. 이동 직후에도 지나온 구간을 검사하며, 바닥 충돌로 비활성화하기 전에 피해 검사를 마친다.
- 얼음칼 생성 중 바닥 경고는 등 뒤 생성점이 아닌 플레이어 몸체 중심 아래 지면에 표시한다. 준비 중 이동을 따라가고 발사 순간 위치에 고정된다. 검 자체의 생성 고리는 검에 유지된다.
- 원격 검은 플레이어의 X/Z와 지면만 사용한다. `remoteSwordHeight` 기본 0.8m 높이에서 휘두르므로 점프로 넘을 수 있다. 회수 단계는 Recovery이며 곡선 이동과 가감속을 사용한다.
- 발악 투창 예고는 플레이어 발밑에서 끝나는 기본 3m 화살표다. 준비 중 이동을 따라가며 발사 후 방향은 고정한다.
- 포효 준비·회복, 기마 명령의 비대칭 팔 자세, 발구르기 상체 반동, 질주 중 고개 안정화, 투창 준비/발사 연결을 보강했다. SpearThrow 마지막 프레임과 SpearRelease 첫 프레임의 뼈 회전 차이는 0도다.

### 기둥·눈보라·투창 연출 보강

- 기둥은 타격마다 실제 체력이 1씩 줄어 기존 무기 타격 성공/히트스톱을 전달한다. `CombatDamageGate.FilterDamage`는 이 구조물에서만 1회 피해로 정규화한다. DOTween으로 짧은 흔들림과 `_CrackProgress` 진행을 만들고, 전용 `Fracturing Ice` 셰이더가 1타보다 2타에 더 많은 균열을 보여 준다. 3타에는 독립된 `FrozenIceBurst` 파편 이펙트를 생성한다.
- 새 기둥 위치는 기존 기둥과의 거리, 벽/구조물 충돌, 플레이어와 보스의 위치를 검사한다. 기본 간격 3.2m이며, 빈 자리를 찾지 못하면 겹쳐 생성하지 않는다.
- 소환 원판 대신 실제 검 주위를 감싸 올라가는 냉기와 작은 결정 조각을 표시한다.
- 한기 패턴은 최소 7초의 수집 준비 시간을 제공한다. 준비 중 모은 보호 시간은 소비하지 않고 눈보라가 시작된 뒤부터 줄어든다. 피해는 눈보라 발동 이후 기존 Tick 간격으로 처리한다.
- 결정 획득 시 작은 얼음 조각이 플레이어 쪽으로 모인다. 공통 `SeasonGameplay/PlayerHealthUI/FrostProtection`에 배치한 아이콘·시간·잔여 막대를 `FrostProtectionHUD`가 갱신한다. 일반 피격 무적과 결정 보호 시간은 구분한다.
- 투창 예고는 균열이 없는 직선/화살표이며, 준비 및 던지기 직전까지 플레이어를 따라간다. 발사 순간 방향을 고정한다. 손에 연결된 위치에서 2.6초 동안 창을 만들고 기본 속도 28로 던진다. 창 촉과 자루 메시, 생성 고리, 비행 냉기 꼬리를 추가했다.
- `Roar`, `FrostChannel`, `FrostSpin`, `MountedCommand`, `MountedStompReady/Impact`, `MountedCharge`, `SpearThrow/Release` 클립을 다듬었다. 팔 포즈는 길이를 보존한 키프레임으로 제작하며 실행 중 강제 IK로 휘두르지 않는다. 1타·2타 및 정면 내려찍기 원본은 기준 동작으로 유지한다.
- `StartPolish()` 실행 검사: 실제 무기 피해 경로의 타격 성공, DOTween 균열 누적, 파괴 파편, 반복 소환 간 충돌 없음, 보호 UI와 7초 준비/보호 시간 보존, 눈보라 Tick, 새 모션 종료 후 복귀, 이동 표적 방향 표시, 손에서 발사한 빠른 창 3회 유도 성공을 통과했다. 별도 실패 경로에서도 즉사와 정리를 확인했다. UI 및 모션/파편 스크린샷을 확인했으며 검증은 MonsterTest에서 수행했다.

이 절이 현재 동작 기준이다. 아래 이전 작업 기록에는 제거된 패턴과 과거 검증 결과가 포함되어 있다.

### 1페이즈

- 기본 1·2타와 내려찍기/균열은 기존 애니메이션을 유지한다.
- 빙결 폭발은 새 `Roar` 동작으로 예고한다.
- **얼음검의 저주**: 기존 50% 안전지대＋군마 왕복 공격을 유지하고 표시 이름만 변경했다. 내부 Enum `SwordFury`는 저장 호환을 위해 유지한다.
- 얼음 기둥: 기존 `Plant` 동작 후 기본 6개를 소환한다. `CombatHealth`와 `FrozenKnightConstruct`를 통해 공격력 크기에 관계없이 기본 3번의 유효 타격으로 파괴한다. 기둥은 이동 장애물이며 수명이 끝나면 제거된다.
- 확장 원형 검기: `FrostSpin`으로 한 바퀴 회전하며 고리를 밖으로 보낸다. 기존 `PlayerMotor`의 앉기/점프 상태로 회피를 판정한다.
- HP 70%·30% 한기: 각 1회, 현재 공격 종료 후 실행한다. 구형 결정이 보스로 이동하며, 준비 후 15초 동안 전장 지속 피해를 준다. 획득당 3초 보호가 누적되고 전용 HUD로 표시된다. 보스에 도착한 결정은 보호를 주지 않고 사라진다.

### 2페이즈

- 기존 직선 돌진·발구르기·고정 방향 얼음검은 유지한다. 빙결 기마 돌격은 선택과 수동 시작에서 제외했다.
- 얼음 기둥은 하마하지 않고 이미 꽂힌 검에서 소환 연출을 시작한다.
- 좌우 기마 분신은 모델의 현재 자세를 복제한 시각 효과다. 실제 피해는 넓어진 별도 돌진 판정으로 처리하며 한 공격의 중복 피해를 막는다.
- 겨울 잡몹은 기존 SnowWolf / IceGolem / FrostMammoth 프리팹을 사용한다. 소환한 생존 몬스터는 최대 3마리다.
- 원격 대검은 플레이어 쪽으로 이동해 2회 휘두른 뒤 저장된 월드 위치/회전으로 돌아와 꽂힌다.
- HP 0에서 즉시 사망하지 않고 마지막 결정 기믹을 시작한다. 중앙 구형 결정은 플레이어 공격에 면역이고 보스의 창 3회 충돌로 파괴된다. 보스는 가장자리에서 총 4개의 창을 준비해 발사한다. 발사 후에는 추적하지 않는다. 실패하면 전장 폭발이 플레이어를 즉사시키고, 성공하면 플레이어 피해 없이 보스 사망을 완료한다. 사망 이벤트는 마지막에 한 번만 전달한다.

### 추가 파일과 연결

- 실행 코드: `FrozenKnightBoss.Encounters.cs`, `FrozenKnightConstruct.cs`, `FrozenKnightSpear.cs`.
- 기존 확장: `FrozenKnightBoss`, `CombatHealth`, `FrozenKnightVfx.Patterns`, `FrozenKnightAfterimage`.
- 에셋 제작 도구: `FrozenKnightEncounterAuthoring`. 씬 오브젝트를 생성하지 않고 클립과 메시만 제작한다.
- 프리팹: `Assets/Prefabs/Effects/FrozenKnightPillar`, `FrozenKnightPickup`, `FrozenKnightCore`, `FrozenKnightSpear`.
- 기둥: BoxCollider＋CombatHealth＋FrozenKnightConstruct＋NavMeshObstacle. 결정: 별도 SphereCollider＋CombatHealth＋FrozenKnightConstruct. 창: 모델과 분리한 이동 구간 SphereCast 판정.
- 클립/Animator State: `Roar`, `FrostSpin`, `SpearThrow`. 기존 컨트롤러에 연결했으며 새 Parameter는 없다.
- 메인 FrozenKnight 프리팹에 위 프리팹 4개와 겨울 잡몹 3개 참조를 연결했다. 추가 수동 연결은 필요하지 않다.
- Inspector의 신규 패턴 항목에서 HP 임계값, 기둥 수/타격 횟수/수명, 검기 속도/폭/높이, 한기 시간/Tick, 결정 수/보호 시간, 분신 간격, 소환 상한, 원격검 시간, 창 수/속도/준비 시간/결정 파괴 횟수를 조절한다.
- 전장 중심은 연결된 `arenaCenter`, 방 입출구 중앙, 초기 보스 위치 순으로 정하고 NavMesh 바닥 높이에 맞춘다. 별도 보스방은 `arenaCenter`를 직접 연결하고 `arenaRadius`를 방 크기에 맞추면 된다.
- 검증 도구: Play Mode에서 `FrozenKnightVerification.StartEncounters()`는 새 패턴과 성공 경로, `StartEncounters(true)`는 실패 즉사 경로를 검사한다. 검사 중 바꾸는 체력/위치는 씬에 저장하지 않는다.
- MonsterTest 실행 검증: 기둥 3타 파괴, 원형 검기의 서기 피격/앉기·점프 회피, 결정 보호 3초 누적, 소환 생존 상한 3, 원격 대검 실제 피격/제자리 복귀, 창 3회 유도 성공 및 4회 실패 즉사, 최종 사망 1회와 소환물 정리를 통과했다. 70%·50%·30% 순차 발동과 각 한기 패턴 1회 제한도 검사했다. 현재 컴파일/실행 콘솔 오류는 없다. 실제 겨울방 구조물과의 배치 간섭 및 난이도 조정은 별도 플레이 점검 대상이다.

## 이전 작업 기록

## 꽂힌 대검 피격 반동 / 얼어붙은 기사 패턴 디자인

- 검의 월드 위치를 고정한 뒤 CombatFeedback(LateUpdate 순서 15000)이 상체 뼈에 반동을 적용해, 손 뼈의 자식이던 검도 흔들렸다. 칼을 꽂을 때 SwordGrip을 보스 루트로 옮겨 뼈 계층에서 분리하고 월드 자세를 유지한다. 회수/중단/비활성화 시 원래 손 부모·로컬 자세·배율로 복원한다. 기사의 피격 반동은 유지한다.
- 색상으로 패턴을 구분하지 않는다. 보스 갑옷과 대검에 맞는 푸른 얼음·은백색을 공유하며, 형상과 움직임으로 구분한다.
- 베기: 칼날을 따라가는 초승달 궤적. 균열: 전방으로 가지를 뻗는 검 자국과 순차적인 얼음 솟음. 빙결 폭발: 검 주위 빙판이 갈라지며 넓은 갑옷형 얼음 조각이 들리고 파편과 충격파가 퍼진다.
- 폭주: 안전 경계 바깥에 왕관형 얼음 성벽과 얇은 냉기 장막. 군마 발구르기: 한 쌍의 발굽 문양, 낮고 넓은 파편, 방사형 지면 균열. 질주: 발굽 자국과 진행 방향으로 흐르는 냉기; 특수 질주에는 양옆에 칼날형 얼음 조각을 추가한다.
- 추적 얼음검: 칼 주변에 분절된 은백색 소환 고리와 비행 꼬리. 착지 흔적은 작은 눈 결정으로 유지한다. 모든 VFX는 기존 DamageArea와 독립이며 기존 반경·폭·안전지대와 외곽 표시를 공유한다.
- `FrozenKnightVfx.Patterns.cs`에 패턴별 표현을 분리했다. 기존 VFX 메시 버퍼를 재사용하며 장식마다 GameObject를 생성하지 않는다.
- MonsterTest에서 2페이즈 보스에 CombatFeedback을 거치는 실제 피해 3회(HP 450→447)를 적용했다. 꽂힌 검의 위치 오차 최대 0.000000492m, 회전 오차 0도였다. 폭발·폭주·발구르기·돌진·추적검의 실행 화면과 전환 후 복귀를 확인했다. 상시 빙판 균열은 공격 경고와 겹쳐 보이지 않게 강도를 낮췄다.

## 추적 얼음검 / 꽂힌 얼음검 / 군마 동작 / 돌진 잔상 (최신)

- 기마 얼음검은 발사 후에도 플레이어를 향해 최대 초당 210도로 회전한다. 속도 14, 최대 비행 거리 35m와 기존 피해/3초 슬로우(배율 0.6, 대시 제외)를 유지한다.
- 얼음검은 손에서 나오지 않고 보스 등 뒤 위쪽의 IceBladeSpawnRight/Left에서 오른쪽 → 왼쪽 순서로 번갈아 생성된다. 두 발사점은 보스 프리팹 Hierarchy에 배치하고 Inspector 참조를 연결했다. 위치를 직접 옮겨 조절할 수 있다.
- 피격·지면/벽 충돌·비행 수명 종료 시 별도 결정을 만들지 않고 날아온 검 자체를 지면에 꽂는다. 공중에서 피격한 경우에는 피해 판정을 끄고 0.22초 동안 지면으로 내려와 꽂힌다. 칼끝은 지면 아래 0.2m, 기본 유지 4.5초, 마지막 0.65초 동안 서서히 사라진다. 꽂힌 검은 추가 피해나 이동 차단을 만들지 않는다.
- FrozenIceBlade에서 ImpactIce/ImpactCrystal1~3과 물리 콜라이더를 제거했다. 기존 IceSwordVisual을 계속 사용하며, BladeTip 기준점을 추가하고 Inspector에 연결했다. 착지 주변에는 작은 서리 흔적만 남긴다.
- 이번 변경의 MonsterTest 검증: 세 발의 로컬 생성 위치는 `(1.7, 4.8, -1.4)`, `(-1.7, 4.8, -1.4)`, `(1.7, 4.8, -1.4)`였다. 착지 시 칼끝은 지면 아래 0.198m, DamageArea는 비활성, 동일 검 모델은 활성 상태였다. 각 발 1회 피해로 HP 100→97, 유지 시간 종료 후 남은 투사체 0개를 확인했다.
- 1페이즈 군마 준비 이동은 안전 반경 + 몸통 반폭 + 여유 0.6m를 피해 NavMesh와 장애물을 검사한 우회 경로로 이동한다. 경로를 못 찾거나 돌진을 끝까지 통과할 수 없으면 해당 돌진을 건너뛴다. 실제 공격 돌진만 중앙을 통과한다.
- 위험지역과 검의 빙판에 얼음 균열, 냉기 띠, 눈 결정 문양을 보강했다. 위험지역의 입체 결정은 안전구역 바깥 경계에 배치한다.
- Mount/Gallop 클립을 기존 리그에서 다시 편집했다. 탑승은 왼발 디딤 → 몸 올림 → 오른다리 넘김 → 착석 순서이며, 과도한 이동 포물선을 0.25m로 줄였다. 질주는 좌우/앞뒤 다리의 박자와 무릎 접힘, 목·꼬리의 반동을 다듬었다.
- Plant에서 바로 페이즈 전환되는 경우에는 이전 Plant 상태의 끝 자세를 잘못 고정하지 않도록 블렌드 완료 후 검 위치를 고정한다. 해당 중단 전환 테스트에서 칼날 방향 `(0, -1, 0)`을 확인했다.
- FrozenKnightAfterimage를 보스 프리팹에 연결했다. SwordFury의 군마 단독 돌진, 기마 Charge/MultiCharge/MountedFury의 Active 중에만 생성한다. 기마 상태는 말과 기사 모두 표시한다. 준비 이동에는 새 잔상을 만들지 않는다.
- 잔상은 0.14초 간격, 0.36초 수명, 최대 3개 자세를 사용한다. 메시 버퍼를 재사용하고 그림자/콜라이더를 사용하지 않는다. BakeMesh 결과에 모델 확대 배율을 이중 적용하지 않도록 보정했다. Inspector에서 간격·수명·개수·색을 조절할 수 있다.
- 검증: MonsterTest 전체 페이즈/패턴/피격 중복/Tick/전환/사망 검사 PASSED. 군마 준비 이동의 중심 최소 거리 8.8897m, 실제 돌진 6회. 단독/탑승 잔상 크기는 실행 화면으로 확인했다. 발사 후 플레이어 위치 변경 시 검의 회전, 피격 슬로우, 임시 얼음 성장/충돌/시간 후 제거를 확인했다.

## 배치 및 전환

- `Assets/Prefabs/monster/FrozenKnight.prefab`: 전투용 완성 프리팹.
- `Assets/Prefabs/monster/IceHorse.prefab`: 19본 스킨 리깅 군마, Animator, MountSeat 포함.
- 실제 겨울 마지막 방은 `Winter_Room04.unity`이며, Stage4_Winter의 11번째 방이다. 해당 방의 RoomMonsterSpawner.bossPrefab에 연결했다.
- 기존 PlayerMotor, CombatHealth, CombatFeedback, NavMesh, 방 클리어/스트리밍을 사용한다.
- 기본 HP: 1페이즈 350, 2페이즈 450. 각 페이즈 HP 50% 특수 공격은 독립적으로 한 번씩 실행한다.
- 1페이즈부터 군마는 약 12m 떨어진 이동 가능한 지점에서 대기한다. 보스가 움직여도 대기 위치를 유지한다.
- 1페이즈 HP 0 → 무적/공격 중단 → 무릎 꿇기 → 군마 왼편으로 일어나기 → 대기하던 군마가 NavMesh 경로로 접근 → 감속/정지 → 왼발 디딤·몸 올리기·오른다리 넘기기 → 탑승 완료 → 2페이즈 HP 설정 → 무적 해제.
- 검 원본의 손잡이는 로컬 Y 약 0.85이다. RightHand/SwordGrip 아래에서 실제 손잡이를 손에 맞췄다. 검 크기를 조절해도 손잡이 중심은 손에서 벗어나지 않는다.

## 1. 생성한 Script

- `CombatPhaseTransition`: 사망 이벤트 전에 페이즈 전환을 요청하는 기존 HP 시스템의 확장 지점.
- `FrozenKnightBoss`: 10개 패턴, 추적/고정 방향 이동, HP 특수 패턴 대기, 군마 접근/탑승, 쿨타임과 중복 패턴 제한.
- `FrozenKnightDamageArea`: 별도 Trigger의 활성 구간, 이동/회전 스윕, 공격당 중복 타격 방지, 지속 피해 Tick.
- `FrozenKnightHazard`: 직선 검기와 개별 수명을 가진 얼음길.
- `FrozenKnightVfx`: 서리 범위 표시, 균열/가시, 대검 잔상 및 강화 효과. Damage 판정은 수행하지 않는다.
- Editor 전용 `FrozenKnightAnimationAuthoring`: 제공 동작의 분할/편집, 탑승 동작, 군마 클립, 이펙트 메시 에셋 제작.
- Editor 전용 `FrozenKnightVerification`: 이미 배치된 보스/플레이어를 대상으로 Play Mode 검증. 씬 구성이나 프리팹 설정을 생성하지 않는다.

## 2. 수정한 기존 Script

- `CombatHealth`: 1페이즈 HP 0에서 Died와 비활성화를 보류하고, 전환 중에도 방의 생존 적으로 집계한다. 전환 기능이 없는 적은 기존 사망 처리를 유지한다.
- `RoomMonsterSpawner`: 생성한 겨울 보스에 ForestRoom을 전달한다.
- `StageOneDirector`: 플레이어 사망/런 종료 시 겨울 보스의 공격·이동·남은 위험지역을 중단한다.
- `PlayerKarma`: 겨울 보스 처치를 보스 처치로 분류한다.

## 3. Frozen Knight GameObject / Component

루트: CombatHealth, NavMeshAgent, Kinematic Rigidbody, CapsuleCollider, FrozenKnightBoss, FrozenKnightVfx, SeasonEnemyHealthBar.

자식: Knight(Animator), IceHorse(군마 프리팹), ChargeDamageArea, CrackDamageArea, RadialDamageArea.

오른손: RightHand → SwordGrip → Sword → SwordDamageArea / SwordBase / SwordTip.

## 4. Ice Horse GameObject / Component

Animator, IceHorseRig(19본), IceHorseBody(SkinnedMeshRenderer), 등 본 아래 MountSeat.

별도의 몬스터 AI/HP를 만들지 않았다. 군마의 대기·접근·질주는 FrozenKnightBoss가 제어한다.

## 5. Trigger / DamageArea

| 용도 | 판정 |
|---|---|
| 대검 | 칼날에 연결한 Box Trigger, 각 타격의 Active 구간만 활성 |
| 돌진 | 보스 중심 Box Trigger, 돌진 중만 활성 |
| 얼음 균열 | 전방 긴 Box Trigger, 검 타격과 별도 피해 |
| 빙결 폭발/발구르기 | Sphere Trigger |
| 검기 | 독립 프리팹의 Box Trigger, 적중/장애물/최대 거리에서 제거 |
| 얼음길 | 직선 질주 한 번당 하나의 Box Trigger를 지나간 길이만큼 확장, 개별 Duration/Tick |
| 단계별 얼음 가시 | 안쪽/바깥쪽 반경으로 annulus(고리) 계산, 끝난 안쪽 지역은 안전 |

모델 MeshCollider와 VFX 메시로 피해를 판정하지 않는다.

## 6. Inspector Reference

Animator 2개, RiderRoot/HorseRoot/SwordRoot/MountSeat, 4개 DamageArea, 검기/얼음길 프리팹, VFX와 검 시작·끝점은 연결 완료.

`Horse Arrival Point`와 `Special Entry`는 선택 항목이다. 미지정 시 이동 가능한 위치를 자동으로 찾는다. 특정 연출 위치를 원하면 씬의 Transform을 직접 지정하면 된다.

HP, 각 공격 Damage/Range/Width/Warning/Duration/Recovery/Cooldown/Knockback, 이동/검기 속도, 돌진 거리·횟수, 그로기, 특수 HP, 고리와 얼음길 설정을 FrozenKnightBoss Inspector에서 조절한다. 기본 플레이어 피해는 기존 게임 규칙에 따라 유효 타격당 하트 1칸이다. `Use Full Player Damage`를 켜면 각 패턴의 Damage 수치를 그대로 적용한다.

## 7. Animator Parameter / State

Parameter는 사용하지 않는다. 스크립트가 상태를 CrossFade하고 설정된 시간에 맞게 재생 속도를 조절한다. Root Motion은 꺼져 있다.

- 기사: Idle, Walk, SwordReady, Attack1/2/3, SpinReady, SlamReady, Slam, BladeReady, BladeRelease, Plant, Kneel, StandUp, Mount, MountedIdle, MountedReady, MountedCharge, MountedSlashReady, MountedSlash, MountedImpact, MountedGroggy.
- 군마: Idle, Gallop, Rear, Stomp, Groggy, Summon. 현재 접근 방식에서는 Summon 클립을 직접 사용하지 않고 멀리서 Gallop으로 달려온다.

## 8. Animation

`Assets/Art/FrozenKnight/Animations`에 기사 22개, 군마 6개 클립과 컨트롤러 2개를 제작했다. 제공한 Idle/Walk/1타2타/3타/내려찍기 동작을 기반으로 분리하고, 부족한 준비·무릎·기립·탑승·기마 동작을 편집했다.

군마 Blender 원본: `ArtSource/FrozenKnight/IceHorse_Rigged.blend`. 재제작 스크립트: `Tools/build_frozen_horse.py`. 기존 저장소 정책대로 `.blend` 원본은 로컬 보관이고 Git에서 제외된다. Unity 사용 FBX/클립은 Assets에 있다.

## 9. VFX / Prefab

- `FrozenIceBlade.prefab`, `FrozenIceTrail.prefab`.
- FrozenFrost 셰이더/Frost 재질, Blender 제작 FrostCrystal 메시, 범위 테두리와 서리 문양, 얼음 가시, 검 잔상/강화 효과.
- 군마는 원본 USDZ의 알베도/노멀 텍스처를 추출해 Standard 호환 재질로 연결했다.
- 필수 Animation/VFX/Prefab은 모두 생성했다. 별도의 외부 효과 에셋 설치는 필요 없다.

## 10. Unity Editor 사용 순서

1. `Stage4_Winter` 실행 → 11번째 전투방에서 보스 확인. 기존 방 NavMesh를 사용하므로 추가 베이크는 필요 없다.
2. 별도 실험은 MonsterTest에서 원하는 기존 몬스터를 비활성화하고 FrozenKnight 프리팹을 바닥에 배치한다.
3. 프리팹의 FrozenKnightBoss Inspector에서 밸런스를 조절한다. 손잡이 정렬은 SwordGrip을 기준으로 유지한다.
4. 특정 군마 대기/특수 공격 출발점을 원할 때만 선택 Reference에 씬 Transform을 지정한다.

## 확인한 항목

- MonsterTest에서 10개 공격 흐름, 중복 피격 방지, Tick 간격, 방향 고정, 특수 패턴 재실행 제한, 페이즈 HP/무적/최종 사망 검증 통과.
- 실제 겨울 마지막 방에서 스폰, 기존 NavMesh, 전환 중 생존 적 수 1 유지, 최종 처치 후 0 변경 확인.
- 일반 공격 중 HP 50%를 넘긴 경우 두 페이즈 모두 현재 공격을 끝내고 특수 공격을 실행하는 것 확인.
- 손잡이 기준점과 오른손의 위치 오차는 0.01m 미만으로 확인.
- 확인 과정에서 Unity Animator 편집기 내부 `UnityEditor.Graphs.Edge.WakeUp` 예외가 관찰됐다. 창을 닫아도 리로드 때 재발해 편집기 쪽 추가 확인이 필요하다. 보스 런타임 코드에서 발생한 예외는 없었다.

실제 전투 난이도와 플레이어 시점에서의 최종 수치는 플레이테스트를 통해 추가 조절할 수 있다.

## 공격 자세 보정 (2026-09-29)

- 원본 공격의 골반 회전 때문에 내려찍기 중 몸통이 정면 기준 약 179도 돌아가던 것을 확인했다. 편집 클립에서 골반의 추가 좌우 회전을 제거하고 허리·목의 비틀림과 어깨 관절 회전을 제한했다.
- 주요 공격 6개(Attack1/2/3, Slam, MountedSlash, MountedImpact)를 각각 31개 시점에서 샘플링했을 때, 어깨와 머리 위치로 계산한 몸통 방향의 정면 이탈은 최대 약 15.5도였다. AI 이동 방향과 타격 범위는 기존 구조를 유지한다.
- `FrozenKnight_StableArmor.asset`은 원본 메시를 복사해 망토 뒤쪽과 몸통 중심의 스킨 웨이트를 골반·척추에 분배한 보정본이다. 원본 모델/텍스처는 보존하며, 프리팹의 Knight/mesh_0에 보정본을 연결했다.
- 기마 클립의 다리는 원본 공격의 다리 회전 위에 탑승 각도를 더하지 않고, Idle 기준으로 탑승 자세를 만든다.
- MonsterTest 실행 중 공격 포즈의 정면·후면과 실제 패턴 재생 화면을 확인했다. 검증 도구의 캡처는 별도 검토 카메라가 없으면 기존 Main Camera를 잠시 사용하고 원래 위치로 되돌린다.
- 보정 후 10개 패턴, 중복 피격/Tick, 군마 접근 후 탑승, 2페이즈 HP/무적 해제, 특수 패턴 1회 제한, 최종 사망 검증을 다시 실행해 PASSED를 확인했다. 검증 후 Play Mode를 종료했다.

## 원본 비탑승 공격 적용 (후속 변경)

- 사용자가 Animator에 다시 넣어 확인한 `VARCO3DImports/WinterBoss/1타2타`, `3타`, `내려찍기`를 비탑승 공격에 적용했다. 위의 관절 회전 제한은 이 네 공격과 해당 준비 클립에는 적용하지 않는다.
- Attack1/Attack2는 1타2타 원본의 5~47% / 47~94%, Attack3는 3타의 16~83%, Slam은 내려찍기의 30~83% 구간이다. 준비 클립은 각 첫 공격 구간 직전까지를 사용한다. 재생 시간은 기존 Inspector의 공격 지속 시간에 맞춘다.
- 2타 직전에는 1타 종료 자세를 유지하여 첫 공격 준비 자세로 되돌아가지 않는다. 얼음 균열의 기존 별도 DamageArea와 균열 효과는 유지한다.
- `FrozenKnightAnimationAuthoring.BuildFootAttacks()`로 이 일곱 클립만 재생성할 수 있다. 기마 공격과 군마 애니메이션은 이번 변경 대상이 아니다.

## 정면 칼날 궤적과 효과 개선 (최신)

- `FrozenKnightForwardAuthoring`이 원본을 분리한 클립에 정면 궤적 보정을 굽는다. 골반·허리 회전을 제한하고, 두 관절 IK로 오른팔을 움직인 뒤 실제 Sword의 칼날 축을 기준으로 손목 방향을 맞춘다. 런타임 IK 연산이나 별도 오브젝트 생성은 없다.
- 1타는 오른쪽→왼쪽, 2타는 왼쪽→오른쪽, 3타는 더 넓은 정면 베기로 이어진다. 내려찍기는 위에서 정면 바닥으로 내려온다. 칼끝 위치를 프레임별로 샘플링해 앞쪽에 있는 것을 확인했다.
- 공격 준비 끝 자세와 공격 시작 궤적을 맞추고, 왼팔의 과도한 들림을 제한했다. `Attack Blend Time` / `Recovery Blend Time`으로 공격과 대기 복귀 블렌드 시간을 별도로 조절할 수 있다.
- 3타 경고는 기존 뒤쪽까지 덮던 300도에서 정면 170도로 변경했다. 잔상은 실제 검 DamageArea가 활성일 때만 생성한다.
- 경고에 가지형 눈꽃 문양, Active 구간에 상승하는 입체 얼음 입자, 칼날 잔상에 청색→백색 그라데이션과 얇은 밝은 테두리를 추가했다. 결정 셰이더에는 면별 명암과 가장자리 광택을 추가했다. 입자는 같은 동적 메시로 묶어 렌더링한다.

### 양손 대검 자세 재수정

오른팔만 칼끝에 맞추던 보정은 양손 손잡이 기준으로 교체했다. 손잡이 이동은 양팔이 닿는 반경으로 제한하고 왼손 목표는 오른손에서 손잡이 아래 방향으로 0.075 모델 단위 떨어진 위치다. 팔꿈치 굽힘 평면까지 정렬하여 상완 축의 비틀림을 방지하며, 손목 회전은 프레임별 원본 회전에 덧붙이지 않고 일정한 칼날 좌표계에서 계산한다. 준비 동작에서는 양팔 모두 함께 손잡이로 이동한다.

MonsterTest에서 3연격과 내려찍기를 연속 재생한 검증: 타격 활성 518 프레임에서 왼손 목표 오차 최대 0.00356m, 칼끝의 보스 정면 거리 최소 0.719m. 베기 중간 자세와 양손 머리 위 준비 자세를 화면으로 확인했다. 검증 후 Play Mode를 종료했다.

## 원본 전체 클립 / 원래 재생 속도 (현재 적용)

사용자 요청에 따라 비탑승 공격은 보정/분리 클립 대신 Animator에 직접 연결된 원본 `1타2타`, `3타`, `내려찍기` State를 재생한다. 1타2타는 3.7667초짜리 한 클립, 3타는 5.0667초, 내려찍기는 4.7667초이며 모두 Animator/State 배속 1이다. 앞의 팔 IK/궤적 보정은 이 전투 경로에 적용되지 않는다.

- 1타2타가 끝난 뒤 3타 전체를 재생한다. 1타와 2타 사이에 재시작/정지하지 않는다.
- `Original Attack Blend Time`은 원본 공격 진입/연결, `Recovery Blend Time`은 대기 복귀 전환 시간이다. 후딜레이 길이가 Idle 재생 속도를 변경하지 않는다.
- 판정은 `First/Second/Final/Slam Hit Window`의 0~1 재생 위치 구간에서만 켠다. 기존 공격의 Warning/Duration 및 Combo Speed로 이 세 클립의 속도를 늘이거나 줄이지 않는다.
- 균열은 `Slam Impact Time`에 Crack Delay를 더한 시점에 별도 판정을 활성화한다. 클립의 후반 복귀 동작도 끝까지 재생한다.
- `BuildFootAttacks()`에서 강제 팔 궤적 보정 호출을 제거하여 다시 빌드해도 원본을 덮어쓰지 않는다. 기존 개선 VFX는 유지한다.

### 크기 / 내려찍기 방향 후속 변경

- FrozenKnight 프리팹 루트 Scale은 1 → 1.5. SwordGrip은 1 → 1.333333으로 설정해 상위 확대를 포함한 검의 최종 크기가 기존의 정확히 2배가 되도록 했다. 손잡이 기준 확대이므로 오른손에서 분리되지 않는다.
- `SlamForward.anim` / `내려찍기_정면` State는 원본 내려찍기 복사본이며, 루트 회전만 Y축 약 +72.85도로 보정했다. 팔 관절/위치 곡선, 길이 4.7667초와 1배속은 유지했다.
- 40% 타격 시점의 칼끝은 보스 좌표 기준 `(0.03, -0.12, 4.06)`으로 정면 중앙에 위치한다. 1타2타/3타는 기존 원본 연결 그대로다.

## 2연격 / 표시와 판정 일치 (최신)

- 일반 대검 패턴에서 3타 재생을 제거했다. 원본 1타2타(3.7667초, 1배속) 후 `combo.second.recovery`만큼 후딜레이를 가진다.
- 확대된 검의 두 타격 구간을 각각 61개 시점에서 측정한 수평 최대 도달 거리는 약 7.25m / 6.51m다. 프리팹의 1타/2타 범위를 7.3m / 6.6m로 설정했다. 표시만 키우면서 검 자체를 다시 확대하지 않는다.
- 비탑승 베기는 기존 radialArea에 `ArmSector`를 적용한다. VFX와 판정이 동일한 반경 및 `Melee Sector Angle` 150도를 공유하며, 한 타격마다 동일한 피격 집합으로 중복 피해를 막는다. 칼날이 높이 지나가도 표시 안의 지상 플레이어가 빠지지 않는다.
- 발구르기/빙결 폭발도 수평 360도 판정을 사용하고, 고리 특수 패턴은 플레이어 몸체가 안쪽/바깥쪽 경계에 걸치는 경우를 포함한다. 표시는 경고이고 피해는 Active 시간에만 발생한다. 기존 피격/대시 무적은 유지한다.
- Box DamageArea는 부모 확대 배율을 보정하여 균열/돌진의 표시와 실제 월드 크기가 일치한다. 구조물로 64개 검색 버퍼가 가득 찬 경우에는 전체 검색으로 누락을 막는다.
- 실행 검증: 1타 반경 안 6.8m 및 경계에 몸체가 걸친 7.35m에서 피격, 8.5m/뒤쪽/부채꼴 밖에서는 무피해. 반복 접촉 5회에도 동일 타격 피해는 1회. 보스 Scale 1.5에서 균열의 실제 Box 크기는 `(1.8, 3, 14)m`로 확인했다.
- 변경 후 두 페이즈 10개 패턴의 전체 흐름, 타격 중복 방지/Tick, 특수 패턴 1회 제한, 군마 접근/탑승, HP 전환/사망 검증은 PASSED. 재생 종료 후 남긴 변경은 프리팹/스크립트/문서이며 테스트용 플레이어 위치·HP는 저장하지 않았다.

## 빙결 폭발 / HP 폭주 강화 / 검기 제거 (최신)

- 얼음 검기는 AI 선택, Begin 진입, 실행 코루틴에서 제거했다. 기존 직렬화 호환을 위해 Enum 값 2는 예약으로 남는다.
- 빙결 폭발 미발동 원인은 근접 기준 5m가 접근 정지 거리 5.5m보다 짧고, 일반 공격 종료마다 체류 시간이 초기화되던 조건이었다. 현재는 폭발 반경 7.5m에서 체류 시간을 누적하고 2초 조건/쿨타임을 만족하면 우선 선택한다. 현재 공격은 끝낸 후 선택하며 연속 같은 패턴 방지는 유지한다.
- 빙결 폭발: 검 꽂기/손 놓기 준비 2초(애니메이션 블렌드로 늦어진 종료까지 대기), 활성 0.45초, 쿨타임 9초.
- 얼음 검의 폭주: 5개 고리, 첫 외곽 반경 5m, 고리 폭 4m, 간격 3.5m → 마지막 외곽 반경 19m. 검 꽂기/손 놓기 준비 2초, 각 고리 경고 0.55초, 활성 0.18초, 고리 사이 대기 0.06초, 종료 후 검 회수 1.4초 및 회복 3초(강화 배율 적용).
- 폭주 전용으로 가시 밀도 2배, 높이 2배, 폭 1.45배의 이중 결정 열과 확장하는 빛의 파동을 적용했다. 파동은 현재 위험 고리 안에서만 표현하고, 지난 고리의 내부 안전 지대는 비워 둔다.
- MonsterTest 실행 검증: 5.4m 거리에서 일반 AI가 빙결 폭발을 선택했고, 폭발 중 HP 50% 이하로 낮추자 현재 공격 종료 후 폭주 5개 고리가 약 0.92초 간격으로 발동했다. 특수 패턴 재실행 및 삭제한 얼음 검기 실행은 거부됐다. 테스트용 플레이어 위치/무적 변경은 Play 종료로 되돌렸다.
- `Plant.anim`은 내려찍기를 재사용하지 않고 `FrozenKnightPlantAuthoring`에서 새로 제작했다. Idle 시작 자세에서 양손을 손잡이로 모아 검을 세우고 아래로 누른 뒤, 손을 놓고 양팔을 내리는 2초 비반복 클립이다. 팔 길이를 보존한 양손 키포즈를 애니메이션 곡선으로 굽고 실시간 팔 IK는 사용하지 않는다.
- Plant 56% 지점에서 SwordGrip의 월드 위치/회전을 고정한다. 이후 손이 떨어져도 검은 지면에 수직으로 남는다. 기존 검 오브젝트를 사용하며 복제나 추가 공격 콜라이더는 없다. `PlantRetrieve`는 손잡이를 다시 잡아 검을 회수하는 1.4초 클립으로, 40%에서 손 연결을 복구한다. 사망/비활성화/페이즈 전환 시에도 고정을 해제한다.
- `PlantSword`는 Animator 전환 지연까지 기다린 다음 판정을 허용한다. 폭발/각 파동에서 꽂기 동작을 재시작하지 않는다. MonsterTest에서 손 놓은 동안 위치 변화 약 0.000001m, 칼끝 방향이 수직 아래인 것을 확인했고 회수 후 원래 손 로컬 위치로 복구됐다.

## 군마 협공 / 바닥의 검 / 기마 얼음검 (현재 적용)

이 절의 내용이 위의 기존 5개 고리 폭주와 기마 측면 베기를 대체한다.

- **1페이즈 HP 폭주:** 보스는 검을 꽂고 그 자리에 서 있다. 2.5초의 모이기 경고 후 반경 7m 바깥이 지속 위험지역이 된다(외곽 45m, 피해 1, 1초 Tick). 군마가 별도로 반대편 왕복 후 옆 방향으로 이동하며 총 6회 직선 돌진한다. 각 돌진은 1초 경고, 폭 2.6m, 속도 17, 피해 1이다. 경고를 시작하면 방향을 고정하고, 경로 변경은 다음 돌진 준비 때만 한다. 이동 경로는 기존 NavMesh를 사용하고 벽 앞에서 멈춘다. 보스 주변은 바깥 냉기 피해만 안전하며 군마 돌진은 피해야 한다.
- **탑승 전환:** 전용 Plant 동작으로 검을 꽂고 손을 놓은 다음 기존 군마 접근/정지/탑승 연출을 실행한다. 검은 2페이즈 내내 월드 위치에 남는다. 기마 패턴의 Recover가 검을 다시 가져오지 않으며, 탑승 위치 갱신 후 검 위치를 고정한다.
- **검의 빙판:** 검 위치 중심 반경 12m, 조향/제동 가속도 14. 기존 WinterIcePatch 및 PlayerMotor의 표면 처리를 재사용한다. 최고 이동속도를 바꾸지 않고 방향 변경과 제동에 약한 관성을 준다. 대시는 기존대로 빙판 처리를 건너뛴다. 보스 사망/비활성화 시 표면 효과를 해제한다.
- **기마 측면 베기:** 선택/실행 코루틴에서 제거했다. 기존 Enum 값 6은 저장 호환용 예약으로 남으며 Begin은 거부한다. 1페이즈에서 제거했던 검기(값 2)도 계속 비활성이다.
- **기마 얼음검 발사:** Pattern `MountedIceBlades`(값 10). 등 뒤 좌우 위쪽에서 얼음검을 생성한 뒤, 각 검의 발사 순간 플레이어 위치를 한 번 조준하고 그 방향으로 직진한다. 발사 후 플레이어를 추적하지 않는다. 기본 3발, 각 경고 0.85초, 발사 간 활성 0.2초, 속도 14, 최대거리 35m, 피해 1, 쿨타임 8초. 착지한 검 자체가 4.5초간 꽂혀 있다가 사라진다. VFX 모델에는 피해 콜라이더가 없고 별도 DamageArea가 판정한다.
- **얼음검 슬로우:** 피해가 실제로 들어갔을 때만 3초간 일반 이동속도 배율 0.6을 적용한다. 대시 속도/지속시간/쿨타임에는 영향을 주지 않는다. 중복 피격 시 배율을 곱하지 않고 지속시간을 갱신한다. 리스폰/플레이어 비활성화 시 해제한다.
- **프리팹 연결:** FrozenKnight에 HorseChargeWarning(VFX), PlantedSwordIceField(WinterIcePatch + VFX)를 Hierarchy에서 추가하고 Inspector 참조를 연결했다. 기존 ChargeDamageArea를 군마 협공 중에도 재사용한다. FrozenIceBlade에 IceSwordVisual(원본 대검 모델)을 추가하고 냉기 재질 및 projectileVisual 참조를 연결했다.
- **코드:** 기존 FrozenKnightBoss를 partial로 나눠 `FrozenKnightBoss.Cavalry.cs`에 추가 패턴을 정리했다. 기존 Hazard/DamageArea/VFX, WinterIcePatch, PlayerMotor를 확장했다. 새로운 Player/HP/피격 시스템은 만들지 않았다.
- **검증:** MonsterTest 전체 두 페이즈 패턴/전환/사망 검증 PASSED. 제거한 측면 베기 실행 거부, 기마의 모든 패턴에서 검 위치 유지, 사망 시 빙판 해제를 확인했다. 실물 얼음검 피격 시 HP 97→96 및 배율 0.6 적용. FixedUpdate 후 측정한 일반 이동 2.4, 대시 12, 만료 후 일반 이동 4. 위험지역은 안쪽 HP 100 유지, 바깥 HP 99, 같은 Tick 재호출 시 99 유지.

## 1·2타 궤적 및 탑승 자세 보정

- 원본 `1타2타`의 속도는 유지한다. 실제 앞쪽 휘두르기에 맞춰 첫 판정 구간을 0.24~0.335, 두 번째를 0.52~0.585로 맞췄다. 타격별 판정 초기화는 유지한다.
- 임의 회전하던 범위 장식을 실제 SwordTip 이동을 투영한 궤적으로 교체했다. 두 타격의 잔상을 분리하고, 2타의 얼음날 잔상을 더 선명하게 했다. 바닥 경고 경계는 실제 판정 범위를 유지한다.
- `FrozenKnightPoseRefinement`는 기존 리그에 클립을 굽는 편집 도구다. Kneel 2.2초 / StandUp 1.9초에 발 고정, 한쪽 무릎 접지, 상체의 무게 이동, 검을 놓은 손 자세를 적용했다. 기마 클립과 Mount의 발목은 말의 앞쪽을 향하도록 보정했다. 런타임 관절 강제 보정은 없다.
- 전환 이펙트의 높은 기둥 대신 낮은 냉기 문양과 작은 얼음 조각을 사용해 전환 동작이 보이도록 했다.
- MonsterTest 실행에서 1·2타 Active 구간과 1→2페 전환 완료를 확인했다. 기마 발끝 방향은 양쪽 모두 전방이며 정면/측면 렌더를 확인했다. 기존 UnityEditor.Graphs.Edge.WakeUp 예외 외 새 컴파일/플레이 오류는 관찰되지 않았다.
- **군마 일반 이동:** `FrozenHorseWalkAuthoring`으로 `Horse_Walk.anim`을 제작하고 IceHorse Controller의 `Walk`에 연결했다. 네 발의 접지/회수 순서를 분리하며, 접지 구간에서 발굽이 일정 속도로 뒤로 이동하도록 제작했다. 일반 추적은 Walk, 공격 돌진은 기존 Gallop을 사용한다. 실제 NavMesh 속도를 `horseWalkReferenceSpeed`(3.2)로 나눠 재생 속도를 맞추며, 최소/최대 배율도 Inspector에서 조절한다.

## 기마 패턴 발동 / 구분 개선

- 발구르기 체류 판정은 별도 closeRange 5가 아닌 실제 stomp.range 6을 사용한다. 기존 추적 정지 거리 5.5에서도 조건이 충족되며, 체류 시간과 쿨타임을 만족하면 우선 선택한다.
- 연속 질주는 일반 기마 공격 2회 이후 쿨타임이 준비되면 우선 선택한다. 시작 전 1.3초 동안 연속 화살표 문장을 보여주고, 각 돌진 경로 입구에 남은 횟수를 표시한다. 마지막에는 기존 긴 그로기를 유지한다.
- 빙결 기마 돌격은 이동 전에 2.4초간 다섯 얼음날 왕관과 바닥 서리 문장을 보여준다. 이 예고에는 피해가 없다. 각 돌진 준비 경로에도 작은 얼음날을 배치해 일반 돌진과 구분한다.
- Inspector: multiChargeIntroduction, mountedFuryIntroduction, chargeEmblemRadius, attacksBeforeMultiCharge. 기존 VFX 메시와 재질을 재사용한다.

## 연속 질주 제거 / 범위 타이밍 채우기 (현재)

- 일반 연속 질주(MultiCharge)는 선택과 Begin 실행에서 제거했다. Enum 번호만 저장 호환을 위해 유지한다. 일반 돌진 및 HP 특수 빙결 기마 돌격은 유지한다.
- 전체 공격 범위는 고정 표시하고 그 위로 밝은 얼음색이 채워진다. 원형은 안쪽→바깥, 직선은 출발→끝, 부채꼴은 각 타격의 휘두르는 방향으로 진행한다. 고리 범위의 안쪽 안전지대는 채우지 않는다.
- 베기/균열은 Animator의 실제 재생 위치를 사용한다. 시간 기반 준비는 기존 Stage의 경과 시간과 히트 정지를 함께 사용한다. Activate 순간 진행률을 1로 고정한다. 1타와 2타는 각각 새 경고를 시작한다.
- 범위 없는 왕관 시작 예고, 상시 빙판, 꽂힌 검의 장식에는 공격 범위 채우기를 적용하지 않는다.

## 왕좌 입장 / 군마 탑승 연출 (2026-09-30)

- Winter_Room04의 FrozenThrone_01에 FrozenKnightCinematicStage를 추가하고 ThroneSeatedOrigin / ThroneStandingOrigin을 Hierarchy에서 배치했다. RoomMonsterSpawner의 bossCinematicStage 참조도 연결했다.
- 입장: 왕좌 대기 → SitUp 기반 ThroneRise → 제공 FBX를 리타겟한 ThroneDraw1/2 → 왕좌 앞으로 이동 → NavMesh로 중앙 이동 → 전투 시작. 준비 중에는 보스가 무적이고 공격 판정이 비활성이다.
- SitUp의 발이 떠오르는 루트 이동은 Blender에서 접지 기준으로 보정했다. 소스는 ArtSource/FrozenKnight/WinterKnight_Cinematics.blend 및 author_cinematics.py에 보관한다.
- 탑승: 기존 검 꽂기 / 군마 접근을 유지하고 군마 정지 후 CinematicMount를 2.8초로 재생한다. 기존 탑승 모션을 사용했으며 새로운 고품질 모션 캡처 소스로 교체한 것은 아니다.
- FrozenKnightCinematicStage가 기존 Camera와 PlayerMotor/PlayerWeapons를 잠시 제어한다. 종료 및 비활성화 시 조작/무적을 복구하고 PlayerCamera가 원래 시점으로 보간 복귀한다. 별도 영상이나 Timeline 패키지는 사용하지 않았다.
- FrozenKnight 프리팹의 Throne Idle/Rise/Draw1/Draw2/Cinematic Mount 참조와 Animator 상태를 연결했다. 이 방 참조가 없는 MonsterTest에서는 왕좌 입장을 실행하지 않는다.
- 실제 Winter_Room04 단독 실행을 두 번 수행해 입장 완료, 2페이즈 탑승 완료, 플레이어 조작 복구를 확인했다. 마지막 실행 콘솔 오류 0건. 주요 자세 캡처는 Temp/FrozenKnight/Cinematics에 있다. 임시 플레이어/카메라는 삭제하고 씬을 저장했다.
- 이번 범위에는 사망 연출과 사망 FBX의 기마 변환은 포함하지 않았다. 상용 게임과 동급의 연기/접촉 품질을 보장하는 단계는 아니며 원본 탑승 모션의 한계가 남아 있다.

### 탑승 전환 수정

- 검을 꽂은 뒤 Kneel / StandUp을 재생하던 구간을 제거했다. 기사는 서서 기다리고 옆 탑승 지점까지 걸어간다.
- 군마는 꽂힌 검의 실제 Renderer 범위에 horseSwordClearance를 더한 영역을 우회한다. 기존 군마 경로 검사 함수를 재사용하며 horseBoardingDistance로 옆 정지 거리를 조절한다.
- 정지 후 기존 탑승 모션을 재생하고 보스의 NavMesh 이동 기준을 군마 위치로 옮긴다. 기수/군마/꽂힌 검의 월드 위치는 보존한다.
- 수정 후 Winter_Room04 실행 검증: 전환 중 Kneel/StandUp 재생 없음, 군마와 검의 Renderer Bounds 겹침 없음, 탑승 완료 및 조작 복구 확인, 콘솔 오류 0건. 검 회피 간격은 군마의 머리/꼬리를 포함한 실제 수평 반경으로 계산한다.

## 최후의 일격 사망 연출 (2026-09-30)

- 제공된 Two Handed Sword Death.fbx를 Blender에서 MountedIdle 하체와 결합해 MountedDeath.anim(4.8초)을 제작했다. 군마는 서서 멈추고 기사의 상체가 뒤로 무너지는 방식이다. 말에서 떨어지는 연출은 아니다.
- Blender 원본: ArtSource/FrozenKnight/WinterKnight_Death.blend. 재제작 스크립트: author_mounted_death.py. 원본의 골반 낙하를 그대로 적용하지 않고 탑승 위치와 다리 자세를 유지했다.
- 발악 공략 후 마지막 유효 타격에서 기존 CombatHealth 사망 보류 구조를 재사용한다. 사망 모션 → deathPoseHold(1.2초) → CompleteDeferredDeath(true) 순서로 실행해 방이 먼저 클리어되지 않게 한다. CombatHealth를 새로 대체하지 않았다.
- FrozenKnight 프리팹의 mountedDeath / deathIceEffect를 Inspector로 연결했고 Animator에 MountedDeath 상태를 추가했다. 기존 FrozenIceBurst의 작은 파편 효과를 재사용한다.
- FrozenKnightCinematicStage에 사망 구도를 추가했다. 연출 중 군마 애니메이션/공격/이동을 멈추며 플레이어를 보호하고 조작을 잠근다. 종료 후 기존 시점과 조작으로 복귀한다.
- 실제 Winter_Room04에서 입장/탑승/발악의 검 파괴/마지막 타격을 연결 검증했다(검 파괴는 검증 도구에서 ReceiveSpear를 3회 호출). 마지막 타격 직후 사망 보류 확인, 연출 종료 후 Died 이벤트 1회, 플레이어 조작 복구, 콘솔 오류 0건. Temp/FrozenKnight/Cinematics/MountedDeath0.1/0.5/0.9.png에 자세 캡처를 보관했다.

### 사망 자세 보존 / 빙판 확산 연출

- 말에서 옆으로 떨어지는 이동은 제거했다. MountedDeath 마지막 자세에서 기사와 군마 애니메이션을 멈추고 모델을 남긴다. 충돌과 이동은 종료하며, 기존 Died 이벤트로 방 클리어를 처리한다.
- CombatHealth.CompleteDeferredDeath의 keepBody 선택 인수는 기본 false이므로 다른 사망 처리는 유지한다.
- 검을 꽂은 직후 3.2초 동안 빙판이 퍼지고 0.7초 보여준 뒤 군마 접근으로 연결한다. 빙판 확산과 미끄러지는 표면의 범위는 같은 Growth를 사용한다.
- PlantedSwordIceField에 누락되었던 WinterAttackVfx / WinterFrost / patch 참조를 Inspector에서 연결했다. 확산 경계에는 밝은 서리 띠를 추가했다.
- 최종 Winter_Room04 실행 확인: 빙판 확산 캡처 확인, 사망 후 모델 활성 상태 유지, 기사/군마 Animator 속도 0, 활성 Collider 0, Died 1회, 조작 복구, 콘솔 오류 0건. 검 파괴는 검증 도구로 모의 타격했다.

### 입장 보행과 발도 보정

- 왕좌에서 일어나 중앙까지 맨손 보행 후 발도한다. Unarmed Walk Forward.fbx를 보스 골격에 리타겟하여 UnarmedWalk 상태에 연결했다. 대검을 뽑은 후에는 기존 Walk를 사용한다.
- Walk / UnarmedWalk의 마지막 구간을 첫 프레임의 자세 및 접선 속도로 연결했다. 두 클립 모두 반복 경계 값/속도 차이 0 확인. 수정 전 원본은 ArtSource/FrozenKnight/*_BeforeLoop.anim에 보관한다.
- 실제 입장 실행에서 UnarmedWalk 3주기 및 중앙 도착을 확인했다.
- 발도 1 후반과 발도 2 초반에 손목 방향을 바깥으로 보정해 칼날이 몸통을 가로지르던 구간을 줄였다. 수정한 자세 캡처를 확인했고 콘솔 오류 0건.

### 얼음벽 지속 / 군마 누적 가속

- 얼음벽은 시간 만료를 무시하며 1→2페이즈 전환에서도 보존한다. 최후 발악 시작 및 사망/방 해제 시 기존 정리 경로로 제거한다.
- 2페이즈 돌진 이동 구간의 상자 범위에 닿은 벽은 파편 효과와 함께 즉시 파괴한다. 일반 고정 장애물 뒤까지 파괴하지 않도록 거리를 제한한다.
- 1페이즈 50% 협공의 실제 수행 돌진마다 chargeSpeedMultiplier(기본 1.2)를 곱한다. 시작 속도 17, 6회면 마지막 약 42.3. 위치 재배치 속도와 예고 시간은 유지한다.
- 런타임 기능 검증: 만료 시간이 지난 벽 및 페이즈 정리 후 생존 확인, 1페이즈 즉시 파괴 거부, 2페이즈 돌진 구간의 벽 사망/Collider 비활성 확인. 전체 전투 수동 회피 테스트는 수행하지 않았다.
