"""Blender 4.5: small reference-guided grass clump, quad leaf planes."""
import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/LogicGardenOrnamentalGrass';SRC=ROOT/'SourceAssets/LogicGardenOrnamentalGrass'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(862)
parts={n:{'v':[],'f':[],'groups':[]} for n in ['Foliage','SeedHeads']}
def add(name,v,f,label):
    p=parts[name];s=len(p['v']);p['v'].extend([tuple(x) for x in v]);p['f'].extend([tuple(s+i for i in face) for face in f]);p['groups'].append((label,list(range(s,len(p['v'])))))
def blade(angle,height,reach,width,index):
    radial=Vector((math.cos(angle),math.sin(angle),0));side=Vector((-math.sin(angle),math.cos(angle),0))
    base=radial*random.uniform(.01,.05)+Vector((0,0,.006))
    # Three quads per side: two gentle bends and a tiny squared-off tip.
    centers=[base,radial*(reach*.28)+Vector((0,0,height*.72)),radial*(reach*.66)+Vector((0,0,height)),radial*reach+Vector((0,0,height*(.55 if reach>.3 else 1.16)))]
    v=[]
    for center,half in zip(centers,[width*.14,width*.50,width*.34,.0007]):
        v.extend([center-side*half,center+side*half])
    f=[(0,1,3,2),(2,3,5,4),(4,5,7,6)]
    normal=(v[1]-v[0]).cross(v[3]-v[0]).normalized()
    v+= [p-normal*.0004 for p in v]
    f+=[(10,11,9,8),(12,13,11,10),(14,15,13,12)]
    add('Foliage',v,f,f'Leaf_{index:02d}')

# Broad drooping outer blades, a middle fan and upright central spears.
index=0
for n,offset,h_range,r_range,w_range in [(9,.1,(.17,.29),(.36,.46),(.065,.10)),(7,.43,(.30,.40),(.22,.35),(.063,.083)),(4,.15,(.43,.52),(.06,.20),(.056,.075))]:
    for i in range(n):
        a=math.tau*i/n+offset+random.uniform(-.17,.17)
        blade(a,random.uniform(*h_range),random.uniform(*r_range),random.uniform(*w_range),index);index+=1

def basis(d):
    d=d.normalized();u=d.cross(Vector((0,0,1)))
    if u.length<.01:u=d.cross(Vector((0,1,0)))
    u.normalize();return u,d.cross(u).normalized()
for j,(a,reach,height) in enumerate([(2.7,.29,.53),(.6,.24,.47),(4.9,.19,.57)]):
    radial=Vector((math.cos(a),math.sin(a),0))
    points=[radial*.025,radial*(reach*.36)+Vector((0,0,height*.59)),radial*reach+Vector((0,0,height))]
    axis=(points[-1]-points[-2]).normalized();v=[];f=[]
    for k,p in enumerate(points):
        d=points[min(k+1,2)]-points[max(k-1,0)];u,w=basis(d)
        for q in range(3):v.append(p+.0032*(u*math.cos(math.tau*q/3)+w*math.sin(math.tau*q/3)))
    for k in range(2):
        for q in range(3):f.append((k*3+q,k*3+(q+1)%3,(k+1)*3+(q+1)%3,(k+1)*3+q))
    f.extend([(2,1,0),(6,7,8)]);add('Foliage',v,f,f'SeedStem_{j}')
    u,w=basis(axis)
    # Three tiny pointed florets per spike, each an eight-triangle octahedron.
    for k in range(3):
        center=points[-1]+axis*(k*.014)+u*((-1 if k%2 else 1)*.004)
        v=[center-axis*.011,center+axis*.018,center+u*.010,center+w*.007,center-u*.010,center-w*.007]
        f=[]
        for q in range(4):
            b=2+q;c=2+(q+1)%4;f.extend([(0,c,b),(1,b,c)])
        add('SeedHeads',v,f,f'Spike_{j}_Floret_{k}')

# Normalize the intended footprint and height directly in geometry.
allv=[v for p in parts.values() for v in p['v']]
mins=[min(v[i] for v in allv) for i in range(3)];maxs=[max(v[i] for v in allv) for i in range(3)]
factor=.9/max(maxs[0]-mins[0],maxs[1]-mins[1]);zfactor=.6/(maxs[2]-mins[2])
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
root=bpy.data.objects.new('LogicGarden_OrnamentalGrass',None);scene.collection.objects.link(root)
objects=[]
for name,color in [('Foliage',(.22,.34,.042,1)),('SeedHeads',(.79,.73,.43,1))]:
    p=parts[name];mesh=bpy.data.meshes.new('OrnamentalGrass_'+name)
    mesh.from_pydata([(x*factor,y*factor,(z-mins[2])*zfactor) for x,y,z in p['v']],[],p['f']);mesh.update()
    ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);ob.parent=root;objects.append(ob)
    for label,ids in p['groups']:ob.vertex_groups.new(name=label).add(ids,1,'REPLACE')
    mat=bpy.data.materials.new('Grass_'+name+'_Placeholder');mat.diffuse_color=color;mat.use_nodes=True;mat.use_backface_culling=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=color;shader.inputs['Roughness'].default_value=.85;mesh.materials.append(mat)
    uv=mesh.uv_layers.new(name='UVMap')
    for p in mesh.polygons:
        for li in p.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(co.x+.5,co.z/.6)
    # Preserve leaf front/back normals; recalculate only closed stems/seed heads.
    bm=bmesh.new();bm.from_mesh(mesh)
    fs=list(bm.faces) if name=='SeedHeads' else [f for f in bm.faces if f.index>=120]
    bmesh.ops.recalc_face_normals(bm,faces=fs);bm.to_mesh(mesh);bm.free()

def measure(obs):
    result={'vertices':0,'faces':0,'quads':0,'triangles':0,'degenerate_faces':0,'loose_vertices':0}
    for ob in obs:
        m=ob.data;m.calc_loop_triangles();result['vertices']+=len(m.vertices);result['faces']+=len(m.polygons);result['quads']+=sum(len(f.vertices)==4 for f in m.polygons);result['triangles']+=len(m.loop_triangles)
        bm=bmesh.new();bm.from_mesh(m);result['degenerate_faces']+=sum(f.calc_area()<1e-12 for f in bm.faces);result['loose_vertices']+=sum(not v.link_faces for v in bm.verts);bm.free()
    return result
stats=measure(objects);stats.update({'leaf_count':20,'seed_spikes':3,'florets':9,'leaf_faces_all_quads':True,'mesh_objects':2,'materials':2,'modifiers':0,'dimensions_m':[factor*(maxs[0]-mins[0]),factor*(maxs[1]-mins[1]),.6],'pivot':'ground level','scale':[1,1,1]})
assert all(len(p.vertices)==4 for p in objects[0].data.polygons[:120])
assert stats['degenerate_faces']==0 and stats['loose_vertices']==0
bpy.ops.object.select_all(action='DESELECT')
for ob in objects+[root]:ob.select_set(True)
bpy.context.view_layer.objects.active=root
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_distance=1.7;area.spaces.active.region_3d.view_location=Vector((0,0,.27));area.spaces.active.shading.color_type='MATERIAL'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'LogicGarden_OrnamentalGrass.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'LogicGarden_OrnamentalGrass.fbx'),use_selection=True,object_types={'EMPTY','MESH'},use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LogicGarden_OrnamentalGrass.fbx'))
objects=[ob for ob in scene.objects if ob.type=='MESH'];importstats=measure(objects)
assert len(objects)==2 and importstats['triangles']==stats['triangles'] and importstats['quads']==stats['quads']
assert all(ob.parent and ob.parent.name=='LogicGarden_OrnamentalGrass' for ob in objects)
assert all(all(abs(s-1)<1e-5 for s in ob.scale) and len(ob.modifiers)==0 for ob in objects)
stats['fbx_roundtrip']='PASS: hierarchy, quads, triangles, unit scale';stats['fbx_reimport']=importstats
(SRC/'geometry-report.json').write_text(json.dumps(stats,indent=2));print('GRASS_REPORT '+json.dumps(stats))

# Render the re-imported FBX; studio objects are excluded from saved assets.
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.world.color=(.20,.20,.20)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003));ground=bpy.context.object
gm=bpy.data.materials.new('PreviewGround');gm.diffuse_color=(.095,.105,.09,1);ground.data.materials.append(gm)
target=Vector((0,0,.26))
for loc,power,size in [((-2,-3,4),330,3),((3,-1,2),100,2),((1,3,4),260,2)]:
    bpy.ops.object.light_add(type='AREA',location=loc);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=size;lamp.rotation_euler=(target-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=1.08;scene.camera=cam
for name,loc,aim in [('Preview',(1.4,-2.6,1.2),target),('Front',(0,-4,.50),target),('Side',(4,0,.5),target),('Top',(0,0,4),Vector((0,0,0)))]:
    cam.location=loc;cam.rotation_euler=(aim-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(SRC/f'LogicGarden_OrnamentalGrass_{name}.png');bpy.ops.render.render(write_still=True)
