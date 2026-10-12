"""Option 3 foliage only: clipped hedge and pink/white/lavender flower clusters."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/Art/Option3Foliage';SRC=ROOT/'SourceAssets/Option3Foliage'
for p in [OUT/'Textures',OUT/'Editor',SRC]:p.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0
random.seed(33012);scene=bpy.context.scene;scene.unit_settings.system='METRIC'
colors=[(.045,.11,.012),(.075,.18,.022),(.11,.24,.03),(.15,.31,.04),(.21,.37,.06),(.26,.43,.085),(.34,.49,.11),(.085,.15,.018),
(.82,.18,.40),(.98,.33,.60),(.99,.52,.72),(.99,.69,.82),(.68,.12,.28),(.94,.86,.69),(.99,.97,.88),(.99,.70,.12),
(.30,.14,.49),(.45,.24,.66),(.62,.37,.81),(.73,.51,.89),(.83,.64,.96),(.15,.23,.035),(.22,.31,.045),(.12,.19,.025)]
image=bpy.data.images.new('Option3_FoliagePalette',96,64,alpha=False);px=[]
for yy in range(64):
    for xx in range(96):px.extend((*colors[(yy//16)*6+xx//16],1))
image.pixels=px;image.filepath_raw=str(OUT/'Textures/Option3_FoliagePalette.png');image.file_format='PNG';image.save();image.pack()
mat=bpy.data.materials.new('Option3_Foliage');mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.84
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest';mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
class Geo:
    def __init__(self):self.v=[];self.f=[];self.c=[]
    def add(self,v,f,c):
        k=len(self.v);self.v.extend(tuple(p) for p in v);self.f.extend(tuple(k+i for i in face) for face in f);self.c.extend([c]*len(f))
    def leaf(self,p,d,n,l,w,c):
        p=Vector(p);d=Vector(d).normalized();n=Vector(n);n=(n-d*n.dot(d)).normalized();s=d.cross(n).normalized()
        v=[p+d*a*l+s*b*w+n*h for a,b,h in [(0,0,0),(.4,-.5,0),(1,0,-.005),(.4,.5,0),(.45,0,.008)]]
        # Folded solid leaf, front and back, usable with an ordinary opaque shader.
        v+= [q-n*.002 for q in v]
        self.add(v,[(0,1,2,4),(0,4,2,3),(5,9,7,6),(5,8,7,9)],c)
    def stem(self,a,b,r,c=21,sides=4):
        a,b=Vector(a),Vector(b);d=(b-a).normalized();s=d.cross(Vector((0,1,0))).normalized();n=d.cross(s);v=[]
        for p,rr in [(a,r),(b,r*.65)]:
            for i in range(sides):v.append(p+rr*(s*math.cos(i*math.tau/sides)+n*math.sin(i*math.tau/sides)))
        self.add(v,[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]+[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)],c)
    def bud(self,p,r,c):
        p=Vector(p);v=[p+Vector((0,0,r)),p+Vector((0,0,-r))]+[p+Vector((r*math.cos(i*math.tau/5),r*math.sin(i*math.tau/5),0)) for i in range(5)]
        self.add(v,[(0,2+i,2+(i+1)%5) for i in range(5)]+[(1,2+(i+1)%5,2+i) for i in range(5)],c)
def object_from(name,g):
    bottom=min(p[2] for p in g.v);g.v=[(x,y,z-bottom) for x,y,z in g.v]
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(g.v,[],g.f);me.update();me.materials.append(mat)
    uv=me.uv_layers.new(name='PaletteUV')
    for face,c in zip(me.polygons,g.c):
        for li in face.loop_indices:uv.data[li].uv=((c%6+.5)/6,(c//6+.5)/4)
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);return ob
# Rounded clipped hedge core, covered by dense small folded leaves.
bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,.23));core=bpy.context.object;core.dimensions=(1.94,.43,.43)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);mod=core.modifiers.new('Clipped rounded corners','BEVEL');mod.width=.07;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
g=Geo();g.add([v.co for v in core.data.vertices],[tuple(p.vertices) for p in core.data.polygons],3);bpy.data.objects.remove(core,do_unlink=True)
for i in range(650):
    side=random.choice(['top','front','back','left','right'])
    x=random.uniform(-.96,.96);y=random.uniform(-.21,.21);z=random.uniform(.10,.43)
    if side=='top':z=random.uniform(.455,.48);n=Vector((0,0,1));d=Vector((math.cos(i),math.sin(i),random.uniform(-.06,.06)))
    elif side in ['front','back']:
        y=(-1 if side=='front' else 1)*random.uniform(.225,.245);n=Vector((0,-1 if side=='front' else 1,0));d=Vector((random.choice([-1,1]),0,random.uniform(-.25,.25)))
    else:
        x=(-1 if side=='left' else 1)*random.uniform(.98,1.0);n=Vector((-1 if side=='left' else 1,0,0));d=Vector((0,random.choice([-1,1]),random.uniform(-.25,.25)))
    g.leaf((x,y,z),d,n,random.uniform(.14,.20),random.uniform(.085,.12),random.randrange(2,7))
hedge=object_from('Hedge_Segment',g)
# Mixed low rosettes, pink and white five-petal flowers, and purple spikes.
g=Geo()
for i in range(48):
    a=random.uniform(0,math.tau);r=random.uniform(.02,.28);p=(r*math.cos(a),r*math.sin(a),random.uniform(.02,.17))
    g.leaf(p,(math.cos(a),math.sin(a),random.uniform(.35,.8)),(0,0,1),random.uniform(.12,.22),random.uniform(.05,.095),random.randrange(2,7))
for i in range(9):
    a=i*2.399;r=random.uniform(.03,.27);p=Vector((math.cos(a)*r,math.sin(a)*r,random.uniform(.27,.48)))
    g.stem((p.x*.6,p.y*.6,0),p,.006)
    normal=Vector((math.cos(a)*.36,math.sin(a)*.36,1)).normalized();u=normal.cross(Vector((0,1,0))).normalized();v=normal.cross(u)
    for j in range(5):
        phi=j*math.tau/5;d=u*math.cos(phi)+v*math.sin(phi);s=normal.cross(d)
        verts=[p+d*ll+s*ww+normal*hh for ll,ww,hh in [(0,-.008,0),(0,.008,0),(.038,.023,.008),(.066,.012,.015),(.066,-.012,.015),(.038,-.023,.008)]]
        verts +=[q-normal*.004 for q in verts]
        faces=[(0,1,2,5),(5,2,3,4),(6,11,8,7),(11,10,9,8)]+[(k,(k+1)%6,(k+1)%6+6,k+6) for k in range(6)]
        g.add(verts,faces,random.choice([8,9,10,11]) if i%3 else random.choice([13,14]))
    g.bud(p+normal*.008,.017,15)
for i in range(6):
    a=i*2.2;r=random.uniform(.08,.25);h=random.uniform(.45,.68);p=Vector((math.cos(a)*r,math.sin(a)*r,h));g.stem((p.x*.7,p.y*.7,0),p,.0055)
    for j in range(7):
        zz=h-.16+j*.025
        for k in range(3):
            phi=k*math.tau/3+j*.5;g.bud((p.x+.022*math.cos(phi),p.y+.022*math.sin(phi),zz),.016 if j<5 else .013,random.randrange(17,21))
flowers=object_from('Mixed_Flower_Cluster',g)
root=bpy.data.objects.new('Option3_BushesAndFlowers',None);bpy.context.collection.objects.link(root)
pieces=[];rotated_meshes={}
def instance(source,name,x,y,angle=0):
    ob=bpy.data.objects.new(name,source.data);bpy.context.collection.objects.link(ob);ob.parent=root;ob.location=(x,y,0);ob.rotation_euler.z=angle
    # Rotation applied to an independent geometry copy; stable FBX conversion.
    if angle:
        from mathutils import Matrix
        key=(source.data.name,angle)
        if key not in rotated_meshes:
            data=source.data.copy();data.transform(Matrix.Rotation(angle,4,'Z'));rotated_meshes[key]=data
        ob.data=rotated_meshes[key];ob.rotation_euler.z=0
    pieces.append(ob);return ob
for side,y in [('Front',-5.05),('Back',5.05)]:
    for i,x in enumerate([-5,-3,-1,1,3,5]):instance(hedge,f'Hedge_{side}_{i+1:02d}',x,y)
for side,x in [('Left',-6.25),('Right',6.25)]:
    for i,y in enumerate([-4,-2,0,2,4]):instance(hedge,f'Hedge_{side}_{i+1:02d}',x,y,math.pi/2)
positions=[(x,y) for x in [-6.25,0,6.25] for y in [-5.05,5.05]]+[(x,y) for x in [-6.25,6.25] for y in [-2.5,0,2.5]]
for i,(x,y) in enumerate(positions):instance(flowers,f'Flowers_{i+1:02d}',x,y)
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
def export(name,obs):
    select(obs);bpy.ops.export_scene.fbx(filepath=str(OUT/name),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,bake_anim=False,path_mode='COPY',embed_textures=True)
export('Option3_BushesAndFlowers.fbx',[root]+pieces)
export('Hedge_Segment.fbx',[hedge]);export('Mixed_Flower_Cluster.fbx',[flowers])
hedge.hide_render=True;flowers.hide_render=True
select([root]+pieces);bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Option3_Foliage.blend'))
def stats(obs):
    tris=0;zero=0
    for o in obs:
        if o.type!='MESH':continue
        o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);zero+=sum(p.area<1e-12 for p in o.data.polygons)
    return dict(triangles=tris,zero_area_faces=zero,mesh_objects=sum(o.type=='MESH' for o in obs))
before=stats(pieces);old=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(OUT/'Option3_BushesAndFlowers.fbx'));imported=list(set(scene.objects)-old);assert stats(imported)==before
for o in imported:bpy.data.objects.remove(o,do_unlink=True)
report=dict(**before,fbx_roundtrip='PASS',hedge_modules=22,flower_clusters=12,hedge_height_m=.50,flower_height_m=.70,clear_inner_area_m=[12,9.4],foliage_only=True)
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))
# Preview-only floor, camera and lighting.
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.world.color=(.16,.18,.22)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015));floor=bpy.context.object;m=bpy.data.materials.new('StudioGround');m.diffuse_color=(.10,.12,.14,1);floor.data.materials.append(m)
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for pos,power,size in [((-5,-6,12),2500,8),((6,2,10),1700,7)]:
    bpy.ops.object.light_add(type='AREA',location=pos);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.size=size;aim(lamp,(0,0,0))
bpy.ops.object.camera_add(location=(11,-15,13));camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=16;aim(camera,(0,0,.15))
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.filepath=str(SRC/'Option3_Foliage_Preview.png');bpy.ops.render.render(write_still=True)
# A close view makes the flower species and clipped foliage easy to inspect.
for ob in pieces:ob.hide_render=True
hedge.hide_render=False;flowers.hide_render=False;hedge.location=(-.35,.38,0);flowers.location=(.35,-.20,0)
camera.location=(2.3,-3.6,2.1);camera.data.ortho_scale=2.8;aim(camera,(0,0,.28));scene.render.resolution_x=1100;scene.render.resolution_y=900
scene.render.filepath=str(SRC/'Option3_Foliage_Closeup.png');bpy.ops.render.render(write_still=True)
print('OPTION3_FOLIAGE_PASS '+json.dumps(report))
