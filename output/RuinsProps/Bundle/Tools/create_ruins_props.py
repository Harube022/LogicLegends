"""Pale stylized ruin props inspired by the supplied circular temple map."""
import bpy,bmesh,math,random,json
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/Art/RuinsProps';SRC=ROOT/'SourceAssets/RuinsProps'
for p in [OUT/'Textures',OUT/'Editor',SRC]:p.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0;random.seed(131026)
scene=bpy.context.scene;scene.unit_settings.system='METRIC'
def surface(name,size,moss=False):
    image=bpy.data.images.new(name,size,size,alpha=False);px=[]
    for y in range(size):
        for x in range(size):
            fine=random.random();wave=math.sin(x*.045+math.sin(y*.042)*1.7)*math.sin(y*.048)
            if moss:
                c=(.09+.035*fine+.015*wave,.16+.045*fine+.025*wave,.037+.023*fine)
            else:
                value=.48+.028*wave+.022*(fine-.5);value-=.065 if fine<.018 else 0
                c=(value*1.01,value*1.025,value*.99)
            px.extend((*c,1))
    image.pixels=px;image.filepath_raw=str(OUT/'Textures'/f'{name}.png');image.file_format='PNG';image.save();image.pack();return image
stone=surface('Ruins_Stone_BaseColor',512);moss=surface('Ruins_Moss_BaseColor',256,True)
mats=[]
for name,img,color in [('Ruins_Stone',stone,(1,1,1,1)),('Ruins_Moss',moss,(1,1,1,1)),('Ruins_Crevice',stone,(.20,.22,.20,1))]:
    m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.87
    t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=img
    if name=='Ruins_Crevice':
        mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=color
        m.node_tree.links.new(t.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],p.inputs['Base Color'])
    else:m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
    mats.append(m)
class Geo:
    def __init__(self):self.v=[];self.f=[];self.m=[]
    def add(self,v,f,mat=0):
        offset=len(self.v);self.v.extend(Vector(p) for p in v);self.f.extend(tuple(offset+i for i in face) for face in f);self.m.extend([mat]*len(f))
    def box(self,p,s,mat=0,ch=.04,rot=None):
        x,y,z=(t/2 for t in s);rot=rot or Matrix.Identity(3);p=Vector(p);b=min(ch,x*.35,y*.35,z*.4)
        v=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z-b),(x,-y,z-b),(x,y,z-b),(-x,y,z-b),(-x+b,-y+b,z),(x-b,-y+b,z),(x-b,y-b,z),(-x+b,y-b,z)]
        f=[(3,2,1,0),(8,9,10,11)]
        for i in range(4):j=(i+1)%4;f.extend([(i,j,j+4,i+4),(i+4,j+4,j+8,i+8)])
        self.add([p+rot@Vector(q) for q in v],f,mat)
    def rock(self,p,s,mat=0,n=7):
        x,y,z=p;rx,ry,h=s;v=[];phase=random.random()
        for zz,r in [(z,.85),(z+h*.72,1),(z+h,.68)]:
            for i in range(n):
                a=i*math.tau/n+phase;rr=r*random.uniform(.85,1.12);v.append((x+rx*rr*math.cos(a),y+ry*rr*math.sin(a),zz+random.uniform(-.025,.025)))
        f=[tuple(reversed(range(n))),tuple(range(2*n,3*n))]
        for j in range(2):f.extend((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for i in range(n))
        self.add(v,f,mat)
    def column(self,p,r,h,broken=False,fluted=False,n=24,rot=None):
        p=Vector(p);rot=rot or Matrix.Identity(3);v=[]
        for j in [0,1]:
            for i in range(n):
                a=i*math.tau/n;rr=r*(1+.085*(1 if i%2 else -1)) if fluted else r
                zz=j*h+(random.uniform(-.13,.11) if j and broken else 0)
                v.append(p+rot@Vector((rr*math.cos(a),rr*math.sin(a),zz)))
        self.add(v,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])
    def moss(self,p,rx,ry):self.rock(p,(rx,ry,.012),1,9)
def make(name,g):
    low=min(v.z for v in g.v);cx=(min(v.x for v in g.v)+max(v.x for v in g.v))/2;cy=(min(v.y for v in g.v)+max(v.y for v in g.v))/2
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata([(v.x-cx,v.y-cy,v.z-low) for v in g.v],[],g.f);me.update()
    for m in mats:me.materials.append(m)
    for poly,idx in zip(me.polygons,g.m):poly.material_index=idx
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT');return ob
props=[]
# Broken masonry walls taper down to scattered, chipped blocks.
for variant,heights in [('BrokenWall_A',[4,4,3,2,1]),('BrokenWall_B',[1,2,3,2])]:
    g=Geo()
    for col,height in enumerate(heights):
        for row in range(height):
            x=(col-(len(heights)-1)/2)*.54+(row%2)*.05;z=.13+row*.25
            g.box((x,0,z),(.51,.40,.235),ch=.035)
        if height>1:g.rock(((col-(len(heights)-1)/2)*.54,0,.25*height),(.25,.19,.12))
    g.rock((1.15,-.45,0),(.31,.22,.13));g.rock((-.65,.37,0),(.25,.18,.11))
    for x in [-.7,.1,.6]:g.moss((x,-.225,.09),.15,.07)
    props.append(make(variant,g))
# Classical column stump with jagged top and stepped round plinth.
g=Geo();g.column((0,0,0),.43,.10,n=16);g.column((0,0,.10),.36,.12,n=16);g.column((0,0,.22),.28,1.14,True,True)
g.rock((.5,-.1,0),(.25,.19,.15));g.moss((-.12,-.2,.228),.15,.07);props.append(make('BrokenColumn_Stump',g))
# Three fracture-separated fluted shaft sections lying on the ground.
g=Geo();rot=Matrix.Rotation(math.pi/2,3,'Y')
for x,h in [(-1.25,.62),(-.58,.77),(.24,.68)]:g.column((x,0,.31),.29,h,True,True,24,rot)
g.rock((1.18,.23,0),(.27,.30,.12));g.moss((-.38,0,.56),.18,.08);props.append(make('FallenColumn',g))
# Detached curved arch stones, with one low support stump.
g=Geo();g.column((-.75,0,0),.34,.12,n=16);g.column((-.75,0,.12),.24,.60,True,True)
for i in range(7):
    a=math.radians(12+i*13);b=a+math.radians(11);r0=.95;r1=1.29
    v=[(math.cos(t)*r,math.sin(t)*r,z) for z in [0,.35] for r,t in [(r0,a),(r1,a),(r1,b),(r0,b)]]
    g.add(v,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
g.rock((-.7,-.48,0),(.27,.24,.17));props.append(make('CollapsedArch_Fragment',g))
# A short stair remnant and a cracked paving fragment.
g=Geo()
for i in range(3):g.box((0,i*.31,.09+i*.13),(1.40,.90-i*.17,.18+i*.26),ch=.05)
g.rock((.67,-.4,0),(.20,.23,.12));g.moss((-.35,-.29,.182),.21,.09);props.append(make('WornSteps',g))
g=Geo();g.rock((0,0,0),(.72,.60,.14),n=8)
for p,s,a in [((-.2,-.12,.145),(.48,.018,.009),.40),((.14,.04,.145),(.44,.018,.009),-.50)]:g.box(p,s,2,.001,Matrix.Rotation(a,3,'Z'))
g.moss((-.30,.25,.14),.19,.12);props.append(make('CrackedPaving_Slab',g))
for name,count,size in [('RubbleCluster_A',8,1),('RubbleCluster_B',5,.75)]:
    g=Geo()
    for i in range(count):
        x=random.uniform(-.65,.65)*size;y=random.uniform(-.5,.5)*size;r=random.uniform(.14,.29)*size
        g.rock((x,y,0),(r,r*random.uniform(.7,1.1),random.uniform(.12,.30)*size))
    g.moss((-.20,-.20,.10),.14,.08);props.append(make(name,g))

def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
def export(name,obs):
    select(obs);bpy.ops.export_scene.fbx(filepath=str(OUT/name),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,bake_anim=False,path_mode='COPY',embed_textures=True)
for ob in props:export(ob.name+'.fbx',[ob])
root=bpy.data.objects.new('Ruins_Props_Perimeter',None);bpy.context.collection.objects.link(root);pieces=[]
layout=[(0,-3.6,5.3,.12),(1,3.6,5.3,-.18),(3,-5.4,2.8,.9),(4,5.4,3.6,1.0),(2,-5.4,-2.8,0),(2,5.5,-3.3,0),(5,0,-5.6,0),(6,-3.4,-5.4,-.25),(7,-5.5,0,.2),(8,5.6,0,-.2),(7,0,5.7,0),(8,3.4,-5.5,0)]
for i,(idx,x,y,a) in enumerate(layout):
    source=props[idx];me=source.data.copy();me.transform(Matrix.Rotation(a,4,'Z'));ob=bpy.data.objects.new(f'{i+1:02d}_{source.name}',me);bpy.context.collection.objects.link(ob);ob.parent=root;ob.location=(x,y,0);pieces.append(ob)
export('Ruins_Props_Perimeter.fbx',[root]+pieces)
for i,ob in enumerate(props):ob.location=((i%3)*3.3-3.3,(i//3)*2.7-2.7,0);ob.hide_render=True;ob.hide_set(True)
# Save editable source with a clear material viewport, framed on the perimeter.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL';area.spaces.active.region_3d.view_distance=17;area.spaces.active.region_3d.view_location=Vector((0,0,.5))
select([root]+pieces);bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Ruins_Props.blend'))
def stats(obs):
    tris=0;zero=0
    for o in obs:
        if o.type=='MESH':o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);zero+=sum(p.area<1e-10 for p in o.data.polygons)
    return dict(triangles=tris,zero_area_faces=zero,mesh_objects=sum(o.type=='MESH' for o in obs))
before=stats(pieces);old=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(OUT/'Ruins_Props_Perimeter.fbx'));back=list(set(scene.objects)-old);assert stats(back)==before
for o in back:bpy.data.objects.remove(o,do_unlink=True)
assert before['zero_area_faces']==0
report=dict(**before,fbx_roundtrip='PASS',unique_props=len(props),perimeter_instances=len(pieces),layout_center_clear_m_approx=[8,8],map_mesh_included=False)
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2));print('RUINS_PROPS_GEOMETRY_PASS '+json.dumps(report))
# Render only the props, with a presentation floor excluded from FBX/source.
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.world.color=(.15,.17,.21)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.02));floor=bpy.context.object;mat=bpy.data.materials.new('PreviewFloor');mat.diffuse_color=(.07,.09,.11,1);floor.data.materials.append(mat)
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for pos,power,size in [((-6,-7,12),2600,8),((6,2,10),1600,7)]:
    bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=power;l.data.size=size;aim(l,(0,0,.5))
bpy.ops.object.camera_add(location=(12,-15,14));camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=18;aim(camera,(0,0,.4))
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.filepath=str(SRC/'Ruins_Perimeter_Preview.png');bpy.ops.render.render(write_still=True)
for ob in pieces:ob.hide_render=True
for ob in props:ob.hide_render=False
camera.data.ortho_scale=12;camera.location=(10,-13,14);aim(camera,(0,0,.5));scene.render.filepath=str(SRC/'Ruins_Kit_Preview.png');bpy.ops.render.render(write_still=True)
print('RUINS_PROPS_BUILD_COMPLETE')
