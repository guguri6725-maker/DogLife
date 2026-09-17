# Fall 내려가는 문
## 보스룸 출구 크기 보정
Room02_Exit_FallDoor만 DoorScale=(17,11.5,10), 루트 y=-0.65로 조정했습니다. 충돌 크기=(16.7,9.9,1.8), 중심 y=4.95. 더 넓고 높은 보스룸 출구의 울퉁불퉁한 벽 경계에 겹치게 배치했습니다. 나머지 문과 공용 프리팹 크기는 유지합니다. 아래 공통 크기 설명보다 이 개별 설정이 우선합니다.
- 적용 씬: Assets/Scenes/Stage3_Fall.unity
- Hierarchy: Fall_Rooms/Fall_SinkingDoors, 2개 방 입구/출구 총 4개.
- 프리팹: Assets/MapDesigne/Fall/Door/Prefabs/FallSinkingDoor.prefab
- 원본 VARCO3DImports/Fall_Door_Designe.usdz 유지.

## 디자인/최적화
정면 무늬와 UV를 유지하며 깊이를 원본의 18%로 축소했습니다.
수정 후 30,000 → 16,500 삼각형(45% 감소).
Blender 장면 및 모델은 FallSinkingDoor.blend에 보관합니다.
build_fall_sinking_door.py는 이 .blend의 Fall_Sinking_Door 씬에서 다시 실행할 수 있는 작업 기록입니다.
최적화 표면 거리 95백분위 0.001764, 최대 0.012654(정규화 모델 좌표, 두께 수정 후와 비교).

## 애니메이션과 연결
Fall_Sinking_Door: 2.4초. 문 전체가 아래로 내려가 지면 아래로 사라집니다.
VineGate.Open()을 방 클리어 UnityEvent에 연결하면 됩니다.
자동 근접 개방/전투 시스템은 추가하지 않았습니다.
Duration으로 속도를 조정할 수 있고 Close()로 역재생 복구할 수 있습니다.
현재 Start Open=false, 통로 BoxCollider 활성 상태로 저장했습니다.
완전히 열린 후 통로 충돌이 해제됩니다.

## Inspector 구조
문 루트(VineGate) / DoorScale(12,10,10) / AnimatedDoor(FBX) / SinkingPanel.
FBX 자체의 축/단위 변환은 유지해야 합니다. 크기 수정은 DoorScale에서 하세요.
PassageBlocker는 루트 자식이며 크기 (11.8,8.6,1.8)입니다.
문 좌표 x=0,38.4,44.4,90.48 / y=-0.45 / z=0 / Y 회전 90도.
원래 첫 입구 Entry_3 및 마지막 출구 Exit_3/4는 문과 겹치므로 비활성화하여 보존했습니다.

## 검증
실제 Unity 재생 모드에서 4개 Open() 호출 후 IsOpen=true, 통로 충돌 해제 확인.
문 최상단이 y=-2.362까지 내려가 완전히 지면 아래에 들어갑니다.
통로 광선 검사: 문/벽 충돌 없음. 1번 방 출구의 기존 울퉁불퉁한 바닥에는 일부 광선이 닿았습니다.
기존 지형은 변경하지 않았으며 플레이어 이동 테스트는 수행하지 않았습니다.
컴포넌트/재질 연결 정상, 콘솔 오류 0. 검사 후 닫힌 상태로 복원하여 저장.
이미지: Thin_Closed.png, Unity_Thin_Door.png, Fall_Door_Closed_Verified.png, Fall_Door_Placed_Open.png.
