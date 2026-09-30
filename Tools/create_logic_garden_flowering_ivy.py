"""Blender 4.5 generator: assembled flowering ivy and eight-piece modular kit."""
import bpy,bmesh,math,random,json
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/Art/LogicGardenFloweringIvy';SRC=ROOT/'SourceAssets/LogicGardenFloweringIvy'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(241)
materials=[]
for name,color in [('Foliage',(.17,.29,.038,1)),('Petals',(.94,.85,.74,1)),('Centers',(.98,.61,.035,1)),('Buds',(.84,.54,.53,1))]:
    m=bpy.data.materials.new('Ivy_'+name);m.diffuse_color=color;m.use_nodes=True;m.use_backface_culling=True
    sh=m.node_tree.nodes.get('Principled BSDF');sh.inputs['Base Color'].default_value=color;sh.inputs['Roughness'].default_value=.88;materials.append(m)

class Geo:
    def __init__(self):self.v=[];self.f=[];self.m=[];self.groups=[]
    def add(self,v,f,mat=0,label='Part'):
        s=len(self.v);self.v.extend([Vector(p) for p in v]);self.f.extend([tuple(s+i for i in face) for face in f]);self.m.extend([mat]*len(f));self.groups.append((label,list(range(s,len(self.v)))))
    def instance(self,other,loc,rotation=None,scale=1,label='Module'):
        rotation=rotation or Matrix.Identity(3);s=len(self.v)
        self.v.extend([Vector(loc)+rotation@(v*scale) for v in other.v]);self.f.extend([tuple(s+i for i in f) for f in other.f]);self.m.extend(other.m);self.groups.append((label,list(range(s,len(self.v)))))

def sheet(g,v,f,mat=0,label='Surface'):
    v=[Vector(p) for p in v];n=(v[f[0][1]]-v[f[0][0]]).cross(v[f[0][2]]-v[f[0][0]]).normalized();k=len(v)
    g.add(v+[p-n*.00035 for p in v],f+[tuple(i+k for i in reversed(face)) for face in f],mat,label)

def leaf(kind):
    g=Geo()
    if kind=='A':
        v=[(0,0,0),(-.035,.033,-.004),(-.04,.076,-.004),(0,.12,0),(.04,.076,-.004),(.035,.033,-.004)];f=[(0,1,2,3),(0,3,4,5)]
    elif kind=='B':v=[(0,0,0),(-.035,.047,-.006),(0,.10,0),(.031,.053,-.006)];f=[(0,1,2,3)]
    else:
        v=[(0,0,0),(-.019,.029,-.006),(-.05,.047,-.004),(-.024,.076,-.006),(0,.14,0),(.027,.076,-.006),(.05,.047,-.004),(.019,.029,-.006),(0,.061,.008)]
        f=[(8,0,1,2),(8,2,3,4),(8,4,5,6),(8,6,7,0)]
    sheet(g,v,f,label='Leaf_'+kind);return g

def stem(points,r=.004):
    g=Geo();points=[Vector(p) for p in points];v=[];f=[]
    for j,p in enumerate(points):
        d=(points[min(j+1,len(points)-1)]-points[max(j-1,0)]).normalized();u=d.cross(Vector((0,0,1)))
        if u.length<.01:u=d.cross(Vector((0,1,0)))
        u.normalize();w=d.cross(u)
        for k in range(4):v.append(p+(u*math.cos(k*math.pi/2)+w*math.sin(k*math.pi/2))*r*(1-.4*j/(len(points)-1)))
    for j in range(len(points)-1):
        for k in range(4):f.append((4*j+k,4*j+(k+1)%4,4*(j+1)+(k+1)%4,4*(j+1)+k))
    f.extend([(3,2,1,0),tuple(range(len(v)-4,len(v)))]);g.add(v,f,label='Vine');return g

def bud():
    g=Geo();v=[]
    for z,r in [(0,.009),(.033,.024),(.077,.002)]:
        for k in range(4):v.append((math.cos(k*math.pi/2)*r,math.sin(k*math.pi/2)*r*.8,z))
    f=[]
    for j in range(2):
        for k in range(4):f.append((4*j+k,4*j+(k+1)%4,4*(j+1)+(k+1)%4,4*(j+1)+k))
    f.extend([(3,2,1,0),(8,9,10,11)]);g.add(v,f,3,'Bud');return g

def flower(side=False):
    g=Geo()
    for k in range(5):
        a=math.tau*k/5;d=Vector((math.cos(a),math.sin(a),0));w=Vector((-math.sin(a),math.cos(a),0));v=[]
        for s,half,z in [(0,.006,0),(.032,.018,.007 if not side else .018),(.052,.013,.014 if not side else .044)]:
            for sign in [-1,1]:v.append(d*s+w*half*sign+Vector((0,0,z)))
        sheet(g,v,[(0,1,3,2),(2,3,5,4)],1,f'Petal_{k}')
    v=[(x*.011,y*.011,z*.008+.010) for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    g.add(v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],2,'YellowCenter');return g

modules={'Leaf_A':leaf('A'),'Leaf_B':leaf('B'),'Leaf_C_Ivy':leaf('C'),'Flower_Open':flower(),'Flower_Side':flower(True),'Flower_Bud':bud(),'Vine_Segment_A':stem([(0,0,0),(.05,.012,0),(.11,.02,.012),(.20,0,.025)],.006),'Vine_Segment_B':stem([(0,0,0),(.04,.018,.005),(.10,.006,.015),(.15,.025,.023)],.005)}
# Branch segment B includes a short fork, using the same economical square tube.
modules['Vine_Segment_B'].instance(stem([(.055,.013,.008),(.081,-.021,.009),(.115,-.039,.014)],.003),Vector((0,0,0)))

def orient(direction,normal):
    y=Vector(direction).normalized();z=(Vector(normal)-y*Vector(normal).dot(y)).normalized();x=y.cross(z);return Matrix((x,y,z)).transposed()
branches=[
 [(-.50,.08,.045),(-.33,.14,.11),(-.10,.06,.06),(.15,.03,.025),(.36,-.03,.00),(.51,-.08,-.055)],
 [(-.46,-.12,.065),(-.28,-.14,.08),(-.08,-.13,.015),(.12,-.15,-.055),(.36,-.12,-.065)],
 [(-.37,-.15,.055),(-.44,-.19,-.08),(-.39,-.23,-.25),(-.34,-.26,-.42)],
 [(-.24,-.15,.035),(-.18,-.24,-.15),(-.07,-.27,-.32),(-.10,-.25,-.56)],
 [(.02,-.16,-.005),(.06,-.24,-.17),(.19,-.27,-.34),(.16,-.28,-.62)],
 [(.32,-.10,-.05),(.34,-.20,-.20),(.30,-.26,-.37),(.36,-.23,-.51)],
 [(.49,-.06,-.04),(.52,-.17,-.17),(.55,-.20,-.32),(.50,-.22,-.46)],
 [(-.31,.13,.08),(-.22,.26,.09),(-.03,.28,.055),(.17,.24,.03)],
 [(-.12,.045,.065),(-.21,.01,.16),(-.37,.02,.19)],
]
assembly=Geo();leafcount=0
for j,path in enumerate(branches):
    assembly.instance(stem(path,.0048 if j<2 else .0034),Vector((0,0,0)),label=f'Branch_{j:02d}')
    pts=[Vector(p) for p in path]
    # Three or four irregular attachments per span; paired leaves on selected nodes.
    for segment in range(len(pts)-1):
        for k in range(3):
            t=(k+.24+random.random()*.35)/3;p=pts[segment].lerp(pts[segment+1],t)
            hanging=j in [2,3,4,5,6]
            normal=Vector((random.uniform(-.18,.18),-1,.25)) if hanging else Vector((0,-.35,1))
            sign=-1 if (k+segment+j)%2 else 1
            direction=Vector((sign*random.uniform(.5,1),random.uniform(-.25,.3),random.uniform(-.9,-.3) if hanging else random.uniform(-.2,.55)))
            typ=random.choices(['Leaf_A','Leaf_B','Leaf_C_Ivy'],[.40,.32,.28])[0]
            assembly.instance(modules[typ],p,orient(direction,normal),random.uniform(.72,1.16),f'Leaf_{leafcount:03d}_{typ}');leafcount+=1
            if (j<2 and k in [0,1]) or (hanging and k==1):
                assembly.instance(modules['Leaf_B'],p,orient(-direction+Vector((0,0,.3)),normal),.82,f'Leaf_{leafcount:03d}_B');leafcount+=1

def connect(position,label):
    p=Vector(position);nearest=None;best=100
    for path in branches:
        for aa,bb in zip(path,path[1:]):
            a,b=Vector(aa),Vector(bb);d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared));q=a+d*t
            if (q-p).length<best:best=(q-p).length;nearest=q
    if best>.004:assembly.instance(stem([nearest,p],.0018),Vector((0,0,0)),label=label)

flower_spots=[(-.41,-.16,.13),(-.28,.015,.18),(-.12,-.15,.10),(.06,.03,.08),(.24,-.14,-.015),(.43,-.07,.005),(-.36,-.23,-.14),(-.08,-.27,-.22),(.17,-.28,-.34),(.52,-.20,-.22),(-.13,.24,.13)]
for j,p in enumerate(flower_spots):
    n=Vector((random.uniform(-.3,.3),-.8,.7 if j<6 else .25)).normalized()
    rot=Vector((0,0,1)).rotation_difference(n).to_matrix()
    assembly.instance(modules['Flower_Side' if j in [1,9,10] else 'Flower_Open'],p,rot,random.uniform(.76,1.05),f'Flower_{j:02d}')
    connect(p,f'FlowerStem_{j:02d}')
for j,p in enumerate([(-.49,-.12,-.05),(-.29,-.2,-.29),(.03,-.17,.05),(.29,-.1,.025),(.37,-.24,-.30),(-.09,-.25,-.44),(-.32,.17,.16)]):
    rot=Vector((0,0,1)).rotation_difference(Vector((.15,-.4,.8)).normalized()).to_matrix();assembly.instance(modules['Flower_Bud'],p,rot,.55,f'Bud_{j:02d}')
    connect(p,f'BudStem_{j:02d}')

# Bake approximate target dimensions into vertices; pivot stays at upper attachment centre.
mins=[min(v[i] for v in assembly.v) for i in range(3)];maxs=[max(v[i] for v in assembly.v) for i in range(3)]
scale=[1.2/(maxs[0]-mins[0]),.6/(maxs[1]-mins[1]),.8/(maxs[2]-mins[2])]
assembly.v=[Vector((v.x*scale[0],v.y*scale[1],v.z*scale[2])) for v in assembly.v]

def make_object(name,g):
    mesh=bpy.data.meshes.new(name+'_Mesh');mesh.from_pydata(g.v,[],g.f);mesh.update();ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
    for m in materials:mesh.materials.append(m)
    for p,mi in zip(mesh.polygons,g.m):p.material_index=mi
    for label,ids in g.groups:ob.vertex_groups.new(name=label).add(ids,1,'REPLACE')
    uv=mesh.uv_layers.new(name='UVMap')
    for p in mesh.polygons:
        for li in p.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(co.x+.6,co.z+.65)
    return ob

def measure(obs):
    out={'vertices':0,'faces':0,'quads':0,'triangles':0,'degenerate_faces':0,'loose_vertices':0}
    for ob in obs:
        m=ob.data;m.calc_loop_triangles();out['vertices']+=len(m.vertices);out['faces']+=len(m.polygons);out['quads']+=sum(len(p.vertices)==4 for p in m.polygons);out['triangles']+=len(m.loop_triangles)
        bm=bmesh.new();bm.from_mesh(m);out['degenerate_faces']+=sum(f.calc_area()<1e-12 for f in bm.faces);out['loose_vertices']+=sum(not v.link_faces for v in bm.verts);bm.free()
    return out

def export(path,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True)

scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1;bpy.context.preferences.filepaths.save_version=0
ob=make_object('LogicGarden_FloweringIvy',assembly);ob.select_set(True);bpy.context.view_layer.objects.active=ob
stats=measure([ob]);stats.update({'leaves':leafcount,'flowers':11,'buds':7,'branches':9,'materials':4,'mesh_objects':1,'modifiers':0,'dimensions_unity_xyz':[1.2,.8,.6],'pivot':'upper attachment centre','source_faces_all_quads':stats['faces']==stats['quads']})
assert stats['faces']==stats['quads'] and stats['degenerate_faces']==0 and stats['loose_vertices']==0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_distance=2;area.spaces.active.region_3d.view_location=Vector((0,-.05,-.18));area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'LogicGarden_FloweringIvy.blend'));export(OUT/'LogicGarden_FloweringIvy.fbx',[ob])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
kit=[]
for j,(name,g) in enumerate(modules.items()):
    o=make_object(name,g);o.location=((j%4)*.30,(j//4)*.28,0);kit.append(o)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Set individual module origins back to each module's starting point.
for j,o in enumerate(kit):
    offset=Vector(((j%4)*.30,(j//4)*.28,0))
    for v in o.data.vertices:v.co-=offset
    o.location=offset
bpy.context.view_layer.objects.active=kit[0]
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'LogicGarden_Ivy_ModularKit.blend'));export(OUT/'LogicGarden_Ivy_ModularKit.fbx',kit)
stats['modular_kit']={o.name:measure([o]) for o in kit}
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LogicGarden_FloweringIvy.fbx'))
obs=[o for o in scene.objects if o.type=='MESH'];back=measure(obs)
assert len(obs)==1 and back['faces']==stats['faces'] and back['triangles']==stats['triangles']
assert all(abs(s-1)<1e-5 for s in obs[0].scale) and not obs[0].modifiers
stats['fbx_roundtrip']='PASS';stats['fbx_reimport']=back
(SRC/'geometry-report.json').write_text(json.dumps(stats,indent=2));print('IVY_REPORT '+json.dumps(stats))

# Preview support only: no stone, lights or cameras enter the exported plant.
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=850;scene.render.resolution_percentage=100;scene.world.color=(.20,.20,.20)
stone=bpy.data.materials.new('PreviewStone');stone.diffuse_color=(.48,.40,.28,1)
for loc,size in [((0,.10,-.395),(1.02,.58,.56)),((0,.08,-.115),(1.08,.58,.07)),((-.30,.10,-.035),(.43,.34,.09))]:
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);rock=bpy.context.object;rock.scale=size;rock.data.materials.append(stone)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.70));floor=bpy.context.object
fm=bpy.data.materials.new('PreviewFloor');fm.diffuse_color=(.10,.12,.10,1);floor.data.materials.append(fm)
target=Vector((0,-.06,-.19))
for loc,power,size in [((-2,-3,4),480,3),((3,-1,2),180,2),((1,3,4),320,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(target-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=1.55;scene.camera=cam
for name,loc,aim in [('Preview',(1.3,-2.7,1.15),target),('Front',(0,-4,-.02),target),('Side',(4,-.2,.45),target),('Top',(0,0,4),Vector((0,0,-.15)))]:
    cam.location=loc;cam.rotation_euler=(aim-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(SRC/f'LogicGarden_FloweringIvy_{name}.png');bpy.ops.render.render(write_still=True)
