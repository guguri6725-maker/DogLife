# 계절 맵 방 전환 (2026-09-17)

## 실행
- Stage1_Spring / Stage3_Summer / Stage3_Fall / Stage4_Winter 중 원하는 메인 씬을 열고 Play.
- 메인 씬: 기존 3D 씬에서 가져온 플레이어, 카메라, HUD, 스킬 UI, 전투 및 스트리밍 관리자.
- 방 씬: Assets/Scenes/SeasonRooms/{Spring,Summer,Fall,Winter}. 봄 2 / 여름 4 / 가을 2 / 겨울 4방.
- 현재 Summer 메인 씬과 첫 방을 함께 열어 편집 미리보기를 준비했습니다. 메인 씬이 Active여야 합니다.

## 진행 규칙
1. 처음에는 1번 방만 로드하고 몬스터를 활성화합니다.
2. 해당 방 Encounter 아래 CombatHealth가 있는 몬스터를 모두 처치해야 클리어합니다.
3. 클리어 후 다음 방을 비동기 로드합니다. 로드가 완료된 뒤 현재 출구 문을 엽니다.
4. 다음 방의 입구는 열려 있고 몬스터는 비활성 상태입니다.
5. 플레이어가 다음 방 입구보다 3m 안쪽으로 들어가면 입구 문을 닫습니다.
6. 완전히 닫힌 뒤 새 방 전투를 시작하고 이전 방을 Unload합니다.
7. 전투 중에는 방 1개, 이동 준비 중에는 현재/다음 방 2개만 로드합니다.
8. 마지막 방 처치 후 출구 ExitPoint로 가면 클리어. 계절 간 자동 이동은 설정하지 않았습니다.

## 배치와 편집
- SeasonGameplay.prefab: 3D 씬의 플레이어/카메라/기존 UI와 기능을 복사한 구성.
- SeasonTestWood.prefab: 실제 Forest_Room_01의 Wood_1 인스턴스 설정(모델, 애니메이션, NavMeshAgent 포함)을 복사.
- 각 방 Encounter에 테스트용 Wood 몬스터 1마리. 보스룸도 현재는 같은 테스트 몬스터입니다.
- 오브젝트 생성/이동/배치/컴포넌트 연결은 Unity MCP Hierarchy/Inspector 작업으로 수행했습니다.
- 기존 문 애니메이션과 개방/닫힘 충돌 기능을 그대로 사용합니다.
- 여름 물과 통로, 다른 계절 연결 통로도 소속 방과 함께 해제됩니다.
- 각 방 NavMeshSurface는 Children / PhysicsColliders / Ground 레이어로 설정했습니다.
- Unity 기본 Bake 기능으로 경로 데이터를 저장했습니다. 실행 중 재계산이나 모델 Read/Write 활성화는 필요 없습니다.
- 지형을 편집한 후 방 루트 NavMeshSurface Inspector의 Bake를 다시 눌러 해당 방 씬을 저장하세요.
- 몬스터를 추가하려면 해당 방 Encounter 아래에 활성 상태로 배치하세요. Encounter 자체는 편집 상태에서 비활성이고 전투 시작 시 켜집니다.
- 3D 원본 씬과 Forest_Room_01은 추출 전 파일과 SHA256이 일치함을 확인했습니다.

## 검증
- 네 계절 모든 방: 살아 있는 적이 있을 때 출구 닫힘, 처치 후 다음 방 준비 및 문 개방 확인.
- 8개 방 전환: 진입 후 입구 닫힘, 이전 씬 해제, 새 방 적 1마리 활성화 확인.
- 12개 방에서 몬스터 NavMesh 연결 및 플레이어 접지 확인.
- 네 스테이지 모두 마지막 방 처치 후 출구에서 완료 상태 확인.
- 최종 Console 오류 0. 빌드용 경로 데이터 12개가 저장되어 있습니다.
- 실제 CombatHealth.Damage를 호출해 사망 흐름을 검사했습니다. 이동 경계 검사는 Rigidbody 위치 이동으로 수행했으며, 키보드로 모든 통로를 직접 걸어 보거나 독립 실행 빌드를 실행한 검증은 아닙니다.
- 상세 기록: Validation_Report.json. 변경 전 계절 씬 백업도 이 폴더에 보관했습니다.
