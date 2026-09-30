"""Blender 4.5 background generator for a reference-guided quad-leaf bush."""
import bpy, bmesh, math, random, json
from mathutils import Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/LogicGardenQuadBush'
SRC=ROOT/'SourceAssets/LogicGardenQuadBush'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(314159)
verts=[];faces=[];uvcoords=[];groups=[]

def leaf(center,direction,normal,length,width):
    d=direction.normalized();n=(normal-d*normal.dot(d)).normalized();w=d.cross(n).normalized()
    # A hexagonal pointed outline divided along the midrib into TWO QUADS.
    # Broad shoulders, tapered tip, shallow fold, no subdivided grid or triangles.
    outline=[(0,0,.010),(.29,-.44,-.018),(.66,-.48,-.023),(1,0,-.025),(.66,.48,-.023),(.29,.44,-.018)]
    pts=[center+d*(s*length)+w*(x*width)+n*h for s,x,h in outline]
    front=[(0,1,2,3),(0,3,4,5)]
    # Ensure front normal points outward, independent of placement.
    if (pts[1]-pts[0]).cross(pts[2]-pts[0]).dot(n)<0:front=[tuple(reversed(f)) for f in front]
    start=len(verts)
    # Separate back vertices keep opposite smooth normals independent.
    # Backface culling selects one surface, so this needs no two-sided shader.
    verts.extend([tuple(p) for p in pts]+[tuple(p-n*.0008) for p in pts])
    faces.extend([tuple(start+i for i in f) for f in front])
    faces.extend([tuple(start+6+i for i in reversed(f)) for f in front])
    uvcoords.extend([(x+.5,s) for s,x,h in outline]*2)
    groups.append((f'Leaf_{len(groups):03d}',list(range(start,start+12))))

for row,(theta,num) in enumerate(zip([.12,.40,.70,1.00,1.30,1.60,1.91,2.20,2.48],[6,12,20,26,30,28,24,18,10])):
    for k in range(num):
        a=math.tau*(k+.48*(row%2))/num+random.uniform(-.065,.065)
        t=theta+random.uniform(-.065,.065)
        radial=Vector((math.cos(a),math.sin(a),0));side=Vector((-math.sin(a),math.cos(a),0))
        normal=radial*math.sin(t)+Vector((0,0,math.cos(t)))
        down=radial*math.cos(t)+Vector((0,0,-math.sin(t)))
        center=radial*(.625*math.sin(t))+Vector((0,0,.73+.53*math.cos(t)))
        direction=down+normal*random.uniform(.08,.30)+side*random.uniform(-.26,.26)
        length=random.uniform(.33,.43) if row<2 else random.uniform(.37,.47)
        leaf(center,direction,normal,length,random.uniform(.27,.33))
for a in [.2,2.3,4.4]:
    radial=Vector((math.cos(a),math.sin(a),0))
    leaf(-radial*.11+Vector((0,0,1.275)),radial,Vector((0,0,1)),.32,.26)
for a in [.5,2.6,4.7]:
    radial=Vector((math.cos(a),math.sin(a),0))
    leaf(radial*.22+Vector((0,0,1.21)),radial*.35+Vector((0,0,1)),radial,.22,.17)
minz=min(z for x,y,z in verts)
verts=[(x,y,z-minz) for x,y,z in verts]
mesh=bpy.data.meshes.new('LogicGarden_QuadBush_Mesh');mesh.from_pydata(verts,[],faces);mesh.update()
ob=bpy.data.objects.new('LogicGarden_QuadBush',mesh);bpy.context.collection.objects.link(ob)
ob.select_set(True);bpy.context.view_layer.objects.active=ob
for name,indices in groups:ob.vertex_groups.new(name=name).add(indices,1,'REPLACE')
uv=mesh.uv_layers.new(name='UVMap')
for p in mesh.polygons:
    p.use_smooth=True
    for li in p.loop_indices:uv.data[li].uv=uvcoords[mesh.loops[li].vertex_index]
mat=bpy.data.materials.new('QuadBush_Placeholder');mat.diffuse_color=(.24,.48,.045,1);mat.use_nodes=True;mat.use_backface_culling=True
shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=mat.diffuse_color;shader.inputs['Roughness'].default_value=.85
mesh.materials.append(mat)
ob['leaf_count']=len(groups)
ob['topology']='Two quads per leaf front plus two reverse quads offset 0.8 mm; no core or edge walls.'
ob['material_note']='Use ordinary opaque material with backface culling. Reverse faces supply leaf backs.'
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.context.view_layer.update();mesh.calc_loop_triangles()
stats={'vertices':len(mesh.vertices),'faces':len(mesh.polygons),'quads':sum(len(p.vertices)==4 for p in mesh.polygons),'triangles':len(mesh.loop_triangles),'leaves':len(groups),'mesh_objects':1,'materials':1,'modifiers':0,'dimensions_m':list(ob.dimensions),'scale':list(ob.scale),'pivot':list(ob.location),'core_geometry':False,'back_faces_included':True}
assert stats['faces']==stats['quads']
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=3.3;area.spaces.active.region_3d.view_location=Vector((0,0,.65));area.spaces.active.shading.color_type='MATERIAL'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'LogicGarden_QuadBush.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'LogicGarden_QuadBush.fbx'),use_selection=True,object_types={'MESH'},use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True)

bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LogicGarden_QuadBush.fbx'))
objects=[o for o in scene.objects if o.type=='MESH'];assert len(objects)==1
ob=objects[0];m=ob.data;m.calc_loop_triangles()
assert len(m.loop_triangles)==stats['triangles'] and len(m.polygons)==stats['quads']
assert all(len(p.vertices)==4 for p in m.polygons)
assert len(m.materials)==1 and len(ob.modifiers)==0
assert all(abs(s-1)<1e-5 for s in ob.scale)
bm=bmesh.new();bm.from_mesh(m)
stats['degenerate_faces']=sum(f.calc_area()<1e-10 for f in bm.faces)
stats['loose_vertices']=sum(not v.link_faces for v in bm.verts)
stats['intentional_boundary_edges']=sum(e.is_boundary for e in bm.edges)
assert stats['degenerate_faces']==0 and stats['loose_vertices']==0
bm.free()
stats['fbx_reimport_vertices']=len(m.vertices);stats['fbx_roundtrip']='PASS: one mesh, one material, quads preserved, unit scale, no modifiers'
(SRC/'geometry-report.json').write_text(json.dumps(stats,indent=2));print('QUAD_BUSH_REPORT '+json.dumps(stats))

# Studio objects are only for previews, never saved into the model files.
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world.color=(.23,.23,.23)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012));ground=bpy.context.object
gm=bpy.data.materials.new('PreviewGround');gm.diffuse_color=(.11,.13,.12,1);ground.data.materials.append(gm)
target=Vector((0,0,.64))
for loc,power,size in [((-3,-4,6),650,4),((4,-1,3),220,3),((1,4,5),450,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=size;lamp.rotation_euler=(target-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=2.25;scene.camera=cam
for name,loc,aim in [('Preview',(3,-5,2.8),target),('Top',(0,0,6),Vector((0,0,0)))]:
    cam.location=loc;cam.rotation_euler=(aim-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(SRC/f'LogicGarden_QuadBush_{name}.png');bpy.ops.render.render(write_still=True)
