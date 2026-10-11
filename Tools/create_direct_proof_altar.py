"""Create a low-poly direct-proof altar from the supplied chamber reference."""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Art/ProofChamber/DirectProofAltar'
SRC = ROOT / 'SourceAssets/ProofChamber/DirectProofAltar'
OUT.mkdir(parents=True, exist_ok=True)
SRC.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0

# A shared palette gives small facets and markings some variation without
# producing a separate draw submission for every stone or crystal.
colors = [
    (.10,.12,.16),(.15,.17,.20),(.20,.22,.24),(.26,.27,.27),
    (.33,.32,.29),(.39,.36,.31),(.075,.09,.125),(.24,.25,.27),
    (.27,.19,.085),(.42,.29,.105),(.62,.45,.18),(.15,.12,.075),
    (.012,.28,.76),(.025,.48,1),(.23,.73,1),(.64,.89,1),
    (.12,.24,.47),(.08,.37,.72),(.72,.92,1),(.92,.97,1),
    (.08,.14,.23),(.13,.20,.30),(.47,.43,.34),(.025,.065,.11),
    (.17,.38,.70),(.34,.69,.95),(.27,.58,.93),(.025,.18,.42),
    (.32,.26,.14),(.55,.42,.21),(.11,.13,.17),(.035,.08,.16),
]*2
palette = bpy.data.images.new('DirectProof_Palette', width=64, height=64, alpha=False)
pixels=[]
for yy in range(64):
    for xx in range(64):pixels.extend((*colors[(yy//8)*8+xx//8],1))
palette.pixels=pixels
palette.filepath_raw=str(OUT/'DirectProof_Palette.png')
palette.file_format='PNG'
palette.save()
palette.pack()
materials=[]
for name,rough,metal,emission in [
    ('DirectAltar_Stone',.82,0,0),
    ('DirectAltar_Brass',.39,.56,0),
    ('DirectAltar_BlueGlow',.31,.08,2.7),
]:
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value=rough
    bsdf.inputs['Metallic'].default_value=metal
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=palette;tex.interpolation='Closest'
    mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    if emission:
        mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value=emission
    materials.append(mat)

class Geo:
    def __init__(self):self.verts=[];self.faces=[];self.colors=[]
    def add(self,verts,faces,color):
        offset=len(self.verts)
        self.verts.extend(Vector(v) for v in verts)
        self.faces.extend(tuple(offset+i for i in f) for f in faces)
        self.colors.extend(color if isinstance(color,list) else [color]*len(faces))
    def box(self,center,size,color,chamfer=0):
        x,y,z=(v/2 for v in size);px,py,pz=center
        b=min(chamfer,x*.45,y*.45,z*.7)
        if b:
            v=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),
               (-x,-y,z-b),(x,-y,z-b),(x,y,z-b),(-x,y,z-b),
               (-x+b,-y+b,z),(x-b,-y+b,z),(x-b,y-b,z),(-x+b,y-b,z)]
            f=[(3,2,1,0),(8,9,10,11)]
            for i in range(4):
                j=(i+1)%4;f.extend([(i,j,j+4,i+4),(i+4,j+4,j+8,i+8)])
        else:
            v=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),
               (-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
            f=[(3,2,1,0),(4,5,6,7),(0,1,5,4),
               (1,2,6,5),(2,3,7,6),(3,0,4,7)]
        self.add([(px+a,py+c,pz+d) for a,c,d in v],f,color)
    def ring(self,cx,cy,r_outer,r_inner,z,color,n=16):
        v=[]
        for r in [r_outer,r_inner]:
            for i in range(n):
                a=i*math.tau/n
                v.append((cx+r*math.cos(a),cy+r*math.sin(a),z))
        self.add(v,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],color)
    def shaft(self,cx,cy,z0,width,height,tip_shift,seed):
        n=6;v=[];facet=[12,13,14,15,16,17,18,25,26]
        for t,r in [(0,.63),(.39,1),(.76,.82)]:
            for i in range(n):
                a=i*math.tau/n+seed*.27
                v.append((cx+width*.5*r*math.cos(a)+tip_shift*t,
                          cy+width*.42*r*math.sin(a),z0+height*t))
        v.append((cx+tip_shift,cy-.012*seed,z0+height))
        f=[];cols=[]
        for level in range(2):
            for i in range(n):
                j=(i+1)%n;f.append((level*n+i,level*n+j,(level+1)*n+j,(level+1)*n+i))
                cols.append(facet[(i+seed+level*3)%len(facet)])
        for i in range(n):
            f.append((12+i,12+(i+1)%n,18))
            cols.append(facet[(i+seed+4)%len(facet)])
        f.append(tuple(reversed(range(n))));cols.append(16)
        self.add(v,f,cols)

root=bpy.data.objects.new('Direct_Proof_Altar',None)
bpy.context.collection.objects.link(root)
parts=[]
anchors=[]
def mesh(name,geo,matid=0):
    me=bpy.data.meshes.new(name+'_Mesh')
    me.from_pydata(geo.verts,[],geo.faces);me.update()
    ob=bpy.data.objects.new(name,me)
    bpy.context.collection.objects.link(ob);ob.parent=root
    me.materials.append(materials[matid])
    uv=me.uv_layers.new(name='PaletteUV')
    for face,color in zip(me.polygons,geo.colors):
        u=((color%8)+.5)/8;v=((color//8)+.5)/8
        for li in face.loop_indices:uv.data[li].uv=(u,v)
    parts.append(ob)
    return ob
def anchor(name,loc):
    ob=bpy.data.objects.new(name,None)
    bpy.context.collection.objects.link(ob)
    ob.parent=root;ob.location=loc
    ob.empty_display_type='PLAIN_AXES';ob.empty_display_size=.09
    anchors.append(ob)

# Dark stone foundation and raised, broad working table. The front edge carries
# five numbered bays while the top remains clear for proof statement tablets.
g=Geo();g.box((0,0,.07),(3.86,1.35,.14),0,.035);mesh('Foundation_Step',g)
g=Geo();g.box((0,0,.19),(3.72,1.22,.17),2,.028);mesh('Foundation_UpperStep',g)
g=Geo()
for x in [-1.59,1.59]:
    g.box((x,-.02,.51),(.48,1.05,.57),1,.035)
    g.box((x,-.02,.805),(.57,1.11,.08),4,.018)
mesh('Side_Piers',g)
g=Geo();g.box((0,.105,.55),(2.79,.99,.56),6,.045);mesh('Recessed_Core',g)
g=Geo();g.box((0,-.605,.79),(3.56,.105,.49),1,.018);mesh('Numbered_FrontFascia',g)
g=Geo();g.box((0,0,1.055),(3.82,1.29,.15),3,.036);mesh('Worktop_Slab',g)
g=Geo();g.box((0,-.593,1.15),(3.90,.095,.035),9,.008);mesh('Worktop_FrontBrassEdge',g,1)
g=Geo();g.box((0,-.632,1.178),(3.64,.034,.020),13,.003);mesh('Worktop_FrontBlueLine',g,2)
g=Geo()
for x in [-1.91,1.91]:
    for y in [-.51,.51]:g.box((x,y,1.17),(.15,.17,.15),8,.022)
mesh('Worktop_CornerCaps',g,1)

# Each bay is a shallow horizontal docking panel and has an exported anchor.
for i,x in enumerate([-1.30,-.65,0,.65,1.30],1):
    g=Geo();g.box((x,-.075,1.139),(.55,.66,.021),20,.009)
    mesh(f'ProofSlot_{i:02d}_Inset',g)
    g=Geo();g.box((x,-.075,1.152),(.54,.012,.012),14)
    g.box((x,-.40,1.152),(.54,.012,.012),14)
    for xx in [x-.265,x+.265]:g.box((xx,-.237,1.152),(.012,.338,.012),14)
    mesh(f'ProofSlot_{i:02d}_BlueBorder',g,2)
    anchor(f'Proof_Step_{i:02d}',(x,-.22,1.165))
    # Number badges are on the front, not the statement placing surface.
    g=Geo();g.box((x,-.672,.82),(.57,.035,.31),0,.012)
    mesh(f'Front_NumberPlaque_{i:02d}',g)
    g=Geo();g.box((x,-.694,.823),(.52,.010,.255),27,.008)
    mesh(f'Front_NumberPlaque_{i:02d}_BlueInset',g)

# Bold mesh numerals are packed into the FBX; no Unity font is needed for 1–5.
font=bpy.data.fonts.load('C:/Windows/Fonts/arialbd.ttf')
for i,x in enumerate([-1.30,-.65,0,.65,1.30],1):
    cu=bpy.data.curves.new(f'Numeral_{i}','FONT')
    cu.font=font;cu.body=str(i);cu.size=.26
    cu.align_x='CENTER';cu.align_y='CENTER';cu.resolution_u=3;cu.extrude=.002
    ob=bpy.data.objects.new(f'Front_Number_{i}',cu)
    bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.convert(target='MESH');ob=bpy.context.object
    vv=ob.data.vertices
    lo=Vector(tuple(min(v.co[k] for v in vv) for k in range(3)))
    hi=Vector(tuple(max(v.co[k] for v in vv) for k in range(3)))
    center=(lo+hi)*.5;scale=.20/(hi.y-lo.y)
    for v in vv:
        q=(v.co-center)*scale
        v.co=(q.x,-q.z,q.y)
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
    bad=[f for f in bm.faces if f.calc_area()<1e-11]
    if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
    loose=[v for v in bm.verts if not v.link_faces]
    if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bm.to_mesh(ob.data);bm.free()
    ob.location=(x,-.703,.82);ob.parent=root
    ob.data.materials.append(materials[2])
    for layer in list(ob.data.uv_layers):ob.data.uv_layers.remove(layer)
    uv=ob.data.uv_layers.new(name='PaletteUV')
    for loop in uv.data:loop.uv=((19%8+.5)/8,(19//8+.5)/8)
    parts.append(ob)

# Inset blue side bands and a central front gem make the altar recognizable
# even without tablets placed on the working surface.
g=Geo()
for x in [-1.82,1.82]:g.box((x,-.53,.54),(.045,.035,.41),13,.005)
mesh('Side_Pier_BlueGlyphs',g,2)
g=Geo();g.ring(0,-.66,.245,.199,.45,8,16);mesh('Center_Emblem_BrassRim',g,1)
g=Geo();g.ring(0,-.665,.191,.165,.452,13,16);mesh('Center_Emblem_BlueRim',g,2)
g=Geo()
g.add([(0,-.676,.618),(.145,-.676,.45),(0,-.676,.282),(-.145,-.676,.45)],[(3,2,1,0)],12)
g.add([(0,-.682,.575),(.105,-.682,.45),(0,-.682,.325),(-.105,-.682,.45)],[(3,2,1,0)],14)
mesh('Center_Emblem_FacetedGem',g,2)
g=Geo()
for x in [-1.83,1.83]:
    g.box((x,-.58,.385),(.20,.09,.06),9,.009)
    g.box((x,-.58,.655),(.20,.09,.06),9,.009)
mesh('Side_Pier_BrassDetails',g,1)

# Rearward crystals avoid blocking the proof slots and repeat the blue chamber
# motifs. Each cluster is a single low-poly mesh for simple editing.
for label,x,y,z,w,h in [
    ('Center',0,.505,1.132,.29,.62),
    ('Left',-1.67,.38,1.128,.18,.34),
    ('Right',1.67,.38,1.128,.18,.34),
]:
    g=Geo()
    g.shaft(x,y,z,w,h,.035 if x>=0 else -.035,len(label))
    g.shaft(x-w*.62,y-.025,z,w*.42,h*.54,-.055,len(label)+2)
    g.shaft(x+w*.58,y+.008,z,w*.34,h*.42,.05,len(label)+4)
    mesh(f'BlueCrystal_{label}',g,2)
    g=Geo();g.ring(x,y,w*.72,w*.52,z+.016,9,12)
    mesh(f'BlueCrystal_{label}_BrassFoot',g,1)
g=Geo()
for x in [-1.67,0,1.67]:
    g.box((x,.385,1.151),(.30,.085,.04),13,.008)
mesh('Crystal_BackRail_Glow',g,2)

def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs:ob.select_set(True)
    bpy.context.view_layer.objects.active=next(ob for ob in obs if ob.type=='MESH')
def measure(obs):
    out={'vertices':0,'faces':0,'triangles':0,'mesh_objects':0,'zero_area_faces':0,'loose_vertices':0}
    for ob in obs:
        if ob.type!='MESH':continue
        me=ob.data;me.calc_loop_triangles()
        out['vertices']+=len(me.vertices);out['faces']+=len(me.polygons)
        out['triangles']+=len(me.loop_triangles);out['mesh_objects']+=1
        bm=bmesh.new();bm.from_mesh(me)
        out['zero_area_faces']+=sum(f.calc_area()<1e-11 for f in bm.faces)
        out['loose_vertices']+=sum(not v.link_faces for v in bm.verts)
        bm.free()
    return out

scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
source_counts=measure(parts)
assert source_counts['zero_area_faces']==0 and source_counts['loose_vertices']==0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=5
            area.spaces.active.region_3d.view_location=Vector((0,0,.68))
            area.spaces.active.shading.type='MATERIAL'
select(parts+[root]+anchors)
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Direct_Proof_Altar.blend'))

# One mesh in the FBX reduces renderer count. The Blender source remains
# separated by functional component for future changes.
select(parts);bpy.ops.object.join()
joined=bpy.context.object;joined.name='Direct_Proof_Altar_Mesh'
export_counts=measure([joined])
assert export_counts['vertices']==source_counts['vertices']
assert export_counts['triangles']==source_counts['triangles']
assert len(joined.data.materials)==3
select([joined,root]+anchors)
bpy.ops.export_scene.fbx(
    filepath=str(OUT/'Direct_Proof_Altar.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},use_mesh_modifiers=False,use_triangles=False,
    mesh_smooth_type='OFF',bake_anim=False,axis_forward='-Z',axis_up='Y',
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
    bake_space_transform=True,path_mode='COPY',embed_textures=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'Direct_Proof_Altar.fbx'))
imported=list(scene.objects);back=measure(imported)
assert back==export_counts,(back,export_counts)
assert len([ob for ob in imported if ob.type=='MESH'])==1
assert all(not ob.modifiers and all(abs(s-1)<1e-5 for s in ob.scale) for ob in imported)
assert all(bpy.data.objects.get(f'Proof_Step_{i:02d}') for i in range(1,6))
report={**export_counts,'source_mesh_objects':source_counts['mesh_objects'],
        'material_slots':len(next(ob for ob in imported if ob.type=='MESH').data.materials),
        'modifiers':0,'five_slot_centers_unity_x':[-1.30,-.65,0,.65,1.30],
        'slots_local_unity_y':1.165,
        'fbx_roundtrip':'PASS',
        'zero_area_faces':back['zero_area_faces'],
        'loose_vertices':back['loose_vertices']}
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))

# Preview only: all lighting, camera and backdrop are absent from both model
# files, and are added after the saved source and checked FBX exist.
for m in bpy.data.materials:
    if m.name.startswith('DirectAltar_BlueGlow') and m.use_nodes:
        shader=m.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Emission Strength'].default_value=2.7
        tex=next((node for node in m.node_tree.nodes if node.type=='TEX_IMAGE'),None)
        if tex:m.node_tree.links.new(tex.outputs['Color'],shader.inputs['Emission Color'])
scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True
scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.world.color=(.05,.065,.10)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.014))
ground=bpy.context.object;mat=bpy.data.materials.new('PreviewGround')
mat.diffuse_color=(.060,.080,.12,1);ground.data.materials.append(mat)
for p,power,size in [((-3,-4,5),850,4),((3,1,4),1000,4),((0,4,5),700,4)]:
    bpy.ops.object.light_add(type='AREA',location=p)
    light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,.7))-light.location).to_track_quat('-Z','Y').to_euler()
scene.use_nodes=True;nodes=scene.node_tree.nodes;nodes.clear()
rl=nodes.new('CompositorNodeRLayers');gl=nodes.new('CompositorNodeGlare')
gl.glare_type='FOG_GLOW';gl.threshold=1.6;gl.quality='HIGH'
comp=nodes.new('CompositorNodeComposite')
scene.node_tree.links.new(rl.outputs['Image'],gl.inputs['Image'])
scene.node_tree.links.new(gl.outputs['Image'],comp.inputs['Image'])
bpy.ops.object.camera_add(location=(3.7,-5.4,3.3))
camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=5.0
camera.rotation_euler=(Vector((0,0,.75))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SRC/'Direct_Proof_Altar_Preview.png')
bpy.ops.render.render(write_still=True)
camera.location=(0,0,6);camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=4.8;scene.render.filepath=str(SRC/'Direct_Proof_Altar_Top.png')
bpy.ops.render.render(write_still=True)
print('DIRECT_PROOF_ALTAR_REPORT '+json.dumps(report))
