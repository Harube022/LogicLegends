"""Generate reference-guided quad-based flower bush using Blender 4.5."""
import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/LogicGardenFlowerBush'
SRC=ROOT/'SourceAssets/LogicGardenFlowerBush'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
random.seed(8317)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
root=bpy.data.objects.new('LogicGarden_FlowerBush',None)
scene.collection.objects.link(root)
parts={}
for name,color in [('Stems',(.13,.21,.035,1)),('Leaves',(.28,.42,.06,1)),('Petals',(.94,.88,.73,1)),('Centers',(1,.60,.035,1)),('Buds',(.88,.65,.60,1))]:
    mat=bpy.data.materials.new('FlowerBush_'+name)
    mat.diffuse_color=color;mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=color;shader.inputs['Roughness'].default_value=.85
    parts[name]={'v':[],'f':[],'groups':[],'material':mat}

def add(part,vertices,faces,label):
    p=parts[part];start=len(p['v'])
    p['v'].extend([tuple(v) for v in vertices]);p['f'].extend([tuple(start+i for i in f) for f in faces])
    p['groups'].append((label,list(range(start,len(p['v'])))))

def basis(n):
    n=Vector(n).normalized();u=n.cross(Vector((0,0,1)))
    if u.length<.01:u=n.cross(Vector((0,1,0)))
    u.normalize();return u,n.cross(u).normalized(),n

def stem(points,radius,label):
    points=[Vector(p) for p in points];v=[];f=[]
    for j,p in enumerate(points):
        direction=points[min(j+1,len(points)-1)]-points[max(0,j-1)]
        u,w,n=basis(direction)
        r=radius*(1-.55*j/(len(points)-1))
        for k in range(4):v.append(p+r*(u*math.cos(math.tau*k/4)+w*math.sin(math.tau*k/4)))
    for j in range(len(points)-1):
        for k in range(4):f.append((4*j+k,4*j+(k+1)%4,4*(j+1)+(k+1)%4,4*(j+1)+k))
    f.extend([(3,2,1,0),tuple(range(len(v)-4,len(v)))])
    add('Stems',v,f,label)

def leaf(base,direction,length,width,label):
    d=Vector(direction).normalized();w=Vector((0,0,1)).cross(d).normalized();n=d.cross(w).normalized()
    roll=.72*math.sin(Vector(base).x*17+Vector(base).z*23)
    w=w*math.cos(roll)+n*math.sin(roll);n=d.cross(w).normalized()
    # Two quads per side share a central fold: pointed outline, no triangle planes.
    if length>.1:length*=1.15;width*=1.25
    v=[]
    for back in [False,True]:
        for s,x,h in [(0,0,0),(.48,-.5,.008),(1,0,-.035),(.48,.5,.008),(.48,0,.040)]:
            v.append(Vector(base)+d*(s*length)+w*(x*width)+n*(h-(.006 if back else 0)))
    f=[(0,1,2,4),(0,4,2,3),(5,9,7,6),(5,8,7,9)]
    rim=[0,1,2,3]
    for k,a in enumerate(rim):
        b=rim[(k+1)%4];f.append((a,b,b+5,a+5))
    add('Leaves',v,f,label)

def petal(center,d,w,n,length,width,label):
    # Two broad quads per visible petal surface; closed thin edges.
    v=[]
    for back in [False,True]:
        for s,half,h in [(0,.16,0),(.60,.50,.015),(1,.34,.049)]:
            for x in [-1,1]:v.append(center+d*(s*length)+w*(x*width*half)+n*(h-(.006 if back else 0)))
    f=[(0,1,3,2),(2,3,5,4),(6,8,9,7),(8,10,11,9)]
    rim=[0,1,3,5,4,2]
    for k,a in enumerate(rim):
        b=rim[(k+1)%6];f.append((a,b,b+6,a+6))
    add('Petals',v,f,label)

def gem(part,center,n,width,length,label,sides=5):
    u,w,n=basis(n);v=[]
    for z,r in [(0,.40),(.40,.55),(1,.17)]:
        for k in range(sides):
            a=math.tau*k/sides;v.append(Vector(center)+n*(length*z)+width*r*(u*math.cos(a)+w*math.sin(a)))
    f=[]
    for j in range(2):
        for k in range(sides):f.append((j*sides+k,j*sides+(k+1)%sides,(j+1)*sides+(k+1)%sides,(j+1)*sides+k))
    f.extend([tuple(reversed(range(sides))),tuple(range(2*sides,3*sides))])
    add(part,v,f,label)

# Deliberately stagger flowers around the crown, leaving visible stem/leaf gaps.
# Coordinates in metres; front is -Y, side is +X, top is +Z.
flowers=[(-.43,-.10,1.18),(.37,.14,1.43),(-.05,-.39,.98),(.44,-.25,.74),(-.34,-.34,.53),(-.28,.36,.81),(.33,.38,1.02)]
buds=[(-.25,.03,1.49),(.11,.21,1.51),(.57,.05,.99),(.24,-.13,1.16),(-.53,.18,.72),(.16,-.43,.42),(-.09,.46,1.24),(.39,.30,.58)]
endpoints=[Vector(p) for p in flowers+buds]
leaf_index=0
for j,end in enumerate(endpoints):
    a=math.atan2(end.y,end.x)
    start=Vector((.075*math.cos(a+j),.075*math.sin(a+j),0))
    points=[start,start.lerp(end,.36)+Vector((-.04*math.sin(a),.03*math.cos(a),.045)),start.lerp(end,.70)+Vector((-.025*math.sin(a),.02*math.cos(a),.03)),end]
    stem(points,.016 if j<7 else .011,f'Stem_{j:02d}')
    for k,t in enumerate([.29,.47,.64,.79] if j<7 else [.39,.64,.83]):
        anchor=start.lerp(end,t)
        seg=min(2,int(t*3));frac=(t-[0,.36,.70][seg])/([.36,.70,1][seg]-[0,.36,.70][seg])
        anchor=points[seg].lerp(points[seg+1],max(0,min(1,frac)))
        angle=a+(-1 if (k+j)%2 else 1)*random.uniform(.45,1.30)
        direction=Vector((math.cos(angle),math.sin(angle),random.uniform(.35,.95)))
        size=random.uniform(.24,.34)*(1-.20*t)
        if j<7 and k==3:size*=.70
        leaf(anchor,direction,size,random.uniform(.115,.175),f'Leaf_{leaf_index:02d}')
        leaf_index+=1
    n=Vector((end.x*.9,end.y*.9-(0 if j in [5,6] else .55),.65)).normalized()
    if j<len(flowers):
        u,w,n=basis(n);rotation=random.uniform(0,math.tau)
        scale=random.uniform(.90,1.09)
        for k in range(5):
            angle=rotation+math.tau*k/5;d=u*math.cos(angle)+w*math.sin(angle)
            petal(end,d,n.cross(d),n,.145*scale,.119*scale,f'Flower_{j:02d}_Petal_{k}')
        gem('Centers',end+n*.008,n,.072*scale,.044,f'Center_{j:02d}')
    else:
        n=Vector((end.x*.35,end.y*.35,1)).normalized()
        gem('Buds',end,n,.090,.145,f'Bud_{j-7:02d}',sides=4)
        # Small green calyx under each pink bud, reusing the simple leaf form.
        for side in [-1,1]:
            d=Vector((side*.30,side*.15,.9))
            leaf(end-Vector((0,0,.025)),d,.08,.039,f'Calyx_{j}_{side}')

# Three leafy shoots break up the silhouette between the flowers and buds.
for j,end in enumerate([Vector((-.09,.10,1.60)),Vector((-.55,.03,1.21)),Vector((.61,.17,1.17))]):
    start=Vector((0,0,.04));stem([start,end*.48,end*.78,end],.012,f'LeafShoot_{j}')
    for k,t in enumerate([.52,.74]):
        a=j*2.1+k*2.7
        leaf(start.lerp(end,t),Vector((math.cos(a),math.sin(a),.40)),.28,.13,f'ShootLeaf_{j}_{k}')
    leaf(end,Vector((end.x*.4,end.y*.4,1)),.20,.105,f'ShootTip_{j}')

asset=[]
for name,p in parts.items():
    mesh=bpy.data.meshes.new('FlowerBush_'+name);mesh.from_pydata([(x,y,z*.90) for x,y,z in p['v']],[],p['f']);mesh.update()
    ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);ob.parent=root
    mesh.materials.append(p['material']);asset.append(ob)
    for label,indices in p['groups']:ob.vertex_groups.new(name=label).add(indices,1,'REPLACE')
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    # Simple projection UVs for replacement materials; no texture dependency.
    uv=mesh.uv_layers.new(name='UVMap')
    for polygon in mesh.polygons:
        for loop in polygon.loop_indices:
            co=mesh.vertices[mesh.loops[loop].vertex_index].co;uv.data[loop].uv=(co.x+.75,co.z/1.9)

def stats(objects):
    out={'vertices':0,'faces':0,'quads':0,'triangles':0,'boundary_edges':0,'nonmanifold_edges':0,'degenerate_faces':0,'modifiers':0}
    for ob in objects:
        m=ob.data;m.calc_loop_triangles()
        out['vertices']+=len(m.vertices);out['faces']+=len(m.polygons);out['quads']+=sum(len(p.vertices)==4 for p in m.polygons);out['triangles']+=len(m.loop_triangles);out['modifiers']+=len(ob.modifiers)
        bm=bmesh.new();bm.from_mesh(m)
        out['boundary_edges']+=sum(e.is_boundary for e in bm.edges);out['nonmanifold_edges']+=sum(not e.is_manifold for e in bm.edges);out['degenerate_faces']+=sum(f.calc_area()<1e-12 for f in bm.faces);bm.free()
    return out

report=stats(asset)
report.update({'flowers':7,'buds':8,'main_stems':18,'leaves_including_calyxes':len(parts['Leaves']['groups']),'mesh_objects':5,'material_slots':5,'hierarchy':'LogicGarden_FlowerBush > Stems, Leaves, Petals, Centers, Buds','per_part':{ob.name:{'vertices':len(ob.data.vertices),'faces':len(ob.data.polygons),'all_quads':all(len(p.vertices)==4 for p in ob.data.polygons)} for ob in asset}})
assert report['nonmanifold_edges']==0 and report['degenerate_faces']==0
assert report['per_part']['Leaves']['all_quads'] and report['per_part']['Petals']['all_quads']
bpy.ops.object.select_all(action='DESELECT')
for ob in asset+[root]:ob.select_set(True)
bpy.context.view_layer.objects.active=root
scene.tool_settings.transform_pivot_point='MEDIAN_POINT'
bpy.context.preferences.filepaths.save_version=0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=3.6;area.spaces.active.region_3d.view_location=Vector((0,0,.85));area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'LogicGarden_FlowerBush.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'LogicGarden_FlowerBush.fbx'),use_selection=True,object_types={'EMPTY','MESH'},use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True)

# Export verification before producing studio-only preview images.
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LogicGarden_FlowerBush.fbx'))
asset=[o for o in scene.objects if o.type=='MESH'];reimport=stats(asset)
assert len(asset)==5
assert reimport['triangles']==report['triangles'] and reimport['quads']==report['quads']
assert all(o.parent and o.parent.name=='LogicGarden_FlowerBush' for o in asset)
assert all(all(abs(s-1)<.00001 for s in o.scale) for o in asset)
report['fbx_roundtrip']='PASS: hierarchy, quad count, triangle count, unit scale'
report['fbx_reimport']=reimport
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))
print('FLOWER_BUSH_REPORT '+json.dumps(report))

scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.color=(.22,.22,.22)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.01))
ground=bpy.context.object
mat=bpy.data.materials.new('PreviewGround');mat.diffuse_color=(.12,.15,.14,1);ground.data.materials.append(mat)
target=Vector((0,0,.87))
for loc,power,size in [((-3,-4,6),600,4),((4,-1,3),220,3),((1,4,5),450,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=size;lamp.rotation_euler=(target-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=2.1;scene.camera=cam
for name,loc,aim in [('Preview',(2.6,-4.8,2.9),target),('Front',(0,-6,.90),target),('Side',(6,0,.90),target),('Top',(0,0,7),Vector((0,0,0)))]:
    cam.location=loc;cam.rotation_euler=(aim-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(SRC/f'LogicGarden_FlowerBush_{name}.png');bpy.ops.render.render(write_still=True)
