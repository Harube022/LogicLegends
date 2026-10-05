"""Sets stage: reusable static chamber architecture, Blender 4.5."""
import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/SetsArena'; SRC=ROOT/'SourceAssets/SetsArena'
OUT.mkdir(parents=True,exist_ok=True); SRC.mkdir(parents=True,exist_ok=True)
random.seed(41107)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0
palette=[(.19,.205,.235),(.23,.245,.27),(.27,.275,.29),(.31,.30,.29),(.35,.33,.30),(.245,.25,.265),(.28,.29,.32),(.21,.22,.245),
(.38,.35,.29),(.43,.39,.32),(.30,.28,.25),(.48,.43,.35),(.095,.095,.10),(.13,.14,.16),(.115,.10,.085),(.19,.15,.105),
(.25,.025,.045),(.37,.042,.061),(.48,.29,.075),(.66,.45,.13),(.075,.105,.14),(.12,.16,.20),(.09,.065,.047),(.16,.11,.073),
(1,.38,.035),(1,.65,.12),(1,.86,.38),(.17,.19,.22),(.16,.18,.21),(.20,.215,.24),(.24,.25,.265),(.29,.28,.26)]
palette*=2
img=bpy.data.images.new('SetsArena_Palette',width=64,height=64,alpha=False)
pixels=[]
for y in range(64):
    for x in range(64):pixels.extend((*palette[y//8*8+x//8],1))
img.pixels=pixels;img.filepath_raw=str(OUT/'SetsArena_Palette.png');img.file_format='PNG';img.save();img.pack()
mats=[]
for name,rough,metal,emission in [('Arena_StoneCloth',.86,0,0),('Arena_Metal',.42,.65,0),('Arena_Flame',.45,0,3)]:
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;p=n.get('Principled BSDF')
    p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    tex=n.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest';m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
    if emission:m.node_tree.links.new(tex.outputs['Color'],p.inputs['Emission Color']);p.inputs['Emission Strength'].default_value=emission
    mats.append(m)

class Mesh:
    def __init__(self):self.v=[];self.f=[];self.colors=[]
    def add(self,v,f,c):
        offset=len(self.v);self.v.extend(Vector(p) for p in v);self.f.extend(tuple(offset+i for i in ff) for ff in f);self.colors.extend(c if isinstance(c,list) else [c]*len(f))
    def box(self,p,s,c=0,bevel=0,rot=None):
        x,y,z=(t/2 for t in s);p=Vector(p);rot=rot or Matrix.Identity(3)
        if bevel:
            b=min(bevel,x*.3,y*.3,z*.8)
            v=[(a,bv,-z) for a,bv in [(-x,-y),(x,-y),(x,y),(-x,y)]]
            v += [(a,bv,z-b) for a,bv in [(-x,-y),(x,-y),(x,y),(-x,y)]]
            v += [(a,bv,z) for a,bv in [(-x+b,-y+b),(x-b,-y+b),(x-b,y-b),(-x+b,y-b)]]
            f=[(3,2,1,0),(8,9,10,11)]
            for k in range(4):j=(k+1)%4;f.extend([(k,j,j+4,k+4),(k+4,j+4,j+8,k+8)])
        else:
            v=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
            f=[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
        self.add([p+rot@Vector(q) for q in v],f,c)
    def column(self,p,r,h,c,n=8,r2=None):
        r2=r if r2 is None else r2;x,y,z=p;v=[]
        for zz,rr in [(z,r),(z+h,r2)]:
            for k in range(n):a=(k+.5)*math.tau/n;v.append((x+rr*math.cos(a),y+rr*math.sin(a),zz))
        f=[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(k,(k+1)%n,(k+1)%n+n,k+n) for k in range(n)]
        self.add(v,f,c)
parts=[]
def make(name,g,mat=0):
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(g.v,[],g.f);me.update()
    # Consistent outward normals for closed architecture and beveled slabs.
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);me.materials.append(mats[mat])
    uv=me.uv_layers.new(name='PaletteUV')
    for f,c in zip(me.polygons,g.colors):
        for li in f.loop_indices:uv.data[li].uv=((c%8+.5)/8,(c//8+.5)/8)
    parts.append(ob);return ob

# Internal play space: 14 m wide, 12 m deep, 5.6 m high; floor at Z=0.
floor=Mesh();floor.box((0,0,-.19),(14.8,12.8,.28),13);make('Floor_Foundation',floor)
for label,xa,xb,ya,yb in [('Center',-5,5,-4,3.5),('West',-7,-5,-6,6),('East',5,7,-6,6),('Rear',-5,5,3.5,6),('Entrance',-5,5,-6,-4)]:
    g=Mesh();nx=round((xb-xa)/.68);ny=round((yb-ya)/.58);dx=(xb-xa)/nx;dy=(yb-ya)/ny
    for row in range(ny):
        for col in range(nx):
            x=xa+(col+.5)*dx;y=ya+(row+.5)*dy
            g.box((x,y,-.035),(dx-.012,dy-.012,.07),random.choice([0,1,2,5,6,7,27,28,29,30,31]),.016)
    make('Floor_Tiles_'+label,g)
# A plain decorative stone sill, flush with the floor, without gameplay markings.
g=Mesh()
for x in [-5.08,5.08]:g.box((x,-.25,.001),(.12,7.72,.022),8,.006)
for y in [-4.08,3.58]:g.box((0,y,.001),(10.28,.12,.022),8,.006)
make('Floor_StoneInlay',g)

def wall(label,center,length,rot):
    g=Mesh();R=Matrix.Rotation(rot,3,'Z');P=Vector(center)
    def b(p,s,c,bev=0):g.box(P+R@Vector(p),s,c,bev,R)
    # Closed backing plus shallow courses; staggered joints with clipped end stones.
    b((0,.20,2.8),(length,.38,5.6),12)
    for row in range(10):
        z=.29+row*.535;offset=.5 if row%2 else 0;x=-length/2-offset
        while x<length/2:
            lo=max(x,-length/2);hi=min(x+1.0,length/2)
            if hi-lo>.05:b(((lo+hi)/2,-.018,z),(hi-lo-.022,.11,.512),random.choice([0,1,2,3,5,6,7]),.012)
            x+=1
    for z,depth,height,c in [(.16,.32,.32,10),(.47,.27,.13,8),(1.15,.20,.10,8),(4.82,.26,.18,8),(5.26,.38,.27,9),(5.49,.45,.17,10)]:
        b((0,-.06,z),(length,depth,height),c,.025)
    return make('Wall_'+label,g)
wall('Rear',(0,6,0),14.4,0);wall('West',(-7,0,0),12,math.pi/2);wall('East',(7,0,0),12,-math.pi/2)
wall('Front_Left',(-4.26,-6,0),5.48,math.pi);wall('Front_Right',(4.26,-6,0),5.48,math.pi)
g=Mesh();g.box((0,-6.15,4.65),(3.04,.52,1.9),2)
for x in [-1.48,1.48]:g.box((x,-6.06,1.85),(.12,.30,3.70),8)
make('Front_DoorHeader',g)

# Attached square stone piers and bands articulate the wall rhythm.
def pier(label,x,y):
    g=Mesh()
    for z,s,h,c in [(0,.90,.23,10),(.23,.77,.18,8),(.41,.61,.66,3),(1.07,.73,.15,9),(1.22,.54,2.75,4),(3.97,.64,.15,8),(4.12,.73,.22,9),(4.34,.84,.23,10),(4.57,.91,.28,8)]:
        g.box((x,y,z+h/2),(s,s,h),c,.035)
    for z in [1.78,2.55,3.32]:g.box((x,y,z),(.563,.563,.038),10,.007)
    make('Pier_'+label,g)
for i,x in enumerate([-6.48,-3.62,3.62,6.48]):pier('Rear_'+str(i),x,5.68)
for side,x in [('West',-6.72),('East',6.72)]:
    for i,y in enumerate([-4.8,-1.4,2.05]):pier(side+'_'+str(i),x,y)
for x in [-1.7,1.7]:pier('Front_'+str(x),x,-5.88)

# Pointed blind recess behind the future statue. It contains no statue mesh.
def pointed_outline(w,spring,peak,steps=6):
    pts=[(-w,0),(-w,spring)]
    for k in range(1,steps+1):
        t=k/steps;pts.append((-w*(1-t)**.85,spring+(peak-spring)*math.sin(t*math.pi/2)))
    for k in range(steps-1,-1,-1):
        t=k/steps;pts.append((w*(1-t)**.85,spring+(peak-spring)*math.sin(t*math.pi/2)))
    pts.append((w,0));return pts
def arch(name,x,y,z,w,spring,peak):
    points=pointed_outline(w,spring,peak);outer=pointed_outline(w+.22,spring,peak+.26);g=Mesh()
    # Vertical plane facing the room with thickness, behind the trim.
    v=[(x+xx,y-.01,z+zz) for xx,zz in points];g.add(v,[tuple(reversed(range(len(v))))],20)
    for k in range(len(points)-1):
        a,b=points[k],points[k+1];c,d=outer[k+1],outer[k]
        vv=[(x+xx,yy,z+zz) for yy in [y-.12,y+.03] for xx,zz in [a,b,c,d]]
        g.add(vv,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],random.choice([8,9,11]))
    make(name,g)
arch('Rear_StatueAlcove',0,5.86,.38,1.22,2.70,4.35)
# Shallow architectural dais reserves a 2.6 x 1.9 m location for the existing statue.
g=Mesh();g.box((0,4.88,.095),(3.02,1.90,.19),10,.06);g.box((0,5.06,.245),(2.73,1.52,.11),8,.05);make('Rear_StatueDais',g)

# Entry is a closed pair of reusable doors, not an incomplete open wall.
for side in [-1,1]:
    g=Mesh()
    for k in range(5):g.box((side*(.15+k*.284),-6.055,1.86),(.275,.20,3.70),random.choice([14,15,22,23]),.013)
    for z in [.35,1.32,2.42,3.32]:g.box((side*.74,-5.925,z),(1.44,.06,.095),18,.012)
    make('Front_Door_'+('L' if side<0 else 'R'),g)
    g=Mesh();g.column((side*.20,-5.85,1.58),.065,.08,19,n=8);make('Front_DoorHandle_'+str(side),g,1)

# Cloth banners and gold emblems: wall decorations, not information boards.
def banner(label,p,angle):
    P=Vector(p);R=Matrix.Rotation(angle,3,'Z');g=Mesh()
    outline=[(-.42,0,0),(.42,0,0),(.42,0,-1.78),(0,-.018,-2.12),(-.42,0,-1.78)]
    vv=[P+R@Vector(q) for q in outline]
    vv+= [v+R@Vector((0,.018,0)) for v in vv]
    g.add(vv,[(4,3,2,1,0),(5,6,7,8,9)]+[(k,(k+1)%5,(k+1)%5+5,k+5) for k in range(5)],16)
    for x in [-.385,.385]:g.box(P+R@Vector((x,-.012,-.89)),(.035,.016,1.76),19,rot=R)
    # Gold diamond with a small central star.
    v=[(0,-.025,-.63),(.19,-.025,-1.0),(0,-.025,-1.43),(-.19,-.025,-1.0)]
    g.add([P+R@Vector(q) for q in v],[(0,1,2,3)],18)
    v=[(0,-.03,-.88),(.045,-.03,-.98),(.13,-.03,-1.02),(.045,-.03,-1.06),(0,-.03,-1.17),(-.045,-.03,-1.06),(-.13,-.03,-1.02),(-.045,-.03,-.98)]
    g.add([P+R@Vector(q) for q in v],[tuple(range(8))],19)
    make('Banner_'+label,g)
    g=Mesh();g.box(P+Vector((0,0,.065)),(1.05,.085,.085),18,.01,R);make('BannerRod_'+label,g,1)
for i,x in enumerate([-5.08,-2.27,2.27,5.08]):banner('Rear_'+str(i),(x,5.72,4.78),0)
for side,x,a in [('West',-6.81,math.pi/2),('East',6.81,-math.pi/2)]:
    for i,y in enumerate([-3.1,.32,3.85]):banner(side+'_'+str(i),(x,y,4.73),a)

flame_positions=[]
def sconce(label,p):
    x,y,z=p;g=Mesh()
    for zz,r,h,c,r2 in [(z,.21,.13,10,.23),(z+.13,.17,.34,8,.15),(z+.47,.28,.10,18,.30),(z+.57,.30,.19,18,.18)]:g.column((x,y,zz),r,h,c,r2=r2)
    g.column((x,y,z+.72),.15,.035,12)
    make('Sconce_'+label,g,1)
    g=Mesh()
    for k in range(5):
        a=k*2.4;dx=.10*math.cos(a);dy=.10*math.sin(a);h=.31+random.random()*.25
        g.column((x+dx,y+dy,z+.75),.072,h*.63,24,n=5,r2=.055)
        g.column((x+dx,y+dy,z+.75+h*.63),.055,h*.37,25,n=5,r2=.006)
    make('Flame_'+label,g,2);flame_positions.append((x,y,z+1.00))
for i,x in enumerate([-3.60,3.60]):sconce('Rear_'+str(i),(x,5.05,1.48))
for side,x in [('West',-6.13),('East',6.13)]:
    for i,y in enumerate([-4.75,-1.36,2.08]):sconce(side+'_'+str(i),(x,y,1.53))

# A separate solid ceiling and timber coffers complete the enclosure.
g=Mesh();g.box((0,0,5.72),(14.8,12.8,.24),13)
for y in [-5.6,-2.8,0,2.8,5.6]:g.box((0,y,5.51),(14.5,.24,.30),14,.024)
for x in [-6.65,-3.35,0,3.35,6.65]:g.box((x,0,5.54),(.20,12.5,.25),14,.02)
make('Ceiling_Removable',g)

root=bpy.data.objects.new('Sets_Room_Arena',None);bpy.context.collection.objects.link(root)
for ob in parts:ob.parent=root
anchors=[]
for name,pos in [('BookStatue',(0,5.02,.30)),('VennFloor',(0,-.25,.025)),('SetInformationBoard',(-4.65,4.9,1.8)),('ChallengeUI',(4.65,4.9,1.8)),('ElementPedestal',(-5.75,-2.7,.01)),('PlayerEntrance',(0,-4.85,.01))]:
    ob=bpy.data.objects.new('Anchor_'+name,None);bpy.context.collection.objects.link(ob);ob.location=pos;ob.empty_display_type='PLAIN_AXES';ob.empty_display_size=.25;ob.parent=root;anchors.append(ob)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in obs if o.type=='MESH')
def stats(obs):
    r={'vertices':0,'faces':0,'triangles':0,'zero_area_faces':0,'loose_vertices':0}
    for o in obs:
        m=o.data;m.calc_loop_triangles();r['vertices']+=len(m.vertices);r['faces']+=len(m.polygons);r['triangles']+=len(m.loop_triangles)
        bm=bmesh.new();bm.from_mesh(m);r['zero_area_faces']+=sum(f.calc_area()<1e-10 for f in bm.faces);r['loose_vertices']+=sum(not v.link_faces for v in bm.verts);bm.free()
    return r
report=stats(parts);report.update({'mesh_objects':len(parts),'materials':3,'modifiers':0,'interior_unity_xyz':[14,5.6,12],'clear_gameplay_area_unity_xz':[10,7.5],'anchors':{o.name:[round(c,3) for c in (o.location.x,o.location.z,-o.location.y)] for o in anchors}})
assert report['zero_area_faces']==0 and report['loose_vertices']==0
select(parts)
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':a.spaces.active.region_3d.view_distance=21;a.spaces.active.region_3d.view_location=Vector((0,0,1.5));a.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Sets_Room_Arena.blend'))
select(parts+[root]+anchors)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Sets_Room_Arena.fbx'),use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,path_mode='COPY',embed_textures=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'Sets_Room_Arena.fbx'))
imported=[o for o in scene.objects if o.type=='MESH'];back=stats(imported)
print('ROUNDTRIP',len(imported),len(parts),back,report)
assert len(imported)==len(parts) and back['triangles']==report['triangles'] and back['zero_area_faces']==0 and back['loose_vertices']==0
assert all(not o.modifiers and all(abs(s-1)<1e-5 for s in o.scale) for o in imported)
assert all(o.type in {'EMPTY','MESH'} for o in scene.objects)
report['fbx_roundtrip']='PASS';report['reimport']=back
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))

# Render imported geometry. Lighting and cameras exist only in previews.
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.world.color=(.065,.08,.12)
def area(p,energy,color,size,target):
    bpy.ops.object.light_add(type='AREA',location=p);o=bpy.context.object;o.data.energy=energy;o.data.color=color;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for p in flame_positions:
    bpy.ops.object.light_add(type='POINT',location=Vector(p)+Vector((0,0,.20)));l=bpy.context.object;l.data.energy=95;l.data.color=(1,.40,.095);l.data.shadow_soft_size=.32
area((0,-1,5.35),1600,(.51,.64,1),8,(0,0,0));area((0,4.4,4.8),700,(1,.67,.33),4,(0,1,0));area((0,-5.2,3.5),900,(.71,.78,1),7,(0,2,2))
scene.use_nodes=True;n=scene.node_tree.nodes;n.clear();rl=n.new('CompositorNodeRLayers');gl=n.new('CompositorNodeGlare');gl.glare_type='FOG_GLOW';gl.threshold=2;gl.quality='HIGH';co=n.new('CompositorNodeComposite');scene.node_tree.links.new(rl.outputs['Image'],gl.inputs['Image']);scene.node_tree.links.new(gl.outputs['Image'],co.inputs['Image'])
bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera
camera.location=(0,-5.58,3.8);camera.data.type='PERSP';camera.data.lens=18;camera.rotation_euler=(Vector((0,2.2,2.0))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SRC/'Sets_Room_Arena_Interior.png');bpy.ops.render.render(write_still=True)
for o in imported:
    if not o.name.startswith('Floor') and any(s in o.name for s in ['Ceiling','Front','East']):o.hide_render=True
camera.data.type='ORTHO';camera.data.ortho_scale=22.5;camera.location=(15,-18,15);camera.rotation_euler=(Vector((0,.2,1.1))-camera.location).to_track_quat('-Z','Y').to_euler()
area((2,-8,14),2100,(.80,.87,1),10,(0,0,0))
scene.render.filepath=str(SRC/'Sets_Room_Arena_Cutaway.png');bpy.ops.render.render(write_still=True)
camera.location=(0,0,22);camera.rotation_euler=(0,0,0);camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=18.5
for o in imported:
    o.hide_render='Ceiling' in o.name
scene.render.filepath=str(SRC/'Sets_Room_Arena_Top.png');bpy.ops.render.render(write_still=True)
print('ARENA_REPORT '+json.dumps(report))
