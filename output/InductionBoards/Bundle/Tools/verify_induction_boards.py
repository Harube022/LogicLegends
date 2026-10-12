"""Read-only verification of texture embedding and portable FBX references."""
import bpy, json
from pathlib import Path
from io_scene_fbx import parse_fbx
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/InductionBoards'
report=[]
for name in ['InformationBoard','FormulaScrollsStand']:
    tree,version=parse_fbx.parse(str(OUT/(name+'.fbx')))
    contents=[];refs=[]
    def walk(e):
        if e.id==b'Video':
            content=next((c.props[0] for c in e.elems if c.id==b'Content' and c.props),None)
            if content:contents.append(len(content))
        if e.id==b'RelativeFilename' and e.props:refs.append(e.props[0].decode(errors='replace'))
        for c in e.elems:walk(c)
    walk(tree)
    assert len(contents)>=12,(name,len(contents))
    assert all(not Path(ref.replace('\\','/')).is_absolute() and ':/' not in ref.replace('\\','/') for ref in refs)
    report.append(dict(model=name,fbx_version=version,embedded_images=len(contents),embedded_bytes=sum(contents),relative_paths='PASS'))
maps=[]
for path in sorted((OUT/'Textures').glob('*.png')):
    image=bpy.data.images.load(str(path));assert image.size[0]>0 and image.size[1]>0
    maps.append(dict(file=path.name,size=list(image.size)))
assert len(maps)==16
result=dict(fbx=report,texture_files=maps,status='PASS')
(ROOT/'SourceAssets/InductionBoards/embedding-validation.json').write_text(json.dumps(result,indent=2))
print('INDUCTION_BOARDS_EMBEDDING_PASS '+json.dumps(report))
