# 겨울 몬스터

사용자 제공 ZIP의 모델/색상 텍스처를 유지하고 Blender 4.3.2에서 리깅 및 행동 애니메이션을 제작했습니다. 매머드는 스키닝 전에 정점을 180도 회전하여 머리/상아가 게임 전방을 향합니다. 모델은 각각 30,000 삼각형이며 OBJ 중복 정점을 용접한 뒤 heat skinning을 적용했습니다. UV는 유지합니다.

- 원본 및 Blender 작업본: 이 폴더, `WinterMonsters.blend`
- 재현 스크립트: `rig_and_animate.py` (연결된 Blender의 WinterMonsters_Authoring 장면에서 사용)
- 모델/텍스처/재질/애니메이션: `Assets/Art/WinterMonsters`
- 프리팹: `Assets/Prefabs/monster/IceGolem`, `SnowSpirit`, `SnowWolf`, `FrostMammoth`
- 기존 겨울 방 13개의 RoomMonsterSpawner에 연결. 기존 3마리/동일종 3마리 금지 규칙 유지.

## 패턴과 조절

`WinterMonsterEnemy` Inspector의 private SerializeField 값으로 공격 거리, 예고, 공격 시간, 후딜, 쿨타임을 조절합니다.

- 골렘: 1.15초 예고, 양팔 들어올리기/내려찍기, 18m/55도 전방 균열과 순차 가시. 36m/s로 전파되며 도달한 영역에 1회 피해와 아주 약한 넉백.
- 눈 정령: 0.95초 예고, 두 손 내려놓기, 플레이어 방향 장판. 반경 5.4m, 6초. 장판 위에서만 제동/방향 전환 가속도가 3.5가 되어 미끄러지며 최고 이동속도는 감소하지 않음. 점프/대시/넉백은 우선 처리.
- 늑대: 0.8초 준비, 0.58초 덮치기, 0.6초 앞발 준비/재조준, 0.65초 전방 할퀴기, 1.7초 후딜. 착지 반경 2.6m, 첫 타는 약한 띄우기. 두 번째 타격은 4.6m/110도 앞발 부채꼴과 약한 넉백. 착지 지점은 각 점프 전 확정되며 벽/NavMesh 경계로 이동 범위 제한.
- 매머드: 0.95초 고개 낮춤/서리, 1.7m 전진, 5.6m/110도 상아 쓸어올리기. 적중 시 수평 속도 11, 상승 속도 7, 0.75초 공중 넉백.

피해는 기존 CombatHealth/CombatFeedback으로 처리합니다. VFX 메시 자체에는 피해 판정이 없습니다. 장판은 소유자 사망/비활성화, 수명 종료, 방 해제, 스테이지 종료 시 정리됩니다. StageEnemy.cs는 수정하지 않았습니다.

## 검증

Editor 전용 `WinterMonsterValidationRunner`는 빌드에서 제외됩니다. 임시 NavMesh 장면에서 피해 예고, 전방/후방 판정, 늑대 재조준과 2회 피해/후딜, 매머드 넉백, 얼음 관성/이탈/공중/만료 등 28항목을 검증했습니다. 기록과 캡처는 `Builds/VfxValidation/WinterValidation.txt`, `Winter_*.png`에 있습니다.


## 2배 크기 보정

골렘/늑대/매머드 프리팹과 MonsterTest 배치의 XYZ Scale은 (2,2,2)입니다. 공격 거리 필드는 Scale 1 기준이며 실제 판정과 경고는 SizeScale을 공통 적용합니다. 골렘 전파 속도도 같은 비율로 증가합니다. 눈 정령은 기존 크기와 장판 반경을 유지합니다.

Blender에서 발 접지용 2관절 풀이를 애니메이션으로 베이크했습니다. 골렘 교대 보행, 늑대 대각 보행, 매머드 4박 보행을 사용하며 WalkSpeed는 실제 NavMesh 이동 속도와 보폭을 연결합니다. 장애물 앞에서 정지하면 Walk를 멈춥니다. 공격 준비 및 지지하는 발도 접지를 보정하고, 골렘 타격 시점은 공격 28%에 맞췄습니다.

쿨타임 이동: 후딜(Recovery) 후에는 공격 사거리와 별개로 플레이어에게 접근하고, 가까워지면 주변을 돌며 재배치합니다. WinterMonsterEnemy의 '쿨타임 중 이동'에서 Reposition Distance(Scale 1 기준), Reposition Speed Multiplier를 조절합니다. 공격 준비/타격/회복 중 정지와 기존 cooldown은 유지합니다.
