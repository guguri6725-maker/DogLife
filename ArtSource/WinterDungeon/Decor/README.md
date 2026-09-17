# Stage4 Winter — 구조물 배치

## 저장 위치

- 씬: Assets/Scenes/Stage4_Winter.unity
- 구조물 FBX: Assets/MapDesigne/Winter/Models/Decor
- 구조물 프리팹: Assets/MapDesigne/Winter/Prefabs/Decor
- 재질: Assets/MapDesigne/Winter/Materials/Decor
- 텍스처: Assets/MapDesigne/Winter/Textures/Decor
- Blender 작업 원본: Winter_Decor_Optimized.blend
- 배치 전 씬 백업: ../Stage4/Stage4_Before_Decor.unity.backup

## 구성

| 방 | 배치 | 구조물 수 |
|---|---|---:|
| 눈숲 | 눈 덮인 소나무와 고목 군집, 주변 바위와 낮은 관목 | 53 |
| 얼어붙은 동굴 | 주변 얼음 기둥·석순, 벽 상단 고드름, 빙판 | 38 |
| 설산 협곡 | 높이가 다른 절벽 군집, 경사 암석과 고목 | 22 |
| 얼음 보스룸 | 뒤편 얼음 제단, 결정과 기둥, 중앙 전투 공간 | 34 |

기존 방 크기/바닥/벽/연결 통로는 유지했습니다. 각 방의 Decor 그룹에 개별 프리팹이 있습니다. 부모 방의 비균일 확대를 Decor 그룹에서 상쇄하여 구조물의 비율을 보존했습니다. 원본 USDZ 4개는 Assets/VARCO3DImports에 유지하며 기존 씬의 원본 인스턴스는 Winter_Source_References에 비활성 상태로 보관했습니다.

## 최적화 및 외형

4개 원본 합계 120,000 → 79,072 삼각형 (34.11% 감소), 총 31종으로 분리.
원본 UV와 BaseColor/Normal 이미지를 유지했습니다. USDZ 내부에 같은 이름의 재질/이미지가 있어 각 원본 압축에서 별도로 추출하여 연결했습니다. 가는 나뭇가지/관목은 감소율을 낮추거나 원본 밀도를 유지했습니다. 양방향 정점-표면 샘플 최대 오차는 각 모듈 최대 길이의 0.6% 이내이며 전체 표면의 수학적 보증은 아닙니다. 전후 렌더는 각 방 폴더에 있습니다.

## 검증

- 활성 맵 전체: 617,933 삼각형 (기존 구조 142,603 + 장식 475,330)
- 150만 예산 대비 약 41.2% 사용, 잔여 882,067 삼각형
- Renderer 313개, 구조물 147개, 재사용 프리팹 31종
- 방 바닥 7,024개 샘플/중앙 연결 경로 1,680개 샘플에서 누락 0
- 각 방 중앙 폭 4단위의 경로에 높이 1/2단위 수평 레이를 검사해 구조물 간섭 0 (최종 외곽 벽 앞에서 종료)
- 누락 메시/재질/베이스 텍스처/콜라이더 0
- Unity 콘솔 오류 0

검사는 정적 배치와 충돌 샘플 검사입니다. 플레이어 이동, 실제 전투, NavMesh, 최소 사양 FPS 검증은 포함하지 않습니다. 기존 USD importer의 SerializeReference 경고는 별도 패키지 문제로 남아 있습니다.

Unity 오브젝트 생성, 배치, 재질/콜라이더 연결은 기본 Hierarchy/Inspector MCP 도구로 수행했으며 런타임 생성 스크립트를 추가하지 않았습니다.

