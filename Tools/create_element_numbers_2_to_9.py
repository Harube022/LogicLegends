"""Generate numbered variants of the existing Sets element cube in Blender 4.5."""
import bpy,bmesh,math,json,shutil
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/SetsGameplay/Elements2To9';SRC=ROOT/'SourceAssets/SetsGameplay/Elements2To9'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'SourceAssets/SetsGameplay/Sets_Gameplay_Assets.blend'))
bpy.context.preferences.filepaths.save_version=0
template=bpy.data.objects['Element_Object'];keep={template,*template.children_recursive}
for ob in list(bpy.context.scene.objects):
    if ob not in keep:bpy.data.objects.remove(ob,do_unlink=True)
bpy.data.objects.remove(bpy.data.objects['Element_Label_Example_1'],do_unlink=True)
template.location=(0,0,0)
body=list(template.children)
glow=bpy.data.materials['Sets_Glow'];font=bpy.data.fonts.load('C:/Windows/Fonts/arialbd.ttf')
palette=bpy.data.images['SetsGameplay_Palette'];palette.filepath_raw=str(OUT/'SetsGameplay_Palette.png');palette.file_format='PNG';palette.save();palette.pack()

def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs:ob.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in obs if o.type=='MESH')
def stats(obs):
    r={'vertices':0,'faces':0,'triangles':0,'mesh_objects':0,'zero_area_faces':0,'loose_vertices':0}
    for o in obs:
        if o.type!='MESH':continue
        m=o.data;m.calc_loop_triangles();r['vertices']+=len(m.vertices);r['faces']+=len(m.polygons);r['triangles']+=len(m.loop_triangles);r['mesh_objects']+=1
        bm=bmesh.new();bm.from_mesh(m);r['zero_area_faces']+=sum(f.calc_area()<1e-11 for f in bm.faces);r['loose_vertices']+=sum(not v.link_faces for v in bm.verts);bm.free()
    return r
variants=[];report={}
for digit in range(2,10):
    root=bpy.data.objects.new(f'Element_Object_{digit}',None);bpy.context.collection.objects.link(root)
    for original in body:
        ob=original.copy();ob.name=original.name.replace('Element_',f'Element_{digit}_');bpy.context.collection.objects.link(ob);ob.parent=root
    cu=bpy.data.curves.new(f'Number_{digit}','FONT');cu.font=font;cu.body=str(digit);cu.size=.4;cu.align_x='CENTER';cu.align_y='CENTER';cu.resolution_u=3;cu.extrude=.003
    ob=bpy.data.objects.new(f'Element_{digit}_Number',cu);bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH');ob=bpy.context.object
    # Normalize every numeral to the same cap height and center above the cube.
    vv=ob.data.vertices;lo=Vector(tuple(min(v.co[i] for v in vv) for i in range(3)));hi=Vector(tuple(max(v.co[i] for v in vv) for i in range(3)))
    center=(lo+hi)/2;s=.27/(hi.y-lo.y)
    for v in vv:
        q=(v.co-center)*s;v.co=Vector((q.x,-q.z,q.y))
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
    bad=[f for f in bm.faces if f.calc_area()<1e-11]
    if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
    loose=[v for v in bm.verts if not v.link_faces]
    if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
    ob.location=(0,-.015,.795);ob.parent=root;ob.data.materials.clear();ob.data.materials.append(glow)
    for layer in list(ob.data.uv_layers):ob.data.uv_layers.remove(layer)
    uv=ob.data.uv_layers.new(name='PaletteUV')
    for loop in uv.data:loop.uv=((23%8+.5)/8,(23//8+.5)/8)
    obs=[root]+list(root.children_recursive);report[str(digit)]=stats(obs)
    assert not report[str(digit)]['zero_area_faces'] and not report[str(digit)]['loose_vertices']
    select(obs)
    bpy.ops.export_scene.fbx(filepath=str(OUT/f'Element_Object_{digit}.fbx'),use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,path_mode='COPY',embed_textures=True)
    variants.append(root)
for ob in [template]+body:bpy.data.objects.remove(ob,do_unlink=True)
for i,root in enumerate(variants):root.location=((i-3.5)*.90,0,0)
# All numerals are mesh geometry, so no external font is needed to edit/import.
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':a.spaces.active.region_3d.view_distance=8;a.spaces.active.region_3d.view_location=Vector((0,0,.4));a.spaces.active.shading.type='MATERIAL'
select([o for root in variants for o in [root]+list(root.children_recursive)])
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Element_Objects_2_to_9.blend'))
scene=bpy.context.scene
for digit in range(2,10):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(OUT/f'Element_Object_{digit}.fbx'))
    obs=list(scene.objects);actual=stats(obs);assert actual==report[str(digit)],(digit,actual,report[str(digit)])
    assert all(o.type in {'MESH','EMPTY'} and not o.modifiers and all(abs(v-1)<1e-5 for v in o.scale) for o in obs)
    assert bpy.data.objects[f'Element_Object_{digit}'].location.length<1e-5
    report[str(digit)]['fbx_roundtrip']='PASS'
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for i,digit in enumerate(range(2,10)):
    bpy.ops.import_scene.fbx(filepath=str(OUT/f'Element_Object_{digit}.fbx'));bpy.data.objects[f'Element_Object_{digit}'].location=((i-3.5)*.90,0,0)
for m in bpy.data.materials:
    if m.name.startswith('Sets_Glow') and m.use_nodes:
        p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Emission Strength'].default_value=2
        tex=next((n for n in m.node_tree.nodes if n.type=='TEX_IMAGE'),None)
        if tex:m.node_tree.links.new(tex.outputs['Color'],p.inputs['Emission Color'])
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1920;scene.render.resolution_y=650;scene.render.resolution_percentage=100;scene.world.color=(.05,.07,.10)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015));floor=bpy.context.object;m=bpy.data.materials.new('PreviewGround');m.diffuse_color=(.055,.075,.105,1);floor.data.materials.append(m)
for p,e,size in [((0,-4,5),900,7),((-3,3,4),700,5)]:
    bpy.ops.object.light_add(type='AREA',location=p);light=bpy.context.object;light.data.energy=e;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(Vector((0,0,.4))-light.location).to_track_quat('-Z','Y').to_euler()
scene.use_nodes=True;n=scene.node_tree.nodes;n.clear();rl=n.new('CompositorNodeRLayers');gl=n.new('CompositorNodeGlare');gl.glare_type='FOG_GLOW';gl.threshold=1.4;gl.quality='HIGH';co=n.new('CompositorNodeComposite');scene.node_tree.links.new(rl.outputs['Image'],gl.inputs['Image']);scene.node_tree.links.new(gl.outputs['Image'],co.inputs['Image'])
bpy.ops.object.camera_add(location=(0,-8,3.7));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.42))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=8.25;scene.camera=cam
scene.render.filepath=str(SRC/'Element_Objects_2_to_9_Preview.png');bpy.ops.render.render(write_still=True)
print('NUMBERED_ELEMENTS '+json.dumps(report))
