# Spring 뿌리 문

## 결과
- 씬: Assets/Scenes/Stage1_Spring.unity
- 원본: Assets/VARCO3DImports/Spring_Door_Designe.usdz (보존)
- 프리팹: Assets/MapDesigne/Spring/Door/Prefabs/SpringRootDoor.prefab
- 애니메이션 FBX: Assets/MapDesigne/Spring/Door/Models/SpringRootDoor.fbx
- 머티리얼: Assets/MapDesigne/Spring/Door/Materials/SpringRootDoor.mat
- Blender: SpringRootDoor.blend

## 디자인
양옆 문틀은 고정하고 중앙 뿌리를 좌우로 분리했습니다.
중앙 뿌리의 Open 변형 키가 시간차를 두고 양옆 기둥 방향으로 말려 들어갑니다.
30fps, 1~97 프레임, 3.2초. 원본 UV/텍스처 유지.
닫힘 0%, 중간 약 38%, 열림 100% 키가 Unity에 정상 수입되었습니다.
변형 때문에 기둥과 뿌리의 중앙 경계가 분리되지만 닫힘 실루엣은 원본과 일치합니다.

## 배치
Hierarchy: Spring_Rooms > Spring_RootDoors
- Room01_Entrance_RootDoor: x=0
- Room01_Exit_RootDoor: x=38.4
- Room02_Entrance_RootDoor: x=44.4
- Room02_Exit_RootDoor: x=90.48

기존 입구 벽 Entry_3(1번 방), 출구 벽 Exit_3/Exit_4(보스방)는 문과 겹쳐 비활성화했습니다.
기존 벽과 원본 에셋을 삭제하지 않았습니다.
미리보기 제작용 임시 오브젝트는 제거했습니다.

## 방 클리어 이벤트 연결
사용자 선택에 따라 접근 시 자동 개방이나 전투 진행 로직은 추가하지 않았습니다.
각 문 루트의 기존 DogLife.VineGate 컴포넌트에서:
- Open(): 열기
- Close(): 역방향으로 닫기
- Duration: 개방 시간, 기본 3.2초
- Start Open: 기본 꺼짐
- Animation Root / Visual Root / Opening Clip / Blockers: Inspector 연결 완료

UnityEvent를 사용하는 방 클리어 이벤트에 문 루트를 넣고 VineGate > Open()을 선택하면 됩니다.
기존 StageOneDirector를 사용할 경우 해당 문 루트를 Gates에 연결할 수 있습니다.
PassageBlocker는 완전히 열린 뒤 비활성화되고 닫기 시작하면 즉시 활성화됩니다.
움직이는 뿌리에는 별도 고정 MeshCollider를 달지 않아 열린 상태의 통로를 막지 않습니다.
양쪽 고정 문틀에는 MeshCollider가 연결돼 있습니다.

## 검증과 보관
Unity에서 열린/닫힌 변형 키, 머티리얼, FBX 클립, 각 프리팹의 로컬 참조와 닫힘 충돌을 확인했습니다.
방 사이 출입구 2개는 개방 포즈에서 중앙 검사선에 장애물이 없었습니다.
외곽 입구/출구 고정 높이 검사에서는 기존 바닥 메시가 일부 검출돼 실제 캐릭터 이동 테스트는 별도로 필요합니다.
방 클리어 런타임 이벤트 통합 테스트는 아직 연결 전이라 수행하지 않았습니다.
최종 씬은 닫힌 상태로 저장했고 Console 오류 0을 확인했습니다.
당시 열려 있던 Stage4_Winter의 미저장 상태는 저장해 보존한 뒤 닫고 Spring 씬만 열어 두었습니다.
백업: Stage1_Before_RootDoors.unity.backup


## 보스룸 출구 크기 수정 (2026-09-17)
- Room02_Exit_RootDoor/AnimatedRoots 크기 (12,12,5.5) → (18,14,5.5).
- PassageBlocker 크기 (9.6,8.6334,2), 중심 높이 4.3167로 조정.
- 양옆 보스방 벽과 문틀의 틈을 없애고 높이를 맞췄습니다. 다른 문/프리팹은 변경하지 않았습니다.
- 확인 이미지: BossExit_Resized.png.
- 실제 재생 Open() 완료 Progress=1, blocker 비활성, 열린 중앙 통로 15개 광선 검사에서 충돌 0. Console 오류 0.
