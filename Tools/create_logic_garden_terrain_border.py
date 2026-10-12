"""Standalone low-poly Logic Garden terrain from the user-provided terrain concept.

Run in Blender 4.5: blender --background --python Tools/create_logic_garden_terrain_border.py
The existing arena, walls, trees, topiary, fountain, and gameplay objects are excluded.
"""
import bpy
import bmesh
import json
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/LogicGardenTerrainBorder'
SRC=ROOT/'SourceAssets/LogicGardenTerrainBorder'
OUT.mkdir(parents=True,exist_ok=True);SRC.mkdir(parents=True,exist_ok=True)
random.seed(20261011)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)

# Shared rock/plant palette. One small UV atlas is used by all sculptural parts.
swatches=[
    (.28,.28,.26),(.32,.32,.29),(.37,.36,.32),(.42,.41,.37),
    (.48,.46,.41),(.54,.51,.45),(.60,.56,.49),(.65,.61,.53),
    (.24,.25,.23),(.29,.30,.27),(.35,.35,.31),(.40,.40,.35),
    (.46,.45,.39),(.51,.49,.42),(.57,.54,.46),(.63,.59,.50),
    (.25,.29,.20),(.32,.36,.25),(.39,.43,.28),(.46,.49,.30),
    (.54,.56,.34),(.31,.39,.22),(.40,.46,.25),(.50,.55,.31),
    (.050,.13,.034),(.078,.18,.046),(.105,.22,.056),(.135,.27,.066),
    (.175,.32,.079),(.225,.38,.105),(.288,.43,.117),(.36,.49,.146),
    (.095,.20,.043),(.135,.26,.054),(.18,.33,.066),(.22,.38,.075),
    (.27,.43,.098),(.33,.48,.113),(.40,.52,.132),(.48,.57,.160),
    (.24,.20,.11),(.35,.27,.13),(.46,.33,.15),(.57,.40,.20),
    (.66,.50,.28),(.75,.59,.37),(.85,.71,.47),(.97,.87,.63),
    (.42,.57,.72),(.55,.72,.88),(.69,.84,.96),(.82,.91,.99),
    (.86,.85,.70),(.93,.91,.79),(.98,.96,.88),(.99,.99,.96),
    (.60,.43,.13),(.82,.61,.16),(.98,.79,.27),(.39,.32,.20),
    (.18,.29,.065),(.24,.34,.09),(.29,.37,.11),(.35,.42,.13),
]
assert len(swatches)==64
palette=bpy.data.images.new('LogicGarden_TerrainPalette',width=128,height=128,alpha=False)
pixels=[]
for yy in range(128):
    for xx in range(128):pixels.extend((*swatches[(yy//16)*8+xx//16],1))
palette.pixels=pixels
palette.filepath_raw=str(OUT/'LogicGarden_TerrainPalette.png')
palette.file_format='PNG';palette.save();palette.pack()

# Painted, deterministic topsoil/grass map. It carries the fine color flecks
# visible in the concept while the silhouette stays geometric and low-poly.
def hash2(ix,iy):
    h=(ix*374761393 ^ iy*668265263 ^ 20261011) & 0xffffffff
    h=((h ^ (h>>13))*1274126177) & 0xffffffff
    h=(h ^ (h>>16)) & 0xffffffff
    return h/0xffffffff
def noise(x,y):
    ix=math.floor(x);iy=math.floor(y);fx=x-ix;fy=y-iy
    fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
    a=hash2(ix,iy);b=hash2(ix+1,iy);c=hash2(ix,iy+1);d=hash2(ix+1,iy+1)
    return (a*(1-fx)+b*fx)*(1-fy)+(c*(1-fx)+d*fx)*fy
spots=[]
for i in range(39):
    spots.append((random.uniform(-8.3,8.3),random.uniform(-6.8,6.8),
                  random.uniform(.12,.51),random.uniform(.08,.35)))
texture=bpy.data.images.new('LogicGarden_TerrainSurface',width=512,height=512,alpha=False)
px=[]
for py in range(512):
    wy=(py/511-.5)*15.8
    for pxx in range(512):
        wx=(pxx/511-.5)*19.4
        broad=noise(wx*.65+10,wy*.65+7)
        medium=noise(wx*2.2+51,wy*2.2+19)
        fine=noise(wx*9.1+3,wy*9.1+81)
        v=.60*broad+.31*medium+.09*fine
        sparkle=noise(wx*34+13,wy*34+19)
        grain=noise(wx*91+34,wy*91+51)
        color=[.17+.20*v+.080*(sparkle-.5)+.025*(grain-.5),
               .28+.27*v+.135*(sparkle-.5)+.045*(grain-.5),
               .055+.075*v+.027*(sparkle-.5)]
        # Pale earth approach at the open front and irregular soil flecks.
        path=math.exp(-((wx/(1.07+.08*abs(wy+4.7)))**4))*max(0,min(1,(-wy-4.2)/1.5))
        patches=0
        for sx,sy,rx,ry in spots:
            dx=(wx-sx)/rx;dy=(wy-sy)/ry
            dist=dx*dx+dy*dy
            if dist<1.3:patches=max(patches,max(0,1-dist/1.3)*.48)
        dirt=max(path*.70,patches)
        soil=[.47+.07*medium,.35+.06*medium,.19+.04*fine]
        color=[a*(1-dirt)+b*dirt for a,b in zip(color,soil)]
        px.extend((*color,1))
texture.pixels=px
texture.filepath_raw=str(OUT/'LogicGarden_TerrainSurface.png')
texture.file_format='PNG';texture.save();texture.pack()

materials=[]
for name,img,rough in [
    ('Terrain_GrassAndSoil',texture,.98),
    ('Terrain_RockMossFoliage',palette,.87),
]:
    m=bpy.data.materials.new(name);m.use_nodes=True
    shader=m.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value=rough
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img
    tex.interpolation='Linear' if img==texture else 'Closest'
    m.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    materials.append(m)

class Geo:
    def __init__(self):self.v=[];self.f=[];self.colors=[]
    def add(self,vertices,faces,color):
        off=len(self.v);self.v.extend(tuple(v) for v in vertices)
        self.f.extend(tuple(off+i for i in face) for face in faces)
        self.colors.extend(color if isinstance(color,list) else [color]*len(faces))
    def quad(self,a,b,c,d,color):self.add([a,b,c,d],[(0,1,2,3)],color)
    def frond(self,x,y,z,angle,length,width,color):
        dx=math.cos(angle);dy=math.sin(angle);nx=-dy;ny=dx
        points=[]
        for t,w,up in [(0,.09,0),(.46,1,.13),(1,.025,.04)]:
            cx=x+dx*length*t;cy=y+dy*length*t
            points.extend([(cx+nx*width*w/2,cy+ny*width*w/2,z+up*length),
                           (cx-nx*width*w/2,cy-ny*width*w/2,z+up*length)])
        self.add(points,[(0,2,3,1),(2,4,5,3)],color)
    def rock(self,x,y,z,rx,ry,h,seed,small=False):
        rng=random.Random(seed);n=6;v=[]
        rings=[(0,.83),(.57,1),(.94,.53)]
        for zz,scale in rings:
            for i in range(n):
                a=(i+.5)*math.tau/n
                jitter=.87+.25*rng.random()
                v.append((x+rx*scale*jitter*math.cos(a),
                          y+ry*scale*jitter*math.sin(a),
                          z+h*zz+(rng.random()-.5)*h*.07))
        faces=[tuple(reversed(range(n))),tuple(range(2*n,3*n))]
        colors=[random.choice([0,1,2,8]),random.choice([12,13,14,16,17,18])]
        for row in range(2):
            for i in range(n):
                j=(i+1)%n
                faces.append((row*n+i,row*n+j,(row+1)*n+j,(row+1)*n+i))
                colors.append(rng.choice([1,2,3,4,5,6,7,9,10,11,12,13]))
        self.add(v,faces,colors)
        # Geometric moss cap on many rocks, rather than a dense secondary mesh.
        if not small and rng.random()<.78:
            cap=[(x+(vx-x)*.92,y+(vy-y)*.92,vz+.011) for vx,vy,vz in v[12:18]]
            self.add(cap,[tuple(range(n))],rng.choice([18,19,20,21,22,23,30,31]))

root=bpy.data.objects.new('Logic_Garden_Terrain_Border',None)
bpy.context.collection.objects.link(root)
parts=[]
def make(name,g,material=1):
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(g.v,[],g.f);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    me.materials.append(materials[material])
    uv=me.uv_layers.new(name='TerrainUV' if material==0 else 'PaletteUV')
    for face,color in zip(me.polygons,g.colors):
        for li in face.loop_indices:
            v=me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv=((v.x+9.7)/19.4,(v.y+7.9)/15.8) if material==0 else ((color%8+.5)/8,(color//8+.5)/8)
    ob.parent=root;parts.append(ob)
    return ob

# Flat, unobstructed arena-placement surface: 12 x 9.4 metres at Z=0.92.
top=Geo();nx=18;ny=14
for iy in range(ny):
    y0=-4.7+iy*9.4/ny;y1=-4.7+(iy+1)*9.4/ny
    for ix in range(nx):
        x0=-6+ix*12/nx;x1=-6+(ix+1)*12/nx
        top.quad((x0,y0,.92),(x1,y0,.92),(x1,y1,.92),(x0,y1,.92),0)
make('Top_LevelGrass_ArenaPlacement_12x9p4m',top,0)

# Rounded-square lower ground apron, made as an annulus to avoid hidden faces.
def inner_ray(a):
    c=math.cos(a);s=math.sin(a)
    t=min(6.83/max(abs(c),1e-5),5.50/max(abs(s),1e-5))
    return (t*c,t*s)
def outer_ray(a,i):
    c=math.cos(a);s=math.sin(a);p=2/3.3
    wobble=1+.045*math.sin(i*6.2)+.026*math.sin(i*12.3)
    return (9.13*math.copysign(abs(c)**p,c)*wobble,
            7.30*math.copysign(abs(s)**p,s)*wobble)
apron=Geo();N=72
for i in range(N):
    a=i*math.tau/N;b=(i+1)*math.tau/N
    ax,ay=inner_ray(a);bx,by=inner_ray(b)
    cx,cy=outer_ray(b,i+1);dx,dy=outer_ray(a,i)
    apron.quad((ax,ay,.10),(bx,by,.10),(cx,cy,.015),(dx,dy,.015),0)
make('Outer_GrassAndEarthApron',apron,0)

# Faceted retaining ledge under the rock clusters, absent at the front entrance.
ledge=Geo();segment_count=24
def band_side(axis,sign):
    for i in range(segment_count):
        t0=-1+2*i/segment_count;t1=-1+2*(i+1)/segment_count
        if axis=='Y' and sign<0 and abs((t0+t1)/2)<.185:continue
        if axis=='Y':
            a=(t0*6.03,sign*4.66);b=(t1*6.03,sign*4.66)
            c=(t1*6.62,sign*5.25);d=(t0*6.62,sign*5.25)
        else:
            a=(sign*5.95,t0*4.72);b=(sign*5.95,t1*4.72)
            c=(sign*6.61,t1*5.30);d=(sign*6.61,t0*5.30)
        color=random.choice([4,5,6,7,11,12,13,14,15])
        ledge.quad((a[0],a[1],.90),(b[0],b[1],.90),
                   (c[0],c[1],.34),(d[0],d[1],.34),color)
        ledge.quad((d[0],d[1],.34),(c[0],c[1],.34),
                   (c[0],c[1],.10),(d[0],d[1],.10),random.choice([2,3,4,5,10,11]))
for direction in [-1,1]:
    band_side('X',direction);band_side('Y',direction)
make('Rocky_RetainingLedgeAroundTop',ledge)

# A sloped earthy entrance between the near-side stone banks.
ramp=Geo()
for i in range(14):
    y0=-4.69-i*.125;y1=-4.69-(i+1)*.125
    z0=.92-i*.81/14;z1=.92-(i+1)*.81/14
    w0=.98+.17*i/14;w1=.98+.17*(i+1)/14
    ramp.quad((-w0,y0,z0),(w0,y0,z0),(w1,y1,z1),(-w1,y1,z1),0)
make('Front_Approach_EarthenSlope',ramp,0)

# Three rock sizes form the recognizable low-poly cliff silhouette.
big=Geo();lower=Geo();small=Geo();pebbles=Geo();rock_tops=[]
seed=100
for side in [-1,1]:
    for i in range(18):
        x=-5.82+i*11.64/17+random.uniform(-.18,.18)
        y=side*(4.80+random.uniform(-.10,.18))
        rx=random.uniform(.43,.72);ry=random.uniform(.39,.64)
        h=random.uniform(.62,1.02)
        big.rock(x,y,.22,rx,ry,h,seed);rock_tops.append((x,y,.22+h*.95))
        seed+=1
    for i in range(14):
        y=-4.32+i*8.64/13+random.uniform(-.18,.18)
        x=side*(6.10+random.uniform(-.11,.18))
        rx=random.uniform(.40,.65);ry=random.uniform(.40,.66)
        h=random.uniform(.62,1.02)
        big.rock(x,y,.22,rx,ry,h,seed);rock_tops.append((x,y,.22+h*.95))
        seed+=1
for side in [-1,1]:
    for i in range(7):
        x=side*(1.56+i*.65+random.uniform(-.15,.15))
        y=-4.79+random.uniform(-.10,.16)
        h=random.uniform(.61,1.03)
        big.rock(x,y,.18,random.uniform(.42,.68),random.uniform(.40,.60),h,seed)
        rock_tops.append((x,y,.18+h*.95));seed+=1
make('Border_LargeFacetedCliffRocks_WithMossCaps',big)
for side in [-1,1]:
    for i in range(17):
        x=-5.92+i*11.84/16+random.uniform(-.21,.21)
        y=side*(5.28+random.uniform(-.12,.16))
        if side<0 and abs(x)<1.03:continue
        lower.rock(x,y,.11,random.uniform(.31,.53),random.uniform(.30,.47),
                   random.uniform(.35,.66),seed);seed+=1
    for i in range(12):
        y=-4.45+i*8.90/11+random.uniform(-.20,.20)
        x=side*(6.64+random.uniform(-.11,.16))
        lower.rock(x,y,.10,random.uniform(.30,.50),random.uniform(.33,.53),
                   random.uniform(.35,.66),seed);seed+=1
make('Border_LowerLayerOfFacetedRocks',lower)
for i in range(75):
    a=random.uniform(0,math.tau)
    rad=random.uniform(1.02,1.18)
    x=6.58*rad*math.cos(a);y=5.20*rad*math.sin(a)
    if y<-4.7 and abs(x)<1.28:continue
    small.rock(x,y,.075,random.uniform(.18,.34),random.uniform(.18,.32),
               random.uniform(.16,.41),seed,small=True);seed+=1
make('Border_SecondaryStonesAtCliffFoot',small)
for i in range(71):
    a=random.uniform(0,math.tau)
    r=random.uniform(1.12,1.40)
    x=6.6*r*math.cos(a);y=5.25*r*math.sin(a)
    if abs(x)>8.8 or abs(y)>7.15:continue
    pebbles.rock(x,y,.025,random.uniform(.055,.16),random.uniform(.055,.14),
                 random.uniform(.04,.13),seed,small=True);seed+=1
make('Outer_ScatteredPebbles',pebbles)

# Small blade clusters and broader leafy ferns. All leaves are quad planes.
fern=Geo();grass=Geo()
plant_sites=[]
for side in [-1,1]:
    for i in range(14):
        x=-5.77+i*11.54/13+random.uniform(-.21,.21)
        y=side*(4.80+random.uniform(-.20,.38))
        if side<0 and abs(x)<1.35:continue
        plant_sites.append((x,y,.98,random.uniform(.35,.68)))
    for i in range(12):
        y=-4.19+i*8.38/11+random.uniform(-.19,.19)
        x=side*(6.16+random.uniform(-.18,.33))
        plant_sites.append((x,y,.97,random.uniform(.32,.58)))
for i in range(24):
    a=random.uniform(0,math.tau)
    x=7.20*math.cos(a);y=5.90*math.sin(a)
    if abs(x)<1.2 and y<-4.8:continue
    plant_sites.append((x,y,.15,random.uniform(.25,.50)))
for x,y,z in random.sample(rock_tops,min(37,len(rock_tops))):
    if y<-4.4 and abs(x)<1.1:continue
    plant_sites.append((x,y,z+.015,random.uniform(.42,.72)))
for x,y,z,length in plant_sites:
    n=random.randint(6,8);heading=random.uniform(0,math.tau)
    for j in range(n):
        angle=heading+j*math.tau/n+random.uniform(-.12,.12)
        L=length*random.uniform(.76,1.16)
        color=random.choice([25,26,27,28,32,33,34,35,36,37,38])
        fern.frond(x,y,z,angle,L,L*.19,color)
        # Paired leaflets distinguish these clumps from simple radial grass.
        for t in [.38,.66]:
            cx=x+math.cos(angle)*L*t;cy=y+math.sin(angle)*L*t
            side_len=L*(.25 if t<.5 else .20)
            for side in [-1,1]:
                leaflet_angle=angle+side*.90
                fern.frond(cx,cy,z+L*.055,leaflet_angle,side_len,
                            side_len*.42,color)
for i in range(115):
    a=random.uniform(0,math.tau);r=random.uniform(1.02,1.30)
    x=6.75*r*math.cos(a);y=5.45*r*math.sin(a)
    if abs(x)>8.9 or abs(y)>7.2:continue
    z=.105
    for j in range(random.randint(3,5)):
        grass.frond(x,y,z,random.uniform(0,math.tau),random.uniform(.10,.23),
                    random.uniform(.04,.09),random.choice([28,29,30,31,35,36,37]))
make('Ferns_BroadQuadLeavesAroundRocks',fern)
make('Grass_Tufts_OnOuterApron',grass)

# Hanging ivy descends only from the outer front/side rock ledges.
ivy=Geo()
for i in range(21):
    if i<13:
        x=-5.58+i*.93+random.uniform(-.15,.15)
        if abs(x)<1.1:continue
        y=-5.58
        dx=random.uniform(-.07,.07);dy=-.05
    else:
        y=-3.85+(i-13)*1.05+random.uniform(-.12,.12)
        x=random.choice([-6.86,6.86])
        dx=.06 if x>0 else -.06;dy=random.uniform(-.08,.08)
    z0=random.uniform(.90,1.12);length=random.uniform(.62,.97)
    for j in range(5):
        t=j/5;t2=(j+1)/5
        a=(x+dx*t,y+dy*t,z0-length*t)
        b=(x+dx*t2,y+dy*t2,z0-length*t2)
        ivy.quad((a[0]-.009,a[1],a[2]),(a[0]+.009,a[1],a[2]),
                 (b[0]+.009,b[1],b[2]),(b[0]-.009,b[1],b[2]),25)
        if j in [1,2,3,4]:
            side=-1 if j%2 else 1
            ivy.frond(b[0],b[1]-.003,b[2],math.pi if side<0 else 0,
                      random.uniform(.14,.24),.11,random.choice([26,27,28,33,34]))
make('Hanging_Ivy_AlongRockFaces',ivy)

# Miniature white, blue and yellow wildflowers at the edge, plus tiny blossoms.
flowers=Geo()
for i in range(84):
    a=random.uniform(0,math.tau);r=random.uniform(1.10,1.36)
    x=6.65*r*math.cos(a);y=5.24*r*math.sin(a)
    if abs(x)>8.8 or abs(y)>7.1:continue
    z=.105+random.uniform(.006,.024)
    radius=random.uniform(.028,.055)
    petal_color=random.choice([52,53,54,55,49,50,51])
    for j in range(4):
        theta=j*math.pi/2
        u=(math.cos(theta),math.sin(theta));v=(-u[1],u[0])
        p=[(x+u[0]*radius*.18+v[0]*radius*.31,y+u[1]*radius*.18+v[1]*radius*.31,z),
           (x+u[0]*radius*.18-v[0]*radius*.31,y+u[1]*radius*.18-v[1]*radius*.31,z),
           (x+u[0]*radius*1.35-v[0]*radius*.41,y+u[1]*radius*1.35-v[1]*radius*.41,z+.008),
           (x+u[0]*radius*1.35+v[0]*radius*.41,y+u[1]*radius*1.35+v[1]*radius*.41,z+.008)]
        flowers.add(p,[(0,3,2,1)],petal_color)
    centers=[]
    for k in range(6):
        theta=k*math.tau/6
        centers.append((x+radius*.28*math.cos(theta),y+radius*.28*math.sin(theta),z+.012))
    flowers.add(centers,[tuple(range(6))],58)
make('Wildflowers_TinyWhiteAndBluePetals',flowers)

anchor=bpy.data.objects.new('Arena_Placement_FlatCenter',None)
bpy.context.collection.objects.link(anchor)
anchor.parent=root;anchor.location=(0,0,.92)
anchor.empty_display_type='PLAIN_AXES';anchor.empty_display_size=.40
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in obs if o.type=='MESH')
def stats(obs):
    out={'vertices':0,'faces':0,'triangles':0,'mesh_objects':0,'zero_area_faces':0,'loose_vertices':0}
    for ob in obs:
        if ob.type!='MESH':continue
        me=ob.data;me.calc_loop_triangles();out['vertices']+=len(me.vertices)
        out['faces']+=len(me.polygons);out['triangles']+=len(me.loop_triangles)
        out['mesh_objects']+=1
        bm=bmesh.new();bm.from_mesh(me)
        out['zero_area_faces']+=sum(f.calc_area()<1e-10 for f in bm.faces)
        out['loose_vertices']+=sum(not v.link_faces for v in bm.verts)
        bm.free()
    return out
original=stats(parts)
assert original['zero_area_faces']==0 and original['loose_vertices']==0,original
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=Vector((0,0,.46))
            area.spaces.active.region_3d.view_distance=23
            area.spaces.active.shading.type='MATERIAL'
select(parts+[root,anchor])
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Logic_Garden_Terrain_Border.blend'))

select(parts+[root,anchor])
bpy.ops.export_scene.fbx(filepath=str(OUT/'Logic_Garden_Terrain_Border.fbx'),
    use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,
    use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,
    path_mode='COPY',embed_textures=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'Logic_Garden_Terrain_Border.fbx'))
imported=[o for o in scene.objects if o.type=='MESH']
back=stats(imported)
assert back['triangles']==original['triangles'],(back,original)
assert back['mesh_objects']==original['mesh_objects'],(back,original)
assert back['zero_area_faces']==0 and back['loose_vertices']==0
assert all(not ob.modifiers and all(abs(v-1)<1e-5 for v in ob.scale) for ob in imported)
assert bpy.data.objects.get('Arena_Placement_FlatCenter')
report={**original,'fbx_roundtrip':'PASS','reimport_triangles':back['triangles'],
        'flat_arena_pad_m':[12,9.4], 'flat_pad_unity_y_m':.92,
        'overall_footprint_m_approx':[18.3,14.6],
        'materials':2,'modifiers':0,
        'excludes':['stone arena walls','tree','fountain','arena props']}
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))
print('TERRAIN_BORDER_REPORT',json.dumps(report))

# Preview is made from the actual reimported FBX. Lights and camera are preview-only.
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1500;scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.world.color=(.40,.36,.32)
def light(pos,power,color,size,target):
    bpy.ops.object.light_add(type='AREA',location=pos)
    ob=bpy.context.object;ob.data.energy=power;ob.data.color=color
    ob.data.shape='DISK';ob.data.size=size
    ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
light((-5,-5,13),2600,(1,.85,.64),9,(0,0,.4))
light((5,3,10),1800,(.73,.83,1),8,(0,0,.4))
light((0,9,7),900,(1,1,.81),7,(0,0,.3))
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam
cam.data.type='ORTHO';cam.data.ortho_scale=19.9
cam.location=(12,-15,13.0)
cam.rotation_euler=(Vector((0,0,.35))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SRC/'Logic_Garden_Terrain_Border_Preview.png')
bpy.ops.render.render(write_still=True)
cam.location=(0,-4,23);cam.data.ortho_scale=19.2
cam.rotation_euler=(Vector((0,0,.2))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SRC/'Logic_Garden_Terrain_Border_Top.png')
bpy.ops.render.render(write_still=True)
