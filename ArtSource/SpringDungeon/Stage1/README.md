# Stage1 Spring — 방 구조 제작

- 씬: Assets/Scenes/Stage1_Spring.unity
- 에셋: Assets/MapDesigne/Spring (Models, Textures, Materials, Prefabs)
- 원본: Assets/VARCO3DImports/Spring_Tile_Designe.usdz (보존)
- Blender 작업: ArtSource/SpringDungeon/Optimization/Spring_Optimized.blend

## 방 구성

| 방 | 크기 (Unity 단위) | 벽 높이 | 시작 X |
|---|---|---|---|
| 꽃숲 | 38.4 × 36 | 약 9.46 | 0 |
| 만개한 꽃밭 (보스) | 46.08 × 43.2 | 약 9.92 | 44.4 |

Stage4_Winter의 최신 일반/보스 방 기준과 6단위 연결 간격을 따릅니다. Hierarchy의 Spring_Rooms 아래 Floor, Walls, Layout_Markers로 정리했습니다. 장식/게임플레이 구성은 추가하지 않았습니다. 타일 원본에 포함된 꽃·돌·뿌리는 유지합니다.

## 최적화

원본 10,776 → 10,376 삼각형 (약 3.71% 감소). 원본 UV, 베이스 컬러와 노멀 텍스처를 유지하며 벽 2개, 바닥 2개로 분리했습니다. 형상 변화가 큰 모듈은 폴리곤 수를 유지했습니다. 상세 수치는 Optimization/Optimization_Report.json을 참조하세요. 표면 오차는 양방향 정점-표면 샘플 기준이며 전체 표면 오차의 수학적 보증은 아닙니다.

공유 재질과 재사용 프리팹을 적용했으며 각 메시에는 MeshCollider와 Ground 레이어를 연결했습니다. Unity 오브젝트 생성과 Inspector 설정은 MCP의 기본 Hierarchy/컴포넌트 도구로 수행했습니다.

## 확인

- 방 내부 바닥 4,144지점: 빈틈 0
- 방 연결 이동 경로 1,134지점: 빈틈 0
- 중앙 이동 경로를 막는 벽: 0
- 누락 메시/재질: 0
- 렌더러 94개, 배치된 메시 합계 246,781삼각형
- 확인 시 Unity 콘솔 오류 0건
- 플레이 이동/보스/장식은 이번 작업 범위에 포함하지 않았습니다.

미리보기: Stage1_Final_Overview.png

