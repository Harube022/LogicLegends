"""Neutral, tileable stone grain from the existing rock texture; originals stay intact."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter, ImageOps

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'ProjectSettings/ProjectVersion.txt').exists())
ART = Path(__file__).resolve().parents[1]
SOURCE = 'Assets/CHEATCODE/ROCKTEXTURE/Textures/T_YFRM_05_d.tga'
gray = ImageOps.grayscale(Image.open(ROOT / SOURCE)).resize((512, 512), Image.Resampling.LANCZOS)
gray = np.asarray(gray.filter(ImageFilter.GaussianBlur(.6)), dtype=np.float32) / 255
# Offset-and-blend only at repeat borders to avoid a visible square seam.
for axis in (0, 1):
    offset = np.roll(gray, 256, axis=axis)
    edge = np.minimum(np.arange(512), np.arange(511, -1, -1))
    blend = np.clip((24 - edge) / 24, 0, 1) * .5
    blend = blend[:, None] if axis == 0 else blend[None, :]
    gray = gray * (1 - blend) + offset * blend
grain = np.clip(.5 + (gray - gray.mean()) * 2.8, .10, .94)
cavity = np.clip((np.percentile(gray, 38) - gray) * 7, 0, 1)
packed = np.stack((grain, cavity, gray, np.ones_like(gray)), axis=-1)
Image.fromarray(np.uint8(packed * 255), 'RGBA').save(ART / 'Textures/Archive_StoneGrain.png')
(ART / 'Reports/StoneTextureSource.json').write_text(json.dumps({
    'source': SOURCE, 'originalModified': False, 'size': '512x512',
    'channels': {'R': 'neutral rock grain', 'G': 'natural crevice mask', 'B': 'softened source luminance'},
    'intent': 'Visible rock grain, stone crevices and stable beveled paving joints; preserve palette colors.'
}, indent=2), encoding='utf-8')
print('Packed stone grain:', ART / 'Textures/Archive_StoneGrain.png')
