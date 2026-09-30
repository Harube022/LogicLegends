"""Run with Blender --background --python Tools/create_logic_garden_bush.py."""
import bpy
import math
import random
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Art/LogicGardenBush'
SOURCE = ROOT / 'SourceAssets/LogicGardenBush'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
random.seed(314159)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
verts, faces, uvfaces = [], [], []
groups = []

def add_geometry(points, polygons, uvs=None):
    start = len(verts)
    verts.extend([tuple(p) for p in points])
    for polygon in polygons:
        faces.append(tuple(start + k for k in polygon))
        uvfaces.append([uvs[k] if uvs else (0.5, 0.5) for k in polygon])
    return list(range(start, len(verts)))

# Closed, very low resolution core hides gaps without transparent materials.
core = [Vector((0, 0, 1.29))]
for j in range(1, 7):
    t = math.pi * j / 7
    for i in range(12):
        a = i * math.tau / 12
        core.append(Vector((.595 * math.sin(t) * math.cos(a), .595 * math.sin(t) * math.sin(a), .72 + .57 * math.cos(t))))
core.append(Vector((0, 0, .15)))
polys = []
for i in range(12):
    polys.append((0, 1+i, 1+(i+1)%12))
for j in range(5):
    for i in range(12):
        a=1+j*12+i; b=1+j*12+(i+1)%12; c=b+12; d=a+12
        polys.extend([(a,d,c),(a,c,b)])
for i in range(12):
    polys.append((73,61+(i+1)%12,61+i))
# Fix all closed components to outward normals below.
groups.append(('Core', add_geometry(core, polys)))

def leaf(center, direction, normal, length, width, name):
    d = direction.normalized()
    n = (normal - d * normal.dot(d)).normalized()
    w = d.cross(n).normalized()
    # Six-point outline, with a subtle raised central crease on both sides.
    outline = [(0,0,0),(.28,-.44,.012),(.63,-.48,.004),(1,0,-.035),(.63,.48,.004),(.28,.44,.012)]
    points = [center+d*(s*length)+w*(x*width)+n*h for s,x,h in outline]
    points += [center+d*(.47*length)+n*.048, center+d*(.47*length)+n*.014]
    polygons = []
    for k in range(6):
        polygons.extend([(6,k,(k+1)%6),(7,(k+1)%6,k)])
    uv=[(x+.5,s) for s,x,h in outline]+[(.5,.47),(.5,.47)]
    groups.append((name,add_geometry(points,polygons,uv)))

count=0
for row,(theta,num) in enumerate(zip([.12,.40,.70,1.00,1.30,1.60,1.91,2.20],[6,12,20,26,30,28,24,16])):
    for k in range(num):
        a=math.tau*(k+.48*(row%2))/num+random.uniform(-.055,.055)
        t=theta+random.uniform(-.065,.065)
        radial=Vector((math.cos(a),math.sin(a),0))
        side=Vector((-math.sin(a),math.cos(a),0))
        normal=radial*math.sin(t)+Vector((0,0,math.cos(t)))
        down=radial*math.cos(t)+Vector((0,0,-math.sin(t)))
        center=radial*(.625*math.sin(t))+Vector((0,0,.73+.60*math.cos(t)))
        direction=down+normal*random.uniform(.08,.30)+side*random.uniform(-.25,.25)
        length=random.uniform(.33,.43) if row<2 else random.uniform(.37,.48)
        leaf(center,direction,normal,length,random.uniform(.235,.30),f'Leaf_{count:03d}')
        count+=1
# A few upright shoots echo the reference's broken crown silhouette.
for a in [.5,2.6,4.7]:
    radial=Vector((math.cos(a),math.sin(a),0))
    leaf(radial*.22+Vector((0,0,1.28)),radial*.35+Vector((0,0,1)),radial,.24,.16,f'Leaf_{count:03d}')
    count+=1

minz=min(v[2] for v in verts)
verts=[(x,y,z-minz) for x,y,z in verts]
mesh=bpy.data.meshes.new('LogicGarden_Bush_Mesh')
mesh.from_pydata(verts,[],faces)
mesh.update()
obj=bpy.data.objects.new('LogicGarden_Bush',mesh)
bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active=obj
obj.select_set(True)
uv=mesh.uv_layers.new(name='UVMap')
for p,coords in zip(mesh.polygons,uvfaces):
    for loop,co in zip(p.loop_indices,coords):
        uv.data[loop].uv=co
for name,indices in groups:
    obj.vertex_groups.new(name=name).add(indices,1,'REPLACE')
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
for p in mesh.polygons:
    p.use_smooth=True
mat=bpy.data.materials.new('Bush_Placeholder')
mat.diffuse_color=(.22,.43,.045,1)
mat.use_nodes=True
bsdf=mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value=mat.diffuse_color
bsdf.inputs['Roughness'].default_value=.85
mesh.materials.append(mat)
obj['leaf_count']=count
obj['notes']='Closed low-poly leaves; one material; no modifiers; ground pivot; leaf vertex groups for editing.'
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
mesh.calc_loop_triangles()
stats={'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'faces':len(mesh.polygons),'leaves':count,'materials':1,'modifiers':0,'dimensions_m':list(obj.dimensions),'scale':list(obj.scale),'origin':list(obj.location)}
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'LogicGarden_Bush.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'LogicGarden_Bush.fbx'),use_selection=True,object_types={'MESH'},use_mesh_modifiers=False,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True)

# Render a disposable studio scene; it is not part of the saved asset or FBX.
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=900
scene.render.resolution_y=900
scene.render.resolution_percentage=100
scene.world.color=(.23,.23,.23)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015))
ground=bpy.context.object
gm=bpy.data.materials.new('PreviewGround')
gm.diffuse_color=(.095,.115,.105,1)
ground.data.materials.append(gm)
target=Vector((0,0,.75))
for loc,power,size in [((-3,-4,6),650,4),((4,-1,3),250,3),((1,4,5),500,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    lamp=bpy.context.object
    lamp.data.energy=power; lamp.data.shape='DISK'; lamp.data.size=size
    lamp.rotation_euler=(target-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(3,-5,3.2))
cam=bpy.context.object
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=2.5
scene.camera=cam
scene.render.filepath=str(SOURCE/'LogicGarden_Bush_Preview.png')
bpy.ops.render.render(write_still=True)

# Round-trip validation of the deliverable, not just the source mesh.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LogicGarden_Bush.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(meshes)==1
imported=meshes[0]
imported.data.calc_loop_triangles()
assert len(imported.data.loop_triangles)==stats['triangles']
assert len(imported.data.materials)==1
assert not imported.modifiers
assert all(abs(s-1)<.0001 for s in imported.scale)
stats['fbx_reimport_vertices']=len(imported.data.vertices)
stats['fbx_reimport_triangles']=len(imported.data.loop_triangles)
stats['fbx_roundtrip']='PASS'
import bmesh
bm=bmesh.new();bm.from_mesh(imported.data)
stats['boundary_edges']=sum(e.is_boundary for e in bm.edges)
stats['nonmanifold_edges']=sum(not e.is_manifold for e in bm.edges)
stats['degenerate_faces']=sum(f.calc_area()<1e-10 for f in bm.faces)
bm.free()
assert stats['nonmanifold_edges']==0 and stats['degenerate_faces']==0
(SOURCE/'geometry-report.json').write_text(json.dumps(stats,indent=2))
print('BUSH_REPORT '+json.dumps(stats))
