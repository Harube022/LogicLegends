"""Blender 4.5: compact violet plant; shared geometry helpers from ivy generator."""
import bpy,bmesh,math,random,json,ast
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1]
# Load only pure geometry helper definitions, never the ivy generation workflow.
helper=ast.parse((ROOT/'Tools/create_logic_garden_flowering_ivy.py').read_text())
names={'Geo','sheet','stem','bud','flower','make_object','measure','export'}
exec(compile(ast.Module(body=[n for n in helper.body if isinstance(n,(ast.FunctionDef,ast.ClassDef)) and n.name in names],type_ignores=[]),'ivy_geometry_helpers','exec'))
OUT=ROOT/'Assets/Art/LogicGardenVioletPlant';SRC=ROOT/'SourceAssets/LogicGardenVioletPlant'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(414)
materials=[]
for name,color in [('Foliage',(.20,.32,.035,1)),('Violet',(.34,.12,.58,1)),('Centers',(.97,.62,.055,1)),('BudTips',(.60,.29,.60,1))]:
    mat=bpy.data.materials.new('VioletPlant_'+name);mat.diffuse_color=color;mat.use_nodes=True;mat.use_backface_culling=True
    sh=mat.node_tree.nodes.get('Principled BSDF');sh.inputs['Base Color'].default_value=color;sh.inputs['Roughness'].default_value=.85;materials.append(mat)

def leaf(length,width):
    g=Geo();v=[(0,0,0),(-width*.42,length*.31,.008),(-width*.5,length*.67,-.006),(0,length,-.025),(width*.5,length*.67,-.006),(width*.42,length*.31,.008)]
    sheet(g,v,[(0,1,2,3),(0,3,4,5)],0,'Leaf');return g
def rotation(direction,normal):
    y=Vector(direction).normalized();z=Vector(normal)-y*Vector(normal).dot(y)
    if z.length<.01:z=Vector((0,-1,.1))-y*Vector((0,-1,.1)).dot(y)
    z.normalize();x=y.cross(z);return Matrix((x,y,z)).transposed()
def bract():
    g=Geo();sheet(g,[(0,0,0),(-.025,.025,-.005),(-.023,.053,-.005),(0,.083,.007),(.023,.053,-.005),(.025,.025,-.005)],[(0,1,2,3),(0,3,4,5)],1,'PurpleBract');return g

large=leaf(.24,.10);small=leaf(.16,.08);openflower=flower()
# Scale the basic five-petal corolla to about 0.18 m across.
openflower.v=[Vector((v.x*1.65,v.y*1.65,v.z*1.5+.075)) for v in openflower.v]
center_ids={i for f,mi in zip(openflower.f,openflower.m) if mi==2 for i in f}
for i in center_ids:
    v=openflower.v[i];openflower.v[i]=Vector((v.x*.6,v.y*.6,.09+(v.z-.09)*.6))
openflower.instance(stem([(0,0,0),(0,0,.075)],.003),Vector((0,0,0)),label='FlowerStem')
spike=Geo();spike.instance(stem([(0,0,0),(.01,0,.15),(0,0,.27)],.0035),Vector((0,0,0)),label='SpikeStem')
for layer,z in enumerate([.105,.16,.215]):
    for k in range(3):
        a=math.tau*k/3+layer*.7;d=Vector((math.cos(a)*.65,math.sin(a)*.65,1))
        spike.instance(bract(),(.007*math.cos(a),.007*math.sin(a),z),rotation(d,Vector((math.cos(a),math.sin(a),.2))),1 if layer<2 else .8,f'Bract_{layer}_{k}')
spike.instance(bud(),(0,0,.26),scale=.65,label='PinkTip')
budmodule=Geo();budmodule.instance(stem([(0,0,0),(.006,0,.18),(0,0,.22)],.003),Vector((0,0,0)),label='BudStem');budmodule.instance(bud(),(0,0,.21),scale=.9,label='ClosedBud')
for sign in [-1,1]:budmodule.instance(bract(),(0,0,.15),rotation((sign*.5,0,1),(0,-1,.2)),.75,'SideBud')
modules={'Open_Flower':openflower,'Flower_Stalk':spike,'Flower_Bud':budmodule,'Large_Leaf':large,'Small_Leaf':small}

plant=Geo();leafcount=0
for row,(count,z,reach,tilt) in enumerate([(10,.04,.05,.32),(8,.105,.065,.55)]):
    for k in range(count):
        a=math.tau*(k+.35*row)/count+random.uniform(-.15,.15)
        d=Vector((math.cos(a),math.sin(a),tilt+random.uniform(-.12,.15)))
        p=Vector((reach*math.cos(a),reach*math.sin(a),z))
        # Small leaf-bearing petiole connects every radial leaf to the clump.
        plant.instance(stem([(0,0,.008),p],.0025),Vector((0,0,0)),label=f'Petiole_{leafcount}')
        plant.instance(large,p,rotation(d,(0,0,1)),random.uniform(.91,1.15),f'BasalLeaf_{leafcount:02d}');leafcount+=1
tips=[(-.025,.035,.64),(-.16,.045,.54),(.16,.06,.49),(-.23,-.06,.36),(.23,-.05,.39)]
for j,tip in enumerate(tips):
    tip=Vector(tip);origin=Vector((tip.x*.55,tip.y*.55,tip.z-.315))
    plant.instance(stem([(tip.x*.1,tip.y*.1,0),origin*.55,origin],.004),Vector((0,0,0)),label=f'MainStem_{j}')
    plant.instance(spike,origin,Vector((0,0,1)).rotation_difference(Vector((tip.x*.20,tip.y*.3,1)).normalized()).to_matrix(),random.uniform(.9,1),f'FlowerSpike_{j}')
    for k in range(2):
        a=math.atan2(tip.y,tip.x)+(1 if k else -1)*.9;p=origin*(.52+.3*k)
        plant.instance(small,p,rotation((math.cos(a),math.sin(a),.75),(0,0,1)),.75,f'StemLeaf_{leafcount:02d}');leafcount+=1
spots=[(-.13,-.12,.34),(.115,-.13,.29),(-.04,-.19,.23),(.13,.12,.39),(-.15,.13,.29)]
for j,p in enumerate(spots):
    p=Vector(p);normal=Vector((p.x*2,p.y*2-.15,.65)).normalized();rot=Vector((0,0,1)).rotation_difference(normal).to_matrix()
    base=p-rot@Vector((0,0,.075));plant.instance(stem([(base.x*.15,base.y*.15,.01),base],.003),Vector((0,0,0)),label=f'OpenFlowerStem_{j}')
    plant.instance(openflower,base,rot,random.uniform(.85,1.02),f'OpenFlower_{j}')

mins=[min(v[i] for v in plant.v) for i in range(3)];maxs=[max(v[i] for v in plant.v) for i in range(3)]
sc=[.8/(maxs[0]-mins[0]),.8/(maxs[1]-mins[1]),.7/(maxs[2]-mins[2])]
plant.v=[Vector(((v.x-(maxs[0]+mins[0])/2)*sc[0],(v.y-(maxs[1]+mins[1])/2)*sc[1],(v.z-mins[2])*sc[2])) for v in plant.v]
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1;bpy.context.preferences.filepaths.save_version=0
ob=make_object('LogicGarden_VioletPlant',plant);ob.select_set(True);bpy.context.view_layer.objects.active=ob
stats=measure([ob]);stats.update({'leaf_count':leafcount,'flower_spikes':5,'open_flowers':5,'closed_tip_buds':5,'purple_bracts':45,'mesh_objects':1,'materials':4,'modifiers':0,'dimensions_unity_xyz':[.8,.7,.8],'pivot':'bottom centre'})
assert stats['quads']==stats['faces'] and stats['degenerate_faces']==0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_distance=1.7;area.spaces.active.region_3d.view_location=Vector((0,0,.32));area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'LogicGarden_VioletPlant.blend'));export(OUT/'LogicGarden_VioletPlant.fbx',[ob])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
kit=[]
for j,(name,g) in enumerate(modules.items()):
    ob=make_object(name,g);ob.location=((j%3)*.38,(j//3)*.38,0);kit.append(ob)
bpy.context.view_layer.objects.active=kit[0]
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'LogicGarden_Violet_ModularKit.blend'));export(OUT/'LogicGarden_Violet_ModularKit.fbx',kit)
stats['modular_kit']={o.name:measure([o]) for o in kit}
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LogicGarden_Violet_ModularKit.fbx'))
kit=[o for o in scene.objects if o.type=='MESH'];assert len(kit)==5 and set(o.name for o in kit)==set(modules)
assert all(all(len(p.vertices)==4 for p in o.data.polygons) for o in kit);stats['kit_fbx_roundtrip']='PASS'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LogicGarden_VioletPlant.fbx'))
obs=[o for o in scene.objects if o.type=='MESH'];assert len(obs)==1
back=measure(obs);assert back['triangles']==stats['triangles'] and back['quads']==stats['quads']
assert all(abs(s-1)<1e-5 for s in obs[0].scale) and not obs[0].modifiers
stats['fbx_roundtrip']='PASS';stats['fbx_reimport']=back
(SRC/'geometry-report.json').write_text(json.dumps(stats,indent=2));print('VIOLET_REPORT '+json.dumps(stats))

scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.world.color=(.22,.22,.22)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003));floor=bpy.context.object
fm=bpy.data.materials.new('PreviewGround');fm.diffuse_color=(.105,.12,.10,1);floor.data.materials.append(fm)
target=Vector((0,0,.33))
for loc,power,size in [((-2,-3,4),370,3),((3,-1,2),130,2),((1,3,4),300,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(target-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=1.04;scene.camera=cam
for name,loc,aim in [('Preview',(1.5,-3,1.4),target),('Front',(0,-4,.49),target),('Side',(4,0,.49),target),('Top',(0,0,4),Vector((0,0,0)))]:
    cam.location=loc;cam.rotation_euler=(aim-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(SRC/f'LogicGarden_VioletPlant_{name}.png');bpy.ops.render.render(write_still=True)
