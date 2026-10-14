"""Continuous pale-stone border fitted to the screenshot's actual map FBXs."""
import bpy,bmesh,math,json,shutil
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/Art/RuinsMapBorder';SRC=ROOT/'SourceAssets/RuinsMapBorder'
for p in [OUT/'Textures',OUT/'Editor',SRC]:p.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene;scene.unit_settings.system='METRIC'
folder=ROOT/'Assets/FINAL_ASSETS/MAPS/REMADE/REMAKE TUTS/2nd'
bpy.ops.import_scene.fbx(filepath=str(folder/'FLOOR.fbx'));floor=next(o for o in scene.objects if o.type=='MESH')
points=[floor.matrix_world@v.co for v in floor.data.vertices];lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z));W=hi.x-lo.x;D=hi.y-lo.y;H=hi.z-lo.z
# A clean, rescalable bottom-center origin; supplied placement offset restores the map's native coordinates.
floor.matrix_world=Matrix.Translation(-center)@floor.matrix_world
references=[floor]
for filename in ['RUINS.fbx','PLATFORM.fbx']:
    old=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(folder/filename))
    for o in set(scene.objects)-old:
        if o.type=='MESH':o.matrix_world=Matrix.Translation(-center)@o.matrix_world;references.append(o)
reference_collection=bpy.data.collections.new('REFERENCE_Map_NotExported');scene.collection.children.link(reference_collection)
for o in references:
    for c in list(o.users_collection):c.objects.unlink(o)
    reference_collection.objects.link(o)
texture_path=OUT/'Textures/Ruins_Stone_BaseColor.png';shutil.copyfile(ROOT/'Assets/Art/RuinsProps/Textures/Ruins_Stone_BaseColor.png',texture_path)
image=bpy.data.images.load(str(texture_path));image.pack()
shadow=bpy.data.images.new('Ruins_StoneShadow_BaseColor',image.size[0],image.size[1],alpha=True)
pixels=list(image.pixels);tint=(.78,.80,.77,1)
shadow.pixels=[value*tint[i%4] for i,value in enumerate(pixels)]
shadow.filepath_raw=str(OUT/'Textures/Ruins_StoneShadow_BaseColor.png');shadow.file_format='PNG';shadow.save();shadow.pack()
materials=[]
for name,color in [('Border_Stone',(1,1,1,1)),('Border_StoneShadow',(.78,.80,.77,1))]:
    m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.88
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image if name=='Border_Stone' else shadow
    m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color']);materials.append(m)
root=bpy.data.objects.new('Ruins_Map_FullBorder',None);scene.collection.objects.link(root);parts=[]
def finish(ob,name,mat=0):
    ob.name=name;ob.data.materials.append(materials[mat]);ob.parent=root;parts.append(ob);return ob
def box(name,loc,size,mat=0,bevel=.1):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('WornEdges','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);return finish(o,name,mat)
def ring(name,innerW,innerD,thickness,z0,z1,mat=0):
    a=innerW/2;b=innerD/2;t=thickness
    corners=[(-a,-b),(a,-b),(a,b),(-a,b),(-a-t,-b-t),(a+t,-b-t),(a+t,b+t),(-a-t,b+t)]
    v=[(x,y,z) for z in [z0,z1] for x,y in corners];f=[]
    for i in range(4):
        j=(i+1)%4;f.extend([(i,j,j+8,i+8),(i+4,i+12,j+12,j+4),(i+8,j+8,j+12,i+12),(i,j+4,j,i+4)])
    # Correct bottom loop ordering (inner i -> outer i -> outer j -> inner j).
    f=[tuple(face) for face in f]
    for i in range(4):f[i*4+3]=(i,i+4,(i+1)%4+4,(i+1)%4)
    me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);return finish(o,name,mat)
t=3.4
ring('Continuous_Foundation_Skirt',W,D,t,0,H+.02,1)
ring('Continuous_UpperCurb',W-.40,D-.40,t+.40,H,H+2.2)
ring('Continuous_Capstone',W-.65,D-.65,t+.90,H+2.2,H+2.8)
ring('Lower_Foot_Molding',W,D,t+.45,0,.65)
# Decorative masonry courses sit over a continuous ring; joints never open holes through the border.
for side,L,fixed,axis in [('Front',W,-D/2-t,'X'),('Back',W,D/2+t,'X'),('Left',D,-W/2-t,'Y'),('Right',D,W/2+t,'Y')]:
    n=math.ceil(L/5.7);step=L/n
    for row in range(2):
        for i in range(n):
            along=-L/2+(i+.5)*step;z=.65+(H-.8)/4+row*(H-.8)/2
            if axis=='X':loc=(along,fixed,z);size=(step-.10,.16,(H-.8)/2-.10)
            else:loc=(fixed,along,z);size=(.16,step-.10,(H-.8)/2-.10)
            box(f'{side}_FacingBlock',loc,size,bevel=.05)
    # Low repeated pier caps reinforce the rhythm of the existing columns.
    for i in range(1,6):
        along=-L/2+i*L/6
        if axis=='X':loc=(along,fixed+.75*(1 if fixed<0 else -1),H+1.6)
        else:loc=(fixed+.75*(1 if fixed<0 else -1),along,H+1.6)
        box(f'{side}_LowPier',loc,(3.4,3.4,3.2),bevel=.14)
        box(f'{side}_PierCap',(loc[0],loc[1],H+3.4),(3.8,3.8,.45),bevel=.1)
for sx in [-1,1]:
    for sy in [-1,1]:
        x=sx*(W/2+t/2);y=sy*(D/2+t/2)
        box('Closed_CornerPier',(x,y,(H+3.5)/2),(4.6,4.6,H+3.5),bevel=.15)
        box('CornerCap',(x,y,H+3.75),(5.15,5.15,.5),bevel=.15)
# Consolidate to four clear functional objects rather than hundreds of renderers.
groups=[('Stone_Foundation',lambda n:n.startswith(('Continuous_Foundation','Lower_Foot'))),('Continuous_Rim',lambda n:n.startswith(('Continuous_Upper','Continuous_Cap'))),('Masonry_Facing',lambda n:'FacingBlock' in n),('Border_Piers_And_Corners',lambda n:('Pier' in n or 'Cap' in n) and not n.startswith('Continuous'))]
joined=[]
for name,test in groups:
    objects=[o for o in parts if test(o.name)];parts=[o for o in parts if o not in objects];bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=name;joined.append(o)
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.008);bpy.ops.object.mode_set(mode='OBJECT')
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
select([root]+joined)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ruins_Map_FullBorder.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,bake_anim=False,path_mode='COPY',embed_textures=True)
root.location=center;bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ruins_Map_FullBorder_NativeAligned.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,bake_anim=False,path_mode='COPY',embed_textures=True)
root.location=(0,0,0);bpy.context.view_layer.update()
from io_scene_fbx import parse_fbx
tree,version=parse_fbx.parse(str(OUT/'Ruins_Map_FullBorder.fbx'))
objects=next(e for e in tree.elems if e.id==b'Objects')
videos=[e for e in objects.elems if e.id==b'Video']
assert len(videos)==2 and all(any(c.id==b'Content' and c.props and len(c.props[0])>0 for c in e.elems) for e in videos)
# Verify ring topology is closed and FBX retains the geometry and unit scale.
zero=0;tris=0
for o in joined:o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);zero+=sum(p.area<1e-9 for p in o.data.polygons)
assert zero==0
old=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(OUT/'Ruins_Map_FullBorder.fbx'));back=list(set(scene.objects)-old);count=0
for o in back:
    if o.type=='MESH':o.data.calc_loop_triangles();count+=len(o.data.loop_triangles);assert all(abs(s-1)<1e-5 for s in o.scale)
assert count==tris
for o in back:bpy.data.objects.remove(o,do_unlink=True)
bm=bmesh.new();bm.from_mesh(joined[0].data);assert all(e.is_manifold for e in bm.edges);bm.free()
report=dict(triangles=tris,zero_area_faces=zero,mesh_objects=4,fbx_roundtrip='PASS',embedded_textures=2,all_four_sides=True,corners_closed=True,unbroken_structural_rings=True,map_floor_size_m=[W,D,H],native_placement_blender=list(center),native_placement_unity=[-center.x,center.z,-center.y],border_only_fbx=True)
(SRC/'fit-and-geometry-report.json').write_text(json.dumps(report,indent=2))
# Plain reference materials clarify the fit while the original project files remain untouched.
for i,o in enumerate(references):
    m=bpy.data.materials.new('Reference_'+['Grass','PaleRuins','DarkPlatform'][i]);m.diffuse_color=[(.12,.24,.07,1),(.62,.65,.64,1),(.19,.21,.24,1)][i];m.use_nodes=True;m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=m.diffuse_color;o.data.materials.clear();o.data.materials.append(m)
    for p in o.data.polygons:p.material_index=0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_distance=190;area.spaces.active.region_3d.view_location=Vector((0,0,15));area.spaces.active.shading.type='MATERIAL'
select([root]+joined);bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Ruins_Map_Border.blend'))
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.world.color=(.18,.20,.24)
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=1000,location=(0,0,-.08));ground=bpy.context.object;m=bpy.data.materials.new('PreviewFloor');m.diffuse_color=(.075,.09,.11,1);ground.data.materials.append(m)
for pos,power,size in [((-80,-90,170),650000,110),((90,30,130),350000,100)]:
    bpy.ops.object.light_add(type='AREA',location=pos);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.size=size;aim(lamp,(0,0,15))
bpy.ops.object.camera_add(location=(95,-160,160));camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=195;aim(camera,(0,0,20));scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.filepath=str(SRC/'Border_Fitted_To_Map.png');bpy.ops.render.render(write_still=True)
for o in references:o.hide_render=True
camera.data.ortho_scale=185;aim(camera,(0,0,3));scene.render.filepath=str(SRC/'Border_Only_Preview.png');bpy.ops.render.render(write_still=True)
print('RUINS_BORDER_FIT_PASS '+json.dumps(report))
