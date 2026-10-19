"""Pack existing project paintwork into small, neutral surface-detail masks.
No geometry, UVs, colliders or original source textures are changed.
R: painted grain; G: cracks/wood grain; B: moss; A: roughness variation.
"""
from pathlib import Path
import json
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageOps

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'ProjectSettings/ProjectVersion.txt').exists())
OUT = Path(__file__).resolve().parents[1] / 'Textures'
OUT.mkdir(parents=True, exist_ok=True)
N = 496
rng = np.random.default_rng(619)
sources = [
    'Assets/CHEATCODE/GRASSTEXTURE/Textures/Grass/Grass_normal/Grass_normal_up.png',
    'Assets/CHEATCODE/GRASSTEXTURE/Textures/Dirt/dirt_desatured_rocks/dirt_desatured_rocks_up.png',
    'Assets/CHEATCODE/ROCKTEXTURE/Textures/T_YFRM_01_d.tga',
]

def noise(size, cells, seed):
    r = np.random.default_rng(seed)
    a = r.integers(0, 256, (cells, cells), dtype=np.uint8)
    # Periodic border, for continuous sampling across repeated tiles.
    a = np.pad(a, ((1, 1), (1, 1)), mode='wrap')
    im = Image.fromarray(a).resize((size + 2 * size // cells,) * 2, Image.Resampling.BICUBIC)
    margin = size // cells
    return np.asarray(im.crop((margin, margin, margin + size, margin + size)), dtype=float) / 255

atlas = Image.new('RGBA', (1024, 1024))
review = Image.new('RGB', (1024, 1024))
for tile in range(4):
    if tile < 3:
        source = Image.open(ROOT / sources[tile]).convert('RGB').resize((N, N), Image.Resampling.LANCZOS)
        gray = np.asarray(ImageOps.grayscale(source).filter(ImageFilter.GaussianBlur(.8)), dtype=float) / 255
        grain = np.clip((gray - gray.mean()) * (2.0 if tile < 2 else 1.15) + .54, .18, .88)
        if tile == 2:  # Keep the rock's strata, soften photographic microcontrast.
            grain = np.round(grain * 13) / 13
    else:
        y, x = np.mgrid[0:N, 0:N]
        grain = .54 + .075 * np.sin(x * .10 + np.sin(y * .018) * 3) + .025 * np.sin(x * .53 + y * .012)
        source = Image.fromarray(np.uint8(np.clip(grain, 0, 1) * 255)).convert('RGB')
    cracks = Image.new('L', (N, N))
    d = ImageDraw.Draw(cracks)
    r = random.Random(731 + tile)
    if tile == 2:
        for _ in range(9):
            p = (r.randrange(N), r.randrange(N))
            points = [p]
            for k in range(r.randrange(3, 6)):
                p = (p[0] + r.randrange(-24, 25), p[1] + r.randrange(14, 42))
                points.append(p)
            d.line(points, fill=r.randrange(110, 190), width=1)
            d.line([points[1], (points[1][0] + 18, points[1][1] - 13)], fill=100, width=1)
    elif tile == 3:
        for x in range(10, N, 27):
            d.line([(x + np.sin(y * .025) * 3, y) for y in range(0, N, 8)], fill=65, width=1)
    moss = np.clip((noise(N, 11, tile + 40) - .48) * 3.2, 0, 1)
    # The pack is data, not albedo: retain original material and vertex palettes.
    packed = np.stack((grain, np.asarray(cracks) / 255, moss, noise(N, 17, tile + 90)), axis=-1)
    im = Image.fromarray(np.uint8(np.clip(packed, 0, 1) * 255), 'RGBA')
    cell = Image.fromarray(np.pad(np.asarray(im), ((8, 8), (8, 8), (0, 0)), mode='wrap'))
    # Unity's UV origin is bottom left; the upper PNG row is stone and wood.
    at = ((tile % 2) * 512, (1 - tile // 2) * 512)
    atlas.paste(cell, at)
    review.paste(source.resize((512, 512)), at)
atlas.save(OUT / 'Archive_SurfaceDetailAtlas.png')
macro = np.stack([noise(128, 9, 91), noise(128, 13, 93), noise(128, 7, 97), noise(128, 19, 99)], axis=-1)
Image.fromarray(np.uint8(np.clip(macro, 0, 1) * 255), 'RGBA').save(OUT / 'Archive_GroundVariation.png')
(OUT.parent / 'Reports').mkdir(exist_ok=True)
(OUT.parent / 'Reports/TextureSources.json').write_text(json.dumps({
    'sources': sources, 'detailAtlas': '1024x1024, four padded 496px tiles',
    'macroTexture': '128x128 RGBA packed masks',
    'originalTexturesModified': False, 'generated': ['neutral grain masks', 'fine stone cracks', 'wood grain', 'moss masks'],
}, indent=2), encoding='utf-8')
print('Packed surface masks from', ROOT)
