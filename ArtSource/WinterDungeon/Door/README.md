# Winter 얼음 문
## 결과
- 씬: Assets/Scenes/Stage4_Winter.unity
- Hierarchy: Winter_Rooms / Winter_IceDoors (각 방 Entrance / Exit 총 8개)
- 프리팹: Assets/MapDesigne/Winter/Door/Prefabs/WinterIceDoor.prefab
- 모델/머티리얼/텍스처: Assets/MapDesigne/Winter/Door
- 원본 Winter_Door_Designe.usdz는 수정하지 않았습니다.

## 최적화와 애니메이션
- 원본 30,000 → 최종 23,135 삼각형 (파편 단면 포함, 약 22.9% 절감).
- 원본 UV, 색상/노멀 텍스처 및 기둥 실루엣 유지. 최적화 전후 표면 거리 95백분위는 원본 폭의 약 0.063%, 최대 약 0.649%.
- 중앙 얼음 16조각이 순차적으로 떨어지고 바닥 아래에서 사라집니다.
- FBX 내 Winter_Ice_Door 클립: 2초, 30fps. 양쪽 기둥은 고정.
- 물리 파편 시뮬레이션 대신 미리 제작한 이동/회전 애니메이션을 사용합니다.
- 블렌더 원본 WinterIceDoor.blend와 비교/파괴 단계 이미지 보관.
- build_winter_ice_door.py는 보관된 .blend의 Winter_Ice_Door 씬과 WinterDoor_Source_Preserved를 대상으로 재생성하는 작업 기록입니다.

## 이후 방 클리어 연결
1. 문 루트의 기존 VineGate 컴포넌트를 선택합니다.
2. 방 클리어 UnityEvent에 해당 문을 드래그하여 VineGate.Open()을 연결합니다.
3. Duration은 현재 2초입니다. 문이 완전히 열린 후 PassageBlocker가 비활성화됩니다.
4. 현재 Start Open은 모두 꺼져 있습니다. 근접 자동 개방이나 전투 진행 로직은 추가하지 않았습니다.
5. Close()는 파괴를 역재생해 문을 복구합니다. 방 재설정 시에만 사용하세요.

## 배치
문 루트 세계 좌표 x: 0, 38.4, 44.4, 82.8, 88.8, 127.2, 133.2, 179.28.
y=-0.45, z=0, Y회전=90도. 모델 자식 크기는 (12,12,5.5).
원래 막힌 첫 입구 EntryWall_3, 마지막 출구 ExitWall_3/4는 새 문과 겹치므로 비활성화해 보존했습니다.
기존 방 구조와 나머지 벽/장식은 유지했습니다.

## 검증
- Unity 재생 모드에서 실제 Open() 호출: 8개 모두 Progress=1, 통로 충돌 해제, 파편 크기 0 확인.
- 각 문 통로 6개 광선 검사(높이 1/2, 좌우 -1/0/1): 열린 상태에서 장애물 충돌 0.
- 모든 문 내부 참조/클립/재질 정상, 각 문 고정 기둥 MeshCollider 2개 확인.
- 고정 기둥 애니메이션 트랙 변화 없음, Unity 콘솔 오류 0.
- 검사 후 재생 모드를 종료하고 모든 문을 닫힌 상태로 저장했습니다.
- 전투 시스템과의 방 클리어 이벤트 통합은 아직 하지 않았습니다.


## 보스룸 출구 크기 수정 (2026-09-17)
- Room04_Exit_IceDoor/AnimatedIce 크기 (12,12,5.5) → (18,13.5,5.5).
- PassageBlocker 크기 (10.5,8.325,2.8), 중심 높이 4.1625로 조정.
- 보스방 양옆 벽에 문틀이 연결되도록 폭과 높이를 맞췄습니다. 다른 문/프리팹은 변경하지 않았습니다.
- 실제 재생 Open() 완료 Progress=1, blocker 비활성, 열린 중앙 통로 15개 광선 검사에서 충돌 0.
- 확인 이미지: BossExit_Resized.png.
