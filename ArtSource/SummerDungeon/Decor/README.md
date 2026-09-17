# Stage3 Summer 구조물 배치

## 결과
- 씬: Assets/Scenes/Stage3_Summer.unity
- 에셋: Assets/MapDesigne/Summer/{Models,Materials,Textures,Prefabs}/Decor
- Blender 작업본: Summer_Decor_Optimized.blend
- 원본 USDZ 보존. 첫 번째 파일의 실제 이름은 Summer_FirsRoom_Designe.usdz.
- 기존 방 크기, 바닥, 벽, 연결 통로 유지. Unity Hierarchy와 Inspector 설정은 native MCP로 직접 적용.
- 32개 모듈 분리, 143개 구조물 배치.

## 방 구성
| 방 | 컨셉 | 배치 수 |
|---|---|---:|
| Room_01_CoastalCave | 해안 바위와 물웅덩이, 폭포, 관목 | 37 |
| Room_02_TropicalForest | 야자수 군집, 뿌리, 덩굴과 큰 잎 식물 | 42 |
| Room_03_Waterfall | 암벽 아치, 폭포와 물길 | 30 |
| Room_04_CoastalCliff_Boss | 대형 해안 동굴, 절벽과 가장자리 물웅덩이 | 34 |

폭포 방에는 FirstRoom의 WaterfallCliff 모듈 2개를 함께 사용했습니다.
중앙 전투 공간을 확보하고 가장자리에 큰 구조물을 배치했습니다.
물과 폭포는 정적인 메시/텍스처이며 흐름 애니메이션은 추가하지 않았습니다.

## 최적화
원본 4개 합계 120,000 → 113,648 삼각형 (5.293% 감소).
UV와 텍스처를 유지하고, 모듈 최대 길이 대비 샘플 표면 오차 0.6% 이하인 감축만 채택했습니다.
샘플 검사는 전체 표면의 수학적 오차 보장이 아닙니다. 잎과 덩굴 등 감축 시 형태 손실이 큰 부분은 유지했습니다.
USDZ별 원본 이미지를 별도 추출하여 서로 다른 방의 재질이 공유 이미지 이름으로 덮이지 않게 했습니다.

## 검사
- 활성 씬 삼각형 합계: 645,977 (렌더러별 메시 인스턴스 합산)
- 장식 부분: 475,084 / 기존 구조: 170,893
- 1,500,000 예산 대비 잔여: 854,023
- 메시/머티리얼·기본 텍스처/콜라이더 누락: 0
- 방 바닥 7,024점 및 연결 통로 1,680점 샘플: 빈틈 0
- 중앙 통로 높이 1, 2 및 z -2~2 검사: 장식 장애물 0
- 최종 Unity Console 오류 0, 씬 저장 완료 확인.
- 실제 플레이, NavMesh 경로 탐색 및 FPS 성능은 이번 검사에 포함하지 않았습니다.

Optimization_Report.json에 개별 모듈 수치를, Validation_Report.json에 씬 검사 결과를 보관했습니다.
Placement_Manifest.json의 groundAdjustments는 초기 배치 위치에 적용된 최종 접지 높이입니다.
원본/최적화 비교 이미지와 방별 스크린샷도 이 폴더에 보관했습니다.

