"""Static Venn floor, universal boundary, reusable element; Blender 4.5."""
import bpy,bmesh,math,json,random
from mathutils import Vector,Matrix
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/Art/SetsGameplay';SRC=ROOT/'SourceAssets/SetsGameplay'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0;random.seed(105)
palette=[(.017,.055,.13),(.024,.075,.17),(.021,.066,.15),(.025,.045,.085),
 (.13,.022,.046),(.17,.032,.065),(.15,.025,.054),(.07,.027,.037),
 (.082,.034,.15),(.105,.045,.19),(.095,.041,.17),(.06,.027,.092),
 (.055,.065,.085),(.12,.15,.19),(.24,.22,.17),(.42,.32,.14),
 (.014,.30,1),(.04,.62,1),(1,.025,.13),(.95,.09,.26),
 (.48,.13,1),(.73,.39,1),(.63,.87,1),(.95,.95,1),
 (.025,.20,.44),(.038,.29,.61),(.03,.25,.53),(.018,.12,.29),
 (.3,.40,.52),(.36,.49,.62),(.10,.15,.22),(.035,.048,.066)]
palette*=2
im=bpy.data.images.new('SetsGameplay_Palette',width=64,height=64,alpha=False);pix=[]
for y in range(64):
    for x in range(64):pix.extend((*palette[(y//8)*8+x//8],1))
im.pixels=pix;im.filepath_raw=str(OUT/'SetsGameplay_Palette.png');im.file_format='PNG';im.save();im.pack()
mats=[]
for name,rough,metal,emit in [('Sets_Surface',.58,.12,0),('Sets_Glow',.3,.12,3.0)]:
    m=bpy.data.materials.new(name);m.use_nodes=True;sh=m.node_tree.nodes.get('Principled BSDF');sh.inputs['Roughness'].default_value=rough;sh.inputs['Metallic'].default_value=metal
    t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=im;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],sh.inputs['Base Color'])
    if emit:m.node_tree.links.new(t.outputs['Color'],sh.inputs['Emission Color']);sh.inputs['Emission Strength'].default_value=emit
    mats.append(m)
class G:
    def __init__(self):self.v=[];self.f=[];self.c=[]
    def add(self,vs,fs,col):
        s=len(self.v);self.v.extend(Vector(v) for v in vs);self.f.extend(tuple(s+i for i in f) for f in fs);self.c.extend(col if isinstance(col,list) else [col]*len(fs))
    def box(self,p,size,col):
        x,y,z=(v*.5 for v in size);p=Vector(p);v=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
        self.add([p+Vector(vv) for vv in v],[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],col)
    def strip(self,points,width,z,col):
        for a,b in zip(points,points[1:]):
            a,b=Vector(a),Vector(b);d=(b-a).normalized();n=Vector((-d.y,d.x))*width*.5
            self.add([(v.x,v.y,z) for v in [a-n,b-n,b+n,a+n]],[(0,1,2,3)],col)
def uvcolor(me,cols):
    for layer in list(me.uv_layers):me.uv_layers.remove(layer)
    uv=me.uv_layers.new(name='PaletteUV')
    for f,c in zip(me.polygons,cols):
        for li in f.loop_indices:uv.data[li].uv=((c%8+.5)/8,(c//8+.5)/8)
def mesh(name,g,material=0,parent=None):
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(g.v,[],g.f);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);me.materials.append(mats[material]);uvcolor(me,g.c);ob.parent=parent;return ob
def empty(name,p=(0,0,0),parent=None):
    ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob);ob.location=p;ob.parent=parent;ob.empty_display_type='PLAIN_AXES';ob.empty_display_size=.1;return ob
def text(name,body,p,size,color,parent,rotation=(0,0,0)):
    cu=bpy.data.curves.new(name,'FONT');cu.body=body;cu.size=size;cu.align_x='CENTER';cu.align_y='CENTER';cu.resolution_u=3;cu.extrude=0
    ob=bpy.data.objects.new(name,cu);bpy.context.collection.objects.link(ob);ob.location=p;ob.rotation_euler=rotation
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH')
    ob=bpy.context.object;bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    ob.data.materials.append(mats[1]);uvcolor(ob.data,[color]*len(ob.data.polygons));ob.parent=parent;return ob

venn=empty('Venn_Diagram_Floor');boundary=empty('Universal_Set_Boundary');element=empty('Element_Object')
r=2.35;d=1.55;alpha=math.acos(d/r)
def arc(cx,a,b,n=56):return [(cx+r*math.cos(a+(b-a)*k/n),r*math.sin(a+(b-a)*k/n)) for k in range(n+1)]
def clean(poly):
    out=[]
    for p in poly:
        if not out or (Vector(p)-Vector(out[-1])).length>1e-7:out.append(p)
    if len(out)>1 and (Vector(out[0])-Vector(out[-1])).length<1e-7:out.pop()
    return out
aonly=clean(arc(-d,alpha,math.tau-alpha)+arc(d,math.pi+alpha,math.pi-alpha,28))
bonly=[(-x,y) for x,y in reversed(aonly)]
inter=clean(arc(-d,-alpha,alpha,28)+arc(d,math.pi-alpha,math.pi+alpha,28))
def clip(poly,axis,edge,greater):
    result=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        aa=(a[axis]>=edge) if greater else (a[axis]<=edge);bb=(b[axis]>=edge) if greater else (b[axis]<=edge)
        if aa:result.append(a)
        if aa!=bb:
            t=(edge-a[axis])/(b[axis]-a[axis]);result.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])))
    return clean(result)
def area(poly):return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(poly,poly[1:]+poly[:1]))*.5) if len(poly)>2 else 0
# A single, non-overlapping surface per region; no stacked transparent disks.
for name,poly,c in [('Region_A_Only',aonly,0),('Region_Intersection',inter,8),('Region_B_Only',bonly,4)]:
    g=G()
    for ix in range(24):
        for iy in range(16):
            x=-4.2+ix*.35;y=-2.8+iy*.35;p=poly[:]
            for ax,ed,gt in [(0,x+.005,True),(0,x+.345,False),(1,y+.005,True),(1,y+.345,False)]:
                if p:p=clip(p,ax,ed,gt)
            if area(p)<.000003:continue
            # Opaque muted color panels with fine stone-like grid joints.
            g.add([(xx,yy,.003) for xx,yy in p],[tuple(range(len(p)))],c+random.randrange(3))
    mesh(name,g,0,venn)
for cx,label,color in [(-d,'A',16),(d,'B',18)]:
    g=G();under=G();N=128
    for k in range(N):
        a=k*math.tau/N;b=(k+1)*math.tau/N;mid=(a+b)/2;x=cx+r*math.cos(mid);y=r*math.sin(mid)
        other=-cx;inside=(x-other)**2+y*y<r*r;col=20 if inside else color
        for builder,w,z,c in [(under,.085,.006,12),(g,.032,.010,col)]:
            vs=[(cx+rr*math.cos(t),rr*math.sin(t),z) for t,rr in [(a,r-w/2),(a,r+w/2),(b,r+w/2),(b,r-w/2)]]
            builder.add(vs,[(0,1,2,3)],c)
    mesh('Circle_'+label+'_Rim',under,0,venn);mesh('Circle_'+label+'_Light',g,1,venn)
text('Label_A','A',(-2.40,0,.018),.63,17,venn);text('Label_B','B',(2.40,0,.018),.63,19,venn)
text('Label_Intersection_A','A',(-.47,0,.018),.34,23,venn);text('Label_Intersection_B','B',(.47,0,.018),.34,23,venn)
g=G();points=[(-.15,-.13),(-.15,.01)]+[(.15*math.cos(math.pi-k*math.pi/20),.01+.15*math.sin(math.pi-k*math.pi/20)) for k in range(1,21)]+[(.15,-.13)]
g.strip(points,.036,.018,23);mesh('Label_Intersection_Symbol',g,1,venn)

# Closed rectangular frame, with mitered corners; actual outside-A/B space stays clear.
W,H=9.60,7.0
g=G();base=G();trim=G()
def rectangular_ring(builder,w,h,width,z,col):
    outer=[(-w/2,-h/2),(w/2,-h/2),(w/2,h/2),(-w/2,h/2)];inner=[(-w/2+width,-h/2+width),(w/2-width,-h/2+width),(w/2-width,h/2-width),(-w/2+width,h/2-width)]
    builder.add([(x,y,z) for x,y in outer+inner],[(k,(k+1)%4,(k+1)%4+4,k+4) for k in range(4)],col)
rectangular_ring(base,W,H,.19,.004,12)
rectangular_ring(trim,W-.025,H-.025,.025,.008,14)
rectangular_ring(g,W-.12,H-.12,.036,.013,16)
rectangular_ring(trim,W-.32,H-.32,.018,.008,13)
mesh('Boundary_StoneFrame',base,0,boundary);mesh('Boundary_Inlay',trim,0,boundary);mesh('Boundary_Light',g,1,boundary)
text('Label_Universal_U','U',(4.02,-2.85,.022),.67,17,boundary)
g=G()
for sx in [-1,1]:
    for sy in [-1,1]:
        x=sx*4.35;y=sy*3.07
        g.add([(x-.10,y,.012),(x,y-.14,.012),(x+.10,y,.012),(x,y+.14,.012)],[(0,1,2,3)],15)
mesh('Boundary_CornerSigils',g,0,boundary)

# One beveled opaque blue core and a lightweight luminous cube frame.
# Separate sample number can be disabled for TMP/world-space UI at LabelAnchor.
g=G();g.box((0,0,.27),(.40,.40,.40),24);core=mesh('Element_Core',g,0,element)
bm=bmesh.new();bm.from_mesh(core.data);bmesh.ops.bevel(bm,geom=list(bm.edges),offset=.025,segments=1,affect='EDGES');bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(core.data);bm.free()
for uv in list(core.data.uv_layers):core.data.uv_layers.remove(uv)
uvcolor(core.data,[24+(i%4) for i in range(len(core.data.polygons))])
g=G()
for x in [-.215,.215]:
    for y in [-.215,.215]:g.box((x,y,.27),(.024,.024,.454),17)
for z in [.055,.485]:
    for y in [-.215,.215]:g.box((0,y,z),(.454,.024,.024),17)
    for x in [-.215,.215]:g.box((x,0,z),(.024,.454,.024),17)
mesh('Element_LuminousEdges',g,1,element)
g=G()
for x in [-.225,.225]:
    for y in [-.225,.225]:
        for z in [.045,.495]:g.box((x,y,z),(.055,.055,.055),28)
mesh('Element_CornerCaps',g,0,element)
# A clear numeral 1 with a sloped head and baseline, rather than a plain bar.
g=G();outline=[(-.09,-.12),(.09,-.12),(.09,-.08),(.035,-.08),(.035,.15),(-.02,.15),(-.085,.095),(-.057,.062),(-.018,.094),(-.018,-.08),(-.09,-.08)]
g.add([(x,-.015,.78+z) for x,z in outline],[tuple(range(len(outline)))],23)
sample=mesh('Element_Label_Example_1',g,1,element)
socket=empty('Element_LabelAnchor',(0,-.015,.78),element);socket.rotation_euler=(math.pi/2,0,0)

def descendants(root):return [root]+list(root.children_recursive)
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in obs if o.type=='MESH')
def stats(obs):
    result={'vertices':0,'faces':0,'triangles':0,'meshes':0,'zero_area_faces':0,'loose_vertices':0}
    for o in obs:
        if o.type!='MESH':continue
        m=o.data;m.calc_loop_triangles();result['vertices']+=len(m.vertices);result['faces']+=len(m.polygons);result['triangles']+=len(m.loop_triangles);result['meshes']+=1
        bm=bmesh.new();bm.from_mesh(m);result['zero_area_faces']+=sum(f.calc_area()<1e-11 for f in bm.faces);result['loose_vertices']+=sum(not v.link_faces for v in bm.verts);bm.free()
    return result
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
# Save the editable kit laid out as intended. Element rests in outside-A/B space.
element.location=(3.95,-1.7,.025)
for scr in bpy.data.screens:
    for a in scr.areas:
        if a.type=='VIEW_3D':a.spaces.active.region_3d.view_distance=12;a.spaces.active.region_3d.view_location=Vector((0,0,.2));a.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Sets_Gameplay_Assets.blend'))
report={'assets':{},'dimensions':{'circle_radius':r,'circle_center_offset':d,'venn_width':2*(r+d),'venn_depth':2*r,'boundary_width':W,'boundary_depth':H,'element_cube_width':.505},'modifiers':0,'materials':2}
for root in [venn,boundary,element]:
    original=root.location.copy();root.location=(0,0,0);obs=descendants(root);s=stats(obs);assert not s['zero_area_faces'] and not s['loose_vertices'];report['assets'][root.name]=s
    select(obs);bpy.ops.export_scene.fbx(filepath=str(OUT/(root.name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,path_mode='COPY',embed_textures=True)
    root.location=original
# Roundtrip every delivered FBX independently, preserving original source scene on disk.
for name,expected in report['assets'].items():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(OUT/(name+'.fbx')))
    obs=list(scene.objects);actual=stats(obs)
    assert actual==expected,(name,actual,expected)
    assert all(o.type in {'MESH','EMPTY'} and not o.modifiers and all(abs(v-1)<1e-5 for v in o.scale) for o in obs)
report['fbx_roundtrip']='PASS (all 3 files)'
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))

# Reload actual exports into a neutral preview setting.
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for name in ['Venn_Diagram_Floor','Universal_Set_Boundary','Element_Object']:bpy.ops.import_scene.fbx(filepath=str(OUT/(name+'.fbx')))
bpy.context.view_layer.update()
el=bpy.data.objects.get('Element_Object');el.location=(3.95,-1.7,.025)
# The FBX exports are complete. Preview staging and lights are not saved in the source.
scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True
scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.world.color=(.06,.07,.10)
# Restore simple emission explicitly for studio rendering: FBX does not encode
# a Unity shader or bloom setup. This matches the saved source material.
for m in bpy.data.materials:
    if m.name.startswith('Sets_Glow') and m.use_nodes:
        p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Emission Strength'].default_value=3
        t=next((n for n in m.node_tree.nodes if n.type=='TEX_IMAGE'),None)
        if t:m.node_tree.links.new(t.outputs['Color'],p.inputs['Emission Color'])
stone=bpy.data.materials.new('PreviewGround');stone.diffuse_color=(.09,.11,.14,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.05));ground=bpy.context.object;ground.data.materials.append(stone)
# Underlying arena-style paving remains separate from the deliverable overlays.
g=G()
for x in range(28):
    for y in range(22):g.box((-5.06+x*.375,-3.94+y*.375,-.026),(.362,.362,.036),random.choice([12,13,30,31]))
mesh('Preview_Paving',g)
def light(p,e,size,target):
    bpy.ops.object.light_add(type='AREA',location=p);o=bpy.context.object;o.data.energy=e;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
light((0,-4,8),1000,8,(0,0,0));light((-4,3,6),800,7,(0,0,0))
scene.use_nodes=True;n=scene.node_tree.nodes;n.clear();rl=n.new('CompositorNodeRLayers');gl=n.new('CompositorNodeGlare');gl.glare_type='FOG_GLOW';gl.quality='HIGH';gl.threshold=1.4;co=n.new('CompositorNodeComposite');scene.node_tree.links.new(rl.outputs['Image'],gl.inputs['Image']);scene.node_tree.links.new(gl.outputs['Image'],co.inputs['Image'])
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=11.7
cam.location=(0,-9,11);cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(SRC/'Sets_Gameplay_Preview.png');bpy.ops.render.render(write_still=True)
cam.location=(0,0,12);cam.rotation_euler=(0,0,0);cam.data.ortho_scale=11;scene.render.filepath=str(SRC/'Sets_Gameplay_Top.png');bpy.ops.render.render(write_still=True)
# Detail view of the same reusable cube with its removable sample number.
cam.data.ortho_scale=1.60;cam.location=el.location+Vector((1.2,-2.8,1.65));cam.rotation_euler=(el.location+Vector((0,0,.45))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(SRC/'Element_Object_Preview.png');bpy.ops.render.render(write_still=True)
print('SETS_GAMEPLAY_REPORT '+json.dumps(report))
