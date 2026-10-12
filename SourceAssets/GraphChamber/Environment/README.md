# Graph Chamber Environment

Editable low-poly reconstruction of the supplied empty Graph Chamber concept
(`ChatGPT Image Oct 11, 2026, 10_50_34 PM.png`). The environment includes the
visible architecture and scenery: a tiled and cracked floor, concentric stone
inlays, central diamond, enclosed back and side walls, pointed alcoves, square
columns, blue banners with gold trim and cyan emblems, stone torch pedestals,
faceted flames, blue crystal clusters, a front parapet, and entrance steps.

This is a modeled interpretation of one perspective image. The reference does
not show hidden or rear surfaces, so those are completed as practical game
geometry. It does not include graph vertex platforms, edge bridges, the book
statue, or an information board.

## Deliverables

- `Assets/Art/GraphChamber/Environment/Graph_Chamber_Environment.fbx` — Y-up,
  metre-scale FBX for Unity, with all visible environment props as geometry.
- `Assets/Art/GraphChamber/Environment/GraphChamber_Palette.png` — 128 px shared
  palette used by the five placeholder materials. It is embedded in the FBX.
- `SourceAssets/GraphChamber/Environment/Graph_Chamber_Environment.blend` —
  editable Blender source with named parts and no modifiers.
- `SourceAssets/GraphChamber/Environment/Graph_Chamber_Environment_Preview.png`
  and `Graph_Chamber_Environment_Angled.png` — views rendered from the reimported
  FBX with preview-only lighting and camera.
- `SourceAssets/GraphChamber/Environment/geometry-report.json` — measured
  geometry and FBX round-trip check.
- `Tools/create_graph_chamber_environment.py` — Blender 4.5 build script.

The floor-centered origin is at `(0, 0, 0)` in Blender, and the open gameplay
center has its own empty anchor. The playable interior is roughly 15 m wide,
11 m deep, and 5.6 m high. The FBX has 40 named meshes, 21,998 vertices,
16,311 faces, and 33,428 triangles. It uses five shared placeholder materials
and no modifiers. The FBX was reimported into Blender and checked for matching
triangle count, no loose vertices, and no zero-area faces.

Unity lighting, exposure, bloom, and tone mapping should be adjusted to match
the preview's blue crystal and orange torch glow. The rendered preview lights
and camera are not included in the FBX; the flame and crystal shapes are.
