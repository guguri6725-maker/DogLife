# 제작 자료 / 2026-09-22

- 모델 원본: SeasonCrafted.blend
- 재생성 스크립트: build_season.py
- Unity 모델과 텍스처: Assets/Art/SeasonCrafted
- 모델 11종: 고목, 드러난 뿌리, 유목, 청동 문고리, 야자수 2종, 해안 바위, 유적 아치, 파라솔, 해변 의자, 조개.
- 텍스처는 내장 image_gen 도구로 생성했으며 아래 프롬프트를 사용했다.

Create a production game texture atlas, square 2048x2048, exactly FOUR equally sized square quadrants with NO gutters NO border NO text. Orthographic flat albedo only, no directional lighting, no perspective, no objects. Painterly detailed stylized fantasy game matching hand painted forest dungeon. TOP LEFT: continuous tileable weathered dark warm brown ancient tree bark, deeply winding grain with subtle moss in cracks, rich readable detail. TOP RIGHT: continuous tileable pale warm golden beach sand with fine ripples and sparse tiny shell flecks. BOTTOM LEFT: continuous tileable sun bleached ivory limestone with ochre cracks and subtle blue grey weathering. BOTTOM RIGHT: continuous tileable lush green palm leaf surface, long fine parallel veins running vertically and gentle green variations, no isolated leaf silhouettes. Each quadrant fills its full square as a material swatch, no outlines, no shadows, no scene. This atlas will texture custom modeled deadwood, exposed roots, beach sand, coastal ruins and palm fronds in Unity.
