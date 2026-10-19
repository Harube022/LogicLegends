"""Pack existing painted timber and foliage into small linear detail maps. Sources stay untouched."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'ProjectSettings/ProjectVersion.txt').exists())
ART = Path(__file__).resolve().parents[1]
N = 512
wood_path = 'Assets/CHEATCODE/WOODTEXTURE/Textures/Wood3.tga'
leaf_path = 'Assets/CHEATCODE/LOWPOLYENVIRO/Lowpoly_Environments/Sources/Textures/PT_Generic_Tree_Leaves_01.png'
wood = Image.open(ROOT / wood_path).convert('L').resize((N, N), Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(.5))
wood = np.asarray(wood, dtype=float) / 255
grain = np.clip(.5 + (wood - wood.mean()) * 2.3, .12, .92)
cavity = np.clip((np.percentile(wood, 38) - wood) * 5, 0, 1)
y, x = np.mgrid[0:N, 0:N] / N
radius = np.sqrt(((x - .5) * 1.02) ** 2 + ((y - .5) * .98) ** 2)
rings = .5 + .19 * np.sin(radius * 130 + grain * .7)
rng = np.random.default_rng(20261019)
noise = Image.fromarray(rng.integers(0, 256, (16, 16), dtype=np.uint8)).resize((N, N), Image.Resampling.BICUBIC)
weather = np.asarray(noise, dtype=float) / 255
Image.fromarray(np.uint8(np.clip(np.stack((grain, cavity, rings, weather), -1), 0, 1) * 255), 'RGBA').save(ART / 'Textures/Archive_TimberDetail.png')

leaf = Image.open(ROOT / leaf_path).convert('RGBA').resize((N, N), Image.Resampling.LANCZOS)
rgba = np.asarray(leaf, dtype=float) / 255
coverage = rgba[:, :, 3]
if np.min(coverage) > .99:
    coverage = np.clip(rgba[:, :, 1] * 3, 0, 1)
green = rgba[:, :, 1]
veins = np.clip((green - np.percentile(green[coverage > .8], 50)) * 12, 0, 1) * coverage
needles = Image.new('L', (N, N), 75)
draw = ImageDraw.Draw(needles)
for row in range(-1, 9):
    for col in range(-1, 9):
        cx = col * 64 + (row % 2) * 32
        cy = row * 64
        draw.line([(cx, cy-24), (cx, cy+24)], fill=135, width=2)
        for k in range(-4, 5):
            dy = k * 5
            width = 21 - abs(k) * 2
            draw.line([(cx,cy+dy),(cx-width,cy+dy+10)],fill=195,width=2)
            draw.line([(cx,cy+dy),(cx+width,cy+dy+10)],fill=170,width=2)
needle = np.asarray(needles.filter(ImageFilter.GaussianBlur(.55)), dtype=float) / 255
leaf_detail = np.stack((coverage, veins, needle, weather), -1)
Image.fromarray(np.uint8(leaf_detail * 255), 'RGBA').save(ART / 'Textures/Archive_CanopyDetail.png')
(ART / 'Reports/TreeBridgeTextureSources.json').write_text(json.dumps({
    'sources': [wood_path, leaf_path], 'originalsModified': False, 'size': '512x512 each',
    'timberChannels': ['painted timber grain', 'natural grooves', 'end grain rings', 'weather variation'],
    'canopyChannels': ['existing painted leaf silhouettes', 'leaf veins', 'pine needle detail mask', 'cluster variation'],
}, indent=2), encoding='utf-8')
print('Packed tree and bridge detail from existing project textures')
