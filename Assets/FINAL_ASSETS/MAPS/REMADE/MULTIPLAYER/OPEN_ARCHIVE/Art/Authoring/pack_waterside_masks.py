"""Small linear-data maps for flowing water, rock grain and leaf detail. Originals are read-only."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'ProjectSettings/ProjectVersion.txt').exists())
ART = Path(__file__).resolve().parents[1]
N = 256
rng = np.random.default_rng(20261019)

def noise(x, y):
    a = rng.integers(0, 256, (y, x), dtype=np.uint8)
    a = np.pad(a, ((1, 1), (1, 1)), mode='wrap')
    im = Image.fromarray(a).resize((N + 2 * N // x, N + 2 * N // y), Image.Resampling.BICUBIC)
    return np.asarray(im.crop((N // x, N // y, N // x + N, N // y + N)), dtype=float) / 255

flow = np.stack((noise(32, 4), noise(24, 24), noise(13, 7), noise(7, 7)), axis=-1)
Image.fromarray(np.uint8(flow * 255), 'RGBA').save(ART / 'Textures/Archive_FlowMasks.png')
stone = Image.open(ROOT / 'Assets/CHEATCODE/ROCKTEXTURE/Textures/T_YFRM_05_d.tga').convert('L').resize((N, N), Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(1.1))
stone = np.asarray(stone, dtype=float) / 255
grain = np.clip(.5 + (stone - stone.mean()) * 1.5, .22, .82)
cavity = np.clip((np.percentile(stone, 30) - stone) * 4, 0, 1)
leaves = Image.new('L', (N, N), 105)
veins = Image.new('L', (N, N))
draw = ImageDraw.Draw(leaves); vein = ImageDraw.Draw(veins)
for row in range(-1, 9):
    for col in range(-1, 9):
        x = col * 36 + (row % 2) * 18
        y = row * 34
        # Repeated tapered leaf pairs; wrap beyond the edges before cropping.
        points = [(x-12,y), (x-7,y-9), (x+2,y-13), (x+11,y-5), (x+10,y+5), (x+1,y+13), (x-7,y+7)]
        draw.polygon(points, fill=int(rng.integers(155, 215)))
        vein.line([(x-7,y+6),(x+6,y-8)], fill=155, width=1)
        vein.line([(x-3,y+1),(x-7,y-4)], fill=85, width=1)
leaves = np.asarray(leaves.filter(ImageFilter.GaussianBlur(.55)), dtype=float) / 255
veins = np.asarray(veins.filter(ImageFilter.GaussianBlur(.4)), dtype=float) / 255
packed = np.stack((grain, cavity, leaves, veins), axis=-1)
Image.fromarray(np.uint8(packed * 255), 'RGBA').save(ART / 'Textures/Archive_NaturalSurfaceMasks.png')
(ART / 'Reports/WatersideTextureSources.json').write_text(json.dumps({
    'rockSource': 'Assets/CHEATCODE/ROCKTEXTURE/Textures/T_YFRM_05_d.tga',
    'originalsModified': False, 'size': 256,
    'flowChannels': ['vertical streaks', 'foam noise', 'broad turbulence', 'shore variation'],
    'naturalChannels': ['soft rock grain', 'natural cavities', 'leaf shapes', 'leaf veins'],
}, indent=2), encoding='utf-8')
print('Packed two 256px waterside data textures')
