# Stage 1 — 숲을 품은 탑 던전

## 원본과 분리

- `Assets/VARCO3DImports/Stage1Asset`의 USDZ 묶음 두 개에서 환경 소품을 분리했습니다.
- 형태가 여러 칸에 걸쳐 있던 거목과 수직 뿌리는 한 오브젝트로 묶었습니다.
- 두 번째 나무는 거목의 좌우 반전과 폭 조정 변형입니다. 원본 시트의 개별 디자인이 3D 생성 결과에 그대로 보존되지는 않았습니다.
- 분리 FBX 24개: `Assets/Art/TowerDungeon`
- 크기·원본·면 수 대응표: `models.json`
- Blender 편집 원본: `TowerDungeon.blend`

## 표현

깊이를 줄인 메시와 광원에 좌우되지 않는 텍스처 재질을 사용합니다. 원경은 짙은 청록색, 중경은 회녹색 석조, 전경은 어두운 식생으로 구성합니다. 밝은 발판 가장자리와 작은 등불이 이동 가능한 경로를 구분합니다.

## 씬 구조

`Stage01_Forest`의 바닥, 12개 상단 발판, 7개 전투 게이트와 전투 구간 위치를 기준으로 장식했습니다. 외형은 `TowerDungeon_Art` 아래 Architecture / Vegetation / Foreground / Walkway / Landmarks에 정리합니다. 장식 모델에는 충돌체를 추가하지 않습니다.

수정 전 씬 사본: `Stage01_before.unity.txt`
원본 모델 파일은 보존합니다.

## 검증 및 재사용

- 재질과 방향을 연결한 개별 프리팹 24개: `Assets/Art/TowerDungeon/Prefabs`
- 기존 충돌체 22개의 위치·회전·크기가 수정 전과 일치합니다.
- 새 장식 Renderer 170개, 새 장식 Collider 0개, 활성 카메라 1개를 확인했습니다.
- 실제 플레이 검사: 6.43m 이동, 3.18m 점프 상승 후 접지 복귀.
- 시작·중간·보스 구간 화면: `Dungeon_Entry.png`, `Dungeon_Middle.png`, `Dungeon_Boss.png`.
- 전체 전투 진행을 끝까지 플레이하는 검증은 이번 외형 작업에서 수행하지 않았습니다.
