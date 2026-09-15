# 달빛 폐성 — 횡스크롤 테스트 맵

- 씬: `Assets/Scenes/OriginalSetup.unity`
- Hierarchy: `MoonlitRuins` 아래 이동 발판 / 유적 / 원경 / 전경 / 조명으로 분류했습니다.
- 경로: 돌다리 입구 → 기둥 위 점프 구간 → 성탑 광장 → 높은 성소.
- 조작: A/D 이동, Space 점프, Shift 대시. 청록색 문 표식이 끝 지점입니다.

## 모델 작업

`Assets/VARCO3DImports`의 USDZ 12종을 Blender에서 복제해 수정했습니다. 원본은 보존했습니다.
모델의 깊이를 줄이고 밑면 중앙을 피벗으로 맞췄으며, 발판은 가로 폭과 높이를 통일했습니다.
메시는 모델당 약 16,000면으로 줄였고, 서로 같은 이름을 쓰던 텍스처는 모델별 폴더로 분리했습니다.

- 편집 원본: `SideViewRuins.blend`
- 원본과 변환 모델 대응: `model_mapping.json`
- Unity용 FBX·재질·텍스처: `Assets/Art/SideViewRuins`

## 화면과 충돌

정사영 카메라, 광원에 영향을 받지 않는 텍스처 재질, 푸른 원경과 짙은 전경 실루엣으로 2D 느낌을 구성했습니다.
카메라는 Player의 자식으로 연결했습니다. 발판 충돌은 별도의 BoxCollider 13개로 구성하고 Ground 레이어 8을 사용합니다.
성소 표식은 맵 종점 표시용이며 별도의 클리어 이벤트는 없습니다.

## 검증

플레이 모드에서 기존 SideViewMotor의 이동과 RequestJump로 7개 점프 구간을 통과하고 x=58 성소에 도착했습니다.
검증 로그: `RUINS_ROUTE_PASS: 7 jumps, sanctuary reached.`
검증용 이동 콜백은 종료 시 해제되며 저장된 씬에 테스트 스크립트를 추가하지 않았습니다.
콘솔 오류 0개를 확인했습니다.
