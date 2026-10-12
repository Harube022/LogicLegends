"""Build a shared induction altar and separable blank boards; Blender 4.5."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/InductionAltar'
SRC=ROOT/'SourceAssets/InductionAltar'
OUT.mkdir(parents=True,exist_ok=True); SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene; scene.unit_settings.system='METRIC'
random.seed(17)
def material(name,color,rough=.7,metal=0,glow=0,texture=None):
    m=bpy.data.materials.new(name); m.use_nodes=True; m.diffuse_color=(*color,1)
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=rough; p.inputs['Metallic'].default_value=metal
    p.inputs['Emission Color'].default_value=(*color,1); p.inputs['Emission Strength'].default_value=glow
    if texture:
        t=m.node_tree.nodes.new('ShaderNodeTexImage'); t.image=bpy.data.images.load(str(texture),check_existing=True)
        m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
    return m
stone=material('Altar_Stone',(.13,.15,.23),texture=OUT/'Textures/Stone_BaseColor.png')
edge=material('Altar_StoneEdge',(.24,.25,.32))
bronze=material('Altar_Bronze',(.40,.25,.10),.4,.65)
navy=material('Altar_TitleFace',(.008,.022,.055),.48)
paper=material('Altar_Parchment',(.79,.57,.30),.88)
blue=material('Altar_Accent',(.012,.31,.95),.25,.2,2)
crystal=material('Altar_Crystal',(.035,.48,.95),.2,.25,.65)
dark=material('Altar_EmblemFace',(.015,.045,.10),.35,.25)
ink=material('Preview_Ink',(.045,.022,.008))
white=material('Preview_TitleInk',(.85,.94,1),.5,0,.3)
groups={}
def add(ob,name,mat,group):
    ob.name=name; ob.data.materials.append(mat); groups.setdefault(group,[]).append(ob)
    return ob
def box(name,loc,size,mat,group,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); ob=bpy.context.object; ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=ob.modifiers.new('Cut stone edges','BEVEL'); mod.width=bevel; mod.segments=1
        bpy.context.view_layer.objects.active=ob; bpy.ops.object.modifier_apply(modifier=mod.name)
    return add(ob,name,mat,group)
def cylinder(name,loc,radius,depth,mat,group,front=False,vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
    ob=bpy.context.object
    if front: ob.rotation_euler[0]=math.pi/2
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    return add(ob,name,mat,group)
def polygon_plate(name,coords,y,depth,mat,group):
    n=len(coords); v=[(x,yy,z) for yy in [y-depth/2,y+depth/2] for x,z in coords]
    f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    me=bpy.data.meshes.new(name); me.from_pydata(v,[],f); me.update()
    ob=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(ob); return add(ob,name,mat,group)
def plaque(name,w,h,z,y,mat,group):
    c=.11; coords=[(-w/2+c,z-h/2),(w/2-c,z-h/2),(w/2,z-h/2+c),(w/2,z+h/2-c),(w/2-c,z+h/2),(-w/2+c,z+h/2),(-w/2,z+h/2-c),(-w/2,z-h/2+c)]
    return polygon_plate(name,coords,y,.055,mat,group)
def gem(name,x,y,z,w,h,group='CrystalAccents'):
    v=[]
    for zz,r in [(z,.55),(z+h*.28,1),(z+h*.70,.72)]:
        for i in range(6):
            a=i*math.tau/6; v.append((x+math.cos(a)*w*r,y+math.sin(a)*w*r,zz))
    v.append((x+w*.2,y,z+h)); f=[tuple(reversed(range(6)))]
    for j in range(2):
        f.extend([(j*6+i,j*6+(i+1)%6,(j+1)*6+(i+1)%6,(j+1)*6+i) for i in range(6)])
    f.extend([(12+i,12+(i+1)%6,18) for i in range(6)])
    me=bpy.data.meshes.new(name); me.from_pydata(v,[],f); me.update()
    ob=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(ob); add(ob,name,crystal,group)
def bar_between(name,a,b,width,mat,group):
    a,b=Vector(a),Vector(b); ob=box(name,(a+b)/2,(width,width,(b-a).length),mat,group,.004)
    ob.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); return ob

# Broad two-step foot and masonry plinth.
box('BottomStep',(0,0,.09),(2.20,1.20,.18),stone,'StonePedestal',.045)
box('UpperStep',(0,.025,.23),(2.05,1.08,.14),edge,'StonePedestal')
for row in range(3):
    n=4 if row%2==0 else 5; width=1.94/n
    for i in range(n):
        box('PedestalBlock',(-.97+width*(i+.5),.03,.39+row*.205),(width-.015,.94,.19),stone,'StonePedestal',.018)
box('TableCornice',(0,0,.96),(2.20,1.12,.17),edge,'StonePedestal',.04)
box('TableTop',(0,0,1.065),(2.09,1.035,.07),stone,'StonePedestal',.018)
# Pillars frame a shallow recessed back behind two scroll boards.
box('RecessedBack',(0,.34,1.78),(1.42,.13,1.40),stone,'UprightFrame')
for s in [-1,1]:
    x=s*.83
    for i in range(5): box('UprightBlock',(x,.18,1.22+i*.245),(.27,.37,.23),stone,'UprightFrame',.018)
    box('PillarCap',(x,.18,2.48),(.39,.47,.15),edge,'UprightFrame')
    box('PillarFoot',(x,.15,1.15),(.39,.47,.14),edge,'UprightFrame')
    box('VerticalInlay',(x,-.02,1.79),(.025,.025,1.18),blue,'StationAccents',.005)
    for z in [1.27,2.32]:
        box('BronzePillarBand',(x,-.025,z),(.32,.065,.075),bronze,'BronzeHardware',.015)
    for y in [-.32,.04]:
        gem('TallCrystal',s*.88,y,1.10,.08,.34 if y<0 else .24)
        gem('SmallCrystal',s*.71,y-.09,1.10,.045,.19)
# Heraldic layered title. Every layer joins only its own functional board.
plaque('TitleBacking',2.18,.55,2.65,-.02,bronze,'TitleBoard')
plaque('TitleGlowTrim',2.10,.48,2.65,-.058,blue,'TitleBoard')
plaque('TitleInset',1.99,.40,2.65,-.098,navy,'TitleBoard')
for s in [-1,1]:
    polygon_plate('TitleDiamond',[(s*1.00-.07,2.65),(s*1.00,2.73),(s*1.00+.07,2.65),(s*1.00,2.57)],-.14,.025,crystal,'TitleBoard')
    cylinder('TitleRivet',(s*.86,-.145,2.79),.015,.016,bronze,'TitleBoard',True,8)
# Gently bowed parchment with actual rolled ends and cap hardware.
for index,z in enumerate([2.08,1.51],1):
    group=f'ParchmentPanel_{index:02d}'
    box('PanelBacking',(0,.08,z),(1.36,.12,.49),bronze,group,.035)
    verts=[]; nx=20; nz=10
    for j in range(nz+1):
        zz=z-.195+j*.039
        for i in range(nx+1):
            xx=-.57+i*.057
            yy=-.045-.014*math.sin(i*math.pi/nx)-.007*math.cos(j*math.tau/nz)
            verts.append((xx,yy,zz))
    faces=[]
    for j in range(nz):
        for i in range(nx):
            k=j*(nx+1)+i; faces.append((k,k+1,k+nx+2,k+nx+1))
    me=bpy.data.meshes.new('Parchment');me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new('BlankParchment',me);bpy.context.collection.objects.link(ob);add(ob,'BlankParchment',paper,group)
    uv=me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        for li in p.loop_indices:
            co=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=((co.x+.57)/1.14,(co.z-z+.195)/.39)
    solid=ob.modifiers.new('Paper thickness','SOLIDIFY');solid.thickness=.012
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=solid.name)
    for s in [-1,1]:
        roll=cylinder('ParchmentRoll',(s*.59,-.06,z),.047,.43,paper,group,False,16)
        for dz in [-.21,.21]: cylinder('ScrollCap',(s*.59,-.06,z+dz),.056,.026,bronze,group,False,12)
        for zz in [z-.18,z+.18]:
            box('PanelCorner',(s*.65,-.04,zz),(.075,.10,.07),blue,group,.015)
# Front medallion with concentric machined rings and raised check.
cylinder('EmblemStoneMount',(0,-.545,.60),.365,.12,edge,'CircularEmblem',True,32)
cylinder('EmblemBronzeRim',(0,-.62,.60),.32,.05,bronze,'CircularEmblem',True,48)
cylinder('EmblemGlowRim',(0,-.655,.60),.283,.028,blue,'CircularEmblem',True,48)
cylinder('EmblemInset',(0,-.675,.60),.253,.025,dark,'CircularEmblem',True,48)
bar_between('CheckShort',(-.13,-.704,.61),(-.035,-.704,.51),.045,blue,'CircularEmblem')
bar_between('CheckLong',(-.035,-.704,.51),(.14,-.704,.73),.045,blue,'CircularEmblem')
for x in [-.80,.80]:
    polygon_plate('TableDiamond',[(x-.04,.91),(x,.96),(x+.04,.91),(x,.86)],-.575,.025,crystal,'StationAccents')

root=bpy.data.objects.new('InductionAltar_Shared',None);bpy.context.collection.objects.link(root)
parts=[]
for name,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    ob.parent=root;parts.append(ob)
    # Recalculate outward normals and unwrap non-paper faces.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False)
    if name not in ['ParchmentPanel_01','ParchmentPanel_02']: bpy.ops.uv.smart_project(island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT')
anchors=[]
for name,loc in [('TitleTextAnchor',(0,-.17,2.65)),('Panel01TextAnchor',(0,-.105,2.08)),('Panel02TextAnchor',(0,-.105,1.51))]:
    ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob);ob.parent=root;ob.location=loc;anchors.append(ob)
for im in bpy.data.images:
    if im.source=='FILE': im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'InductionAltar.blend'))
def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
def export(filename,objects):
    select(objects)
    bpy.ops.export_scene.fbx(filepath=str(OUT/filename),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,bake_anim=False,path_mode='COPY',embed_textures=True)
export('InductionAltar_Shared.fbx',[root]+parts+anchors)
boardparts=[ob for ob in parts if ob.name in ['TitleBoard','ParchmentPanel_01','ParchmentPanel_02']]
export('InductionAltar_Boards.fbx',[root]+boardparts+anchors)
def stats(objects):
    triangles=0;zero=0
    for ob in objects:
        if ob.type!='MESH':continue
        ob.data.calc_loop_triangles();triangles+=len(ob.data.loop_triangles)
        zero+=sum(p.area<1e-10 for p in ob.data.polygons)
    return {'triangles':triangles,'mesh_objects':sum(o.type=='MESH' for o in objects),'zero_area_faces':zero}
expected=stats(parts)
# Verify the deliverable FBX, rather than only the Blender source.
original=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(OUT/'InductionAltar_Shared.fbx'))
imported=list(set(scene.objects)-original);actual=stats(imported)
assert actual==expected,(actual,expected)
assert all(all(abs(v-1)<1e-5 for v in o.scale) for o in imported)
for ob in imported:bpy.data.objects.remove(ob,do_unlink=True)
report={**actual,'fbx_roundtrip':'PASS','root_pivot':'bottom center (0,0,0)','dimensions_m':[2.20,1.30,2.925],'shared_board_meshes':3,'baked_text':False,'geometry_variants':1}
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))

# Studio rendering only; labels below never enter the source model or FBX.
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.world.color=(.09,.11,.16);scene.view_settings.view_transform='AgX'
floor=box('PreviewFloor',(0,0,-.04),(200,200,.07),material('PreviewFloorMat',(.025,.035,.055)),'Preview',0)
def aim(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
for loc,power,size in [((-3,-4,6),900,4),((4,-2,4),750,3),((1,3,5),1100,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.data.energy=power;l.data.size=size;aim(l,(0,0,1.5))
bpy.ops.object.camera_add(location=(3.6,-6.5,3.3));camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=3.8;aim(camera,(0,0,1.43))
texts=[]
for name,loc,w,h,mat in [('Title',(0,-.175,2.65),1.75,.28,white),('Panel1',(0,-.12,2.08),1.02,.23,ink),('Panel2',(0,-.12,1.51),1.02,.23,ink)]:
    curve=bpy.data.curves.new(name,'FONT');curve.align_x='CENTER';curve.align_y='CENTER';curve.size=.20;curve.extrude=.001
    ob=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(math.pi/2,0,0);ob.data.materials.append(mat);texts.append((ob,w,h))
variants=[('BaseCase',(0.012,.31,.95),'BASE CASE','n = 1','P(1)'),('InductiveHypothesis',(.37,.055,.85),'INDUCTIVE HYPOTHESIS','Assume P(k)','k >= 1'),('InductiveStep',(1,.31,.015),'INDUCTIVE STEP','Use P(k)','Prove P(k + 1)')]
for name,color,*labels in variants:
    for m in [blue,crystal]:
        p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Emission Color'].default_value=(*color,1)
    for (ob,w,h),label in zip(texts,labels):
        ob.data.body=label;ob.scale=(1,1,1);bpy.context.view_layer.update();scale=min(w/max(ob.dimensions.x,.001),h/max(ob.dimensions.z,.001),1);ob.scale=(scale,scale,scale)
    scene.render.filepath=str(SRC/f'{name}_Preview.png');bpy.ops.render.render(write_still=True)
print('INDUCTION_ALTAR_REPORT '+json.dumps(report))
