"""Create a low-poly book lectern statue from the supplied visual reference."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'SourceAssets/BookStatue'
OUT = ROOT / 'Assets/Art/BookStatue'
SRC.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, metallic=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = m.diffuse_color
    p.inputs['Roughness'].default_value = .82
    p.inputs['Metallic'].default_value = metallic
    return m

stone = material('Warm charcoal stone', (.145,.125,.115))
edge = material('Weathered stone edges', (.245,.224,.203))
cover = material('Dark brown book cover', (.095,.068,.049))
paper = material('Warm gray stone pages', (.43,.411,.366))
layer = material('Page edges', (.31,.293,.259))
purple = material('Muted amethyst ornament', (.105,.055,.14), .12)
rim = material('Ornament bezel', (.22,.18,.145), .15)
parts = []

def mesh(name, vertices, faces, mat):
    me = bpy.data.meshes.new(name)
    me.from_pydata(vertices, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    me.materials.append(mat)
    parts.append(ob)
    return ob

def tier(name, z, h, rx, ry, top_scale, mat, n=8):
    vs = []
    for zz, s in [(z,1),(z+h,top_scale)]:
        for i in range(n):
            a = math.tau*i/n+(math.pi/4 if n==4 else math.pi/8)
            vs.append((math.cos(a)*rx*s, math.sin(a)*ry*s, zz))
    fs = [tuple(reversed(range(n))), tuple(range(n,2*n))]
    fs += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,mat)

def box(name, center, size, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    ob=bpy.context.object
    ob.name=name
    ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    ob.data.materials.append(mat)
    parts.append(ob)
    return ob

box('Square foundation',(0,0,.065),(1.46,1.30,.13),stone)
tier('Foundation chamfer',.13,.10,.90,.79,.90,stone,4)
tier('Lower octagonal step',.23,.10,.67,.59,1,edge)
tier('Middle octagonal step',.33,.095,.60,.53,1,stone)
tier('Upper octagonal step',.425,.085,.53,.46,.91,edge)
box('Pedestal foot',(0,0,.55),(.65,.55,.08),stone)
tier('Tapered square pillar',.59,.86,.40,.35,.76,stone,4)
box('Lectern capital',(0,0,1.43),(.62,.47,.13),edge)

# Book plane rises toward its back. Each page has a shallow raised outer edge.
def page_z(x,y):
    return 1.62 + .62*y + .10*abs(x)

def leaf(name, sign, width, depth, offset, thick, mat):
    coords=[(.027,-depth/2),(width,-depth/2),(width,depth/2),(.027,depth/2)]
    if sign<0:
        coords=[(-x,y) for x,y in reversed(coords)]
    vs=[(x,y,page_z(x,y)+offset+dz) for dz in [-thick,0] for x,y in coords]
    return mesh(name,vs,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)

for sign,label in [(-1,'Left'),(1,'Right')]:
    leaf(label+' thick cover',sign,.89,1.02,0,.07,cover)
    leaf(label+' cover lip',sign,.865,.99,.013,.018,edge)
    for i in range(4):
        leaf(label+f' page layer {i+1}',sign,.815-i*.003,.93-i*.004,.033+i*.012,.010,layer if i%2==0 else paper)
    leaf(label+' blank page',sign,.80,.91,.087,.016,paper)

# Narrow recessed spine, sloped with the page plane.
vs=[(x,y,page_z(0,y)+.025+dz) for dz in [-.06,0] for x,y in [(-.028,-.505),(.028,-.505),(.028,.505),(-.028,.505)]]
mesh('Central book spine',vs,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],cover)

# Raised faceted oval ornament on the front of the pillar (front = -Y).
def jewel(name, radius_x,radius_z,back_y,front_y,mat):
    vs=[]
    for scale,y in [(1,back_y),(.77,front_y)]:
        for i in range(8):
            a=math.tau*i/8+math.pi/8
            vs.append((math.cos(a)*radius_x*scale,y,.93+math.sin(a)*radius_z*scale))
    fs=[tuple(reversed(range(8))),tuple(range(8,16))]
    fs += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    return mesh(name,vs,fs,mat)
jewel('Octagonal ornament frame',.235,.305,-.21,-.295,rim)
jewel('Faceted purple ornament',.199,.262,-.298,-.35,purple)

# Recalculate normals and unwrap all parts for future texture editing.
bpy.ops.object.select_all(action='DESELECT')
for ob in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.uv.smart_project(island_margin=.025)
bpy.ops.object.mode_set(mode='OBJECT')
root=bpy.data.objects.new('Book_Statue',None)
bpy.context.collection.objects.link(root)
for ob in parts:
    ob.parent=root
root.select_set(True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
bpy.ops.export_scene.fbx(filepath=str(OUT/'Book_Statue.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
bpy.ops.export_scene.gltf(filepath=str(SRC/'Book_Statue.glb'),use_selection=True,export_format='GLB')

# Presentation scene is kept separate from the exported game asset.
floor=box('Preview ground',(0,0,-.06),(200,200,.10),material('Preview ground material',(.065,.075,.084)))
parts.remove(floor)
def aim(ob,target):
    ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.5,-5.4,3.5))
camera=bpy.context.object
camera.name='Preview camera'
aim(camera,(0,0,1.00))
camera.data.type='ORTHO'
camera.data.ortho_scale=2.85
scene.camera=camera
for name,loc,power,size in [('Key',(-3,-4,6),650,4),('Fill',(4,-1,4),400,3),('Rim',(1,4,5),850,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    light=bpy.context.object
    light.name=name
    light.data.energy=power
    light.data.shape='DISK'
    light.data.size=size
    aim(light,(0,0,1))
scene.world.color=(.25,.25,.25)
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.render.resolution_x=1000
scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Book_Statue.blend'))
scene.render.filepath=str(SRC/'Book_Statue_Preview.png')
bpy.ops.render.render(write_still=True)
triangles=0
for ob in parts:
    ob.data.calc_loop_triangles()
    triangles+=len(ob.data.loop_triangles)
(SRC/'README.txt').write_text('Book statue reconstructed from the supplied screenshot.\nFiles: editable Blender source, GLB with materials, and Unity FBX in Assets/Art/BookStatue.\nUnity orientation: Y up; base center at (0,0,0). Approximate width 1.78 m and height 2.05 m.\nBlank stone-gray pages, dark pedestal and muted purple ornament. No gameplay scripts or colliders included.\nPreview floor, lights and camera are excluded from FBX and GLB.\nTriangles: '+str(triangles)+'\n',encoding='utf-8')
print(json.dumps({'triangles':triangles,'mesh_parts':len(parts),'fbx':str(OUT/'Book_Statue.fbx'),'blend':str(SRC/'Book_Statue.blend')}))

