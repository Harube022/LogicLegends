"""Create a matching three-slot Sets spawn pedestal, Blender 4.5."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/Art/SetsGameplay/SpawnPedestal';SRC=ROOT/'SourceAssets/SetsGameplay/SpawnPedestal'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.context.preferences.filepaths.save_version=0
palette=[(.15,.16,.185),(.22,.225,.24),(.31,.29,.265),(.38,.35,.30),(.27,.27,.275),(.43,.39,.33),(.09,.115,.16),(.12,.15,.20),(.35,.25,.10),(.52,.37,.14),(.19,.20,.225),(.055,.08,.12),(.015,.32,1),(.055,.62,1),(.43,.79,1),(.7,.92,1)]*4
image=bpy.data.images.new('SpawnPedestal_Palette',width=64,height=64,alpha=False);pixels=[]
for y in range(64):
    for x in range(64):pixels.extend((*palette[y//8*8+x//8],1))
image.pixels=pixels;image.filepath_raw=str(OUT/'SpawnPedestal_Palette.png');image.file_format='PNG';image.save();image.pack()
mats=[]
for name,rough,metal,emit in [('Pedestal_Stone',.82,.0,0),('Pedestal_Brass',.45,.55,0),('Pedestal_Glow',.3,.08,3)]:
    m=bpy.data.materials.new(name);m.use_nodes=True;sh=m.node_tree.nodes.get('Principled BSDF');sh.inputs['Roughness'].default_value=rough;sh.inputs['Metallic'].default_value=metal
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest';m.node_tree.links.new(tex.outputs['Color'],sh.inputs['Base Color'])
    if emit:m.node_tree.links.new(tex.outputs['Color'],sh.inputs['Emission Color']);sh.inputs['Emission Strength'].default_value=emit
    mats.append(m)
class G:
    def __init__(self):self.v=[];self.f=[];self.c=[]
    def add(self,v,f,c):
        s=len(self.v);self.v.extend(Vector(q) for q in v);self.f.extend(tuple(s+i for i in ff) for ff in f);self.c.extend(c if isinstance(c,list) else [c]*len(f))
    def profile(self,outline,layers,col,caps=True,center=(0,0)):
        N=len(outline);v=[(x*scale+center[0],y*scale+center[1],z) for z,scale in layers for x,y in outline];f=[];colors=[]
        for row in range(len(layers)-1):
            for k in range(N):f.append((row*N+k,row*N+(k+1)%N,(row+1)*N+(k+1)%N,(row+1)*N+k));colors.append(col[row%len(col)] if isinstance(col,list) else col)
        if caps:f.extend([tuple(reversed(range(N))),tuple(range((len(layers)-1)*N,len(layers)*N))]);colors.extend([col[0],col[-1]] if isinstance(col,list) else [col,col])
        self.add(v,f,colors)
    def ring(self,outline,outer,inner,z,col,center=(0,0)):
        N=len(outline);v=[(x*s+center[0],y*s+center[1],z) for s in [outer,inner] for x,y in outline]
        self.add(v,[(k,(k+1)%N,(k+1)%N+N,k+N) for k in range(N)],col)
root=bpy.data.objects.new('Element_Spawn_Pedestal',None);bpy.context.collection.objects.link(root);parts=[]
def make(name,g,mat=0):
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(g.v,[],g.f);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);ob.parent=root;me.materials.append(mats[mat]);uv=me.uv_layers.new(name='PaletteUV')
    for face,col in zip(me.polygons,g.c):
        for li in face.loop_indices:uv.data[li].uv=((col%8+.5)/8,(col//8+.5)/8)
    parts.append(ob);return ob
outline=[]
for center,start in [(.56,-math.pi/2),(-.56,math.pi/2)]:
    for k in range(13):a=start+k*math.pi/12;outline.append((center+.60*math.cos(a),.60*math.sin(a)))
g=G();g.profile(outline,[(0,.96),(.035,1),(.10,1),(.14,.94)],[0,1,2]);make('Pedestal_Foundation',g)
g=G();g.profile(outline,[(.14,.88),(.18,.86),(.34,.86),(.39,.92)],[1,6,2]);make('Pedestal_RecessedBody',g)
g=G();g.profile(outline,[(.39,.98),(.43,1),(.49,.98)],[2,3,5]);make('Pedestal_TopSlab',g)
# Horizontal bands follow the chamfered oval instead of requiring torus geometry.
g=G();g.profile(outline,[(.195,.863),(.229,.863)],12,False);g.ring(outline,.982,.956,.491,4);make('Pedestal_BlueBand',g,2)
# Correct the stone upper-surface rim to a dedicated stone mesh.
band=parts[-1];band.data.materials.append(mats[0])
for face in list(band.data.polygons)[len(outline):]:face.material_index=1
g=G();g.profile(outline,[(.16,.881),(.179,.881)],8,False);g.profile(outline,[(.347,.874),(.365,.892)],9,False);make('Pedestal_BrassTrim',g,1)

def circle(n,r):return [(r*math.cos(k*math.tau/n),r*math.sin(k*math.tau/n)) for k in range(n)]
anchors=[]
for i,x in enumerate([-.70,0,.70],1):
    g=G();g.profile(circle(16,.295),[(.491,1),(.513,1),(.536,.91)],[1,3,4],center=(x,0));make(f'SpawnSocket_{i}_Stone',g)
    g=G();g.ring(circle(24,.245),1,.91,.539,9,center=(x,0));make(f'SpawnSocket_{i}_BrassRing',g,1)
    g=G();g.ring(circle(32,.218),1,.92,.540,13,center=(x,0));make(f'SpawnSocket_{i}_GlowRing',g,2)
    # Dark inset remains visible when no element is present.
    g=G();g.profile(circle(16,.190),[(.536,1),(.539,1)],7,center=(x,0));make(f'SpawnSocket_{i}_Inset',g)
    ob=bpy.data.objects.new(f'Spawn_Point_{i:02d}',None);bpy.context.collection.objects.link(ob);ob.parent=root;ob.location=(x,0,.542);ob.empty_display_type='PLAIN_AXES';ob.empty_display_size=.10;anchors.append(ob)
# Four small brass diamond inlays keep the surface ornamental without fixed values.
g=G()
for x,y in [(-.35,-.39),(.35,-.39),(-.35,.39),(.35,.39)]:g.add([(x-.045,y,.493),(x,y-.075,.493),(x+.045,y,.493),(x,y+.075,.493)],[(0,1,2,3)],9)
make('Pedestal_DiamondInlays',g,1)
# Front and rear plaques have a tiny blue emblem, no numbered or gameplay text.
g=G();glow=G()
for sy in [-1,1]:
    y=sy*.521
    facing=(3,2,1,0) if sy<0 else (0,1,2,3)
    g.add([(-.14,y,.26),(0,y,.325),(.14,y,.26),(0,y,.245)],[facing],8)
    glow.add([(-.055,y+sy*.002,.278),(0,y+sy*.002,.308),(.055,y+sy*.002,.278),(0,y+sy*.002,.253)],[facing],13)
make('Pedestal_FrontRearPlaques',g,1);make('Pedestal_FrontRearEmblems',glow,2)

def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs:ob.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in obs if o.type=='MESH')
def measure(obs):
    result={'vertices':0,'faces':0,'triangles':0,'mesh_objects':0,'zero_area_faces':0,'loose_vertices':0}
    for o in obs:
        if o.type!='MESH':continue
        m=o.data;m.calc_loop_triangles();result['vertices']+=len(m.vertices);result['faces']+=len(m.polygons);result['triangles']+=len(m.loop_triangles);result['mesh_objects']+=1
        bm=bmesh.new();bm.from_mesh(m);result['zero_area_faces']+=sum(f.calc_area()<1e-11 for f in bm.faces);result['loose_vertices']+=sum(not v.link_faces for v in bm.verts);bm.free()
    return result
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
report=measure(parts);report.update({'dimensions_unity_xyz':[2.32,.540,1.20],'spawn_points_local_unity_xyz':[[-.70,.542,0],[0,.542,0],[.70,.542,0]],'materials':3,'modifiers':0})
assert not report['zero_area_faces'] and not report['loose_vertices']
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_distance=4;area.spaces.active.region_3d.view_location=Vector((0,0,.25));area.spaces.active.shading.type='MATERIAL'
select(parts+[root]+anchors)
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Element_Spawn_Pedestal.blend'))
report['source_mesh_objects']=len(parts)
# One renderer in the FBX; the Blender source retains editable components.
select(parts);bpy.ops.object.join();combined=bpy.context.object;combined.name='SpawnPedestal_Mesh'
report.update(measure([combined]));report['material_slots']=len(combined.data.materials)
select([combined,root]+anchors)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Element_Spawn_Pedestal.fbx'),use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,path_mode='COPY',embed_textures=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'Element_Spawn_Pedestal.fbx'));obs=list(scene.objects);back=measure(obs)
assert all(back[k]==report[k] for k in back)
assert all(o.type in {'MESH','EMPTY'} and not o.modifiers and all(abs(s-1)<1e-5 for s in o.scale) for o in obs)
assert all(bpy.data.objects.get(f'Spawn_Point_{i:02d}') for i in range(1,4))
report['fbx_roundtrip']='PASS';(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))

# All presentation objects below remain outside the saved/exported asset.
def emission():
    for m in bpy.data.materials:
        if any(m.name.startswith(n) for n in ['Pedestal_Glow','Sets_Glow']) and m.use_nodes:
            p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Emission Strength'].default_value=2
            tex=next((n for n in m.node_tree.nodes if n.type=='TEX_IMAGE'),None)
            if tex:m.node_tree.links.new(tex.outputs['Color'],p.inputs['Emission Color'])
emission();scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.world.color=(.065,.075,.10)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.01));floor=bpy.context.object;m=bpy.data.materials.new('PreviewFloor');m.diffuse_color=(.09,.105,.13,1);floor.data.materials.append(m)
for p,e,size in [((0,-3,5),650,4),((-3,2,4),850,4),((3,2,3),450,3)]:
    bpy.ops.object.light_add(type='AREA',location=p);ob=bpy.context.object;ob.data.energy=e;ob.data.shape='DISK';ob.data.size=size;ob.rotation_euler=(Vector((0,0,.25))-ob.location).to_track_quat('-Z','Y').to_euler()
scene.use_nodes=True;n=scene.node_tree.nodes;n.clear();rl=n.new('CompositorNodeRLayers');gl=n.new('CompositorNodeGlare');gl.glare_type='FOG_GLOW';gl.threshold=1.5;gl.quality='HIGH';co=n.new('CompositorNodeComposite');scene.node_tree.links.new(rl.outputs['Image'],gl.inputs['Image']);scene.node_tree.links.new(gl.outputs['Image'],co.inputs['Image'])
bpy.ops.object.camera_add(location=(2.8,-4.8,3.1));cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=3.4;cam.rotation_euler=(Vector((0,0,.25))-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
scene.render.filepath=str(SRC/'Element_Spawn_Pedestal_Preview.png');bpy.ops.render.render(write_still=True)
cam.location=(0,0,5);cam.rotation_euler=(0,0,0);scene.render.filepath=str(SRC/'Element_Spawn_Pedestal_Top.png');bpy.ops.render.render(write_still=True)
# Compatibility preview only: existing cubes are not duplicated into pedestal files.
for digit,x in [(1,-.70),(2,0),(3,.70)]:
    path=ROOT/'Assets/Art/SetsGameplay'/('Element_Object.fbx' if digit==1 else f'Elements2To9/Element_Object_{digit}.fbx')
    if path.exists():
        before=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(path));new=set(scene.objects)-before
        element_root=next(o for o in new if o.parent is None);element_root.location=(x,0,.542)
emission();cam.location=(2.8,-4.8,3.1);cam.data.ortho_scale=3.6;cam.rotation_euler=(Vector((0,0,.65))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(SRC/'Element_Spawn_Pedestal_WithElements.png');bpy.ops.render.render(write_still=True)
print('PEDESTAL_REPORT '+json.dumps(report))
