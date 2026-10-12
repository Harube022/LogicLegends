"""Build the Graph Chamber environment from the supplied empty-arena concept.

Run with Blender 4.5: blender --background --python Tools/create_graph_chamber_environment.py
The stage excludes graph nodes/bridges, the book statue, and the information board.
"""
import bpy
import bmesh
import json
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Art/GraphChamber/Environment'
SRC = ROOT / 'SourceAssets/GraphChamber/Environment'
OUT.mkdir(parents=True, exist_ok=True)
SRC.mkdir(parents=True, exist_ok=True)
random.seed(20261011)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# A small common texture keeps the deliberately faceted palette intact in FBX.
swatches = [
    (.050,.067,.121),(.067,.083,.145),(.079,.101,.168),(.095,.116,.185),
    (.112,.130,.199),(.126,.143,.210),(.145,.160,.223),(.158,.176,.235),
    (.067,.078,.126),(.081,.093,.143),(.100,.113,.159),(.121,.130,.174),
    (.133,.145,.185),(.153,.159,.196),(.171,.179,.210),(.188,.196,.221),
    (.035,.044,.077),(.044,.055,.093),(.055,.068,.106),(.068,.081,.119),
    (.083,.092,.129),(.095,.108,.143),(.113,.121,.153),(.137,.144,.174),
    (.271,.187,.099),(.358,.238,.115),(.482,.314,.122),(.592,.405,.150),
    (.712,.508,.212),(.847,.619,.279),(.447,.295,.136),(.235,.156,.086),
    (.009,.031,.156),(.014,.051,.282),(.020,.098,.490),(.023,.166,.708),
    (.024,.250,.867),(.064,.394,.989),(.211,.642,1.0),(.488,.854,1.0),
    (.008,.111,.295),(.012,.225,.535),(.052,.355,.745),(.114,.511,.913),
    (.267,.725,1.0),(.689,.918,1.0),(.905,.981,1.0),(.052,.065,.114),
    (.388,.092,.014),(.714,.218,.019),(.955,.388,.031),(1.0,.574,.074),
    (1.0,.746,.166),(1.0,.876,.392),(1.0,.967,.682),(.166,.079,.044),
    (.105,.116,.171),(.116,.129,.180),(.128,.139,.193),(.148,.156,.201),
    (.091,.108,.166),(.172,.174,.211),(.190,.189,.217),(.226,.209,.211),
]
palette = bpy.data.images.new('GraphChamber_Palette', width=128, height=128, alpha=False)
pix=[]
for yy in range(128):
    for xx in range(128):
        pix.extend((*swatches[(yy//16)*8+xx//16],1))
palette.pixels=pix
palette.filepath_raw=str(OUT/'GraphChamber_Palette.png')
palette.file_format='PNG'
palette.save()
palette.pack()

materials=[]
for name,rough,metal,emit in [
    ('GraphChamber_Stone',.85,0,0),
    ('GraphChamber_Gold',.38,.61,0),
    ('GraphChamber_BlueCloth',.78,0,0),
    ('GraphChamber_CrystalGlow',.27,.04,2.1),
    ('GraphChamber_FireGlow',.43,0,1.8),
]:
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Roughness'].default_value=rough
    p.inputs['Metallic'].default_value=metal
    tex=m.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=palette;tex.interpolation='Closest'
    m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
    if emit:
        m.node_tree.links.new(tex.outputs['Color'],p.inputs['Emission Color'])
        p.inputs['Emission Strength'].default_value=emit
    materials.append(m)

class Geo:
    def __init__(self): self.v=[];self.f=[];self.c=[]
    def add(self,verts,faces,color):
        off=len(self.v)
        self.v.extend(tuple(q) for q in verts)
        self.f.extend(tuple(off+i for i in face) for face in faces)
        self.c.extend(color if isinstance(color,list) else [color]*len(faces))
    def box(self,pos,size,color,chamfer=0):
        x,y,z=(t/2 for t in size);px,py,pz=pos
        b=min(chamfer,x*.38,y*.38,z*.7)
        if b:
            v=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),
               (-x,-y,z-b),(x,-y,z-b),(x,y,z-b),(-x,y,z-b),
               (-x+b,-y+b,z),(x-b,-y+b,z),(x-b,y-b,z),(-x+b,y-b,z)]
            f=[(3,2,1,0),(8,9,10,11)]
            for i in range(4):
                j=(i+1)%4
                f.extend([(i,j,j+4,i+4),(i+4,j+4,j+8,i+8)])
        else:
            v=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),
               (-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
            f=[(3,2,1,0),(4,5,6,7),(0,1,5,4),
               (1,2,6,5),(2,3,7,6),(3,0,4,7)]
        self.add([(px+a,py+bb,pz+cc) for a,bb,cc in v],f,color)
    def cylinder(self,pos,r,h,color,n=8,r2=None):
        x,y,z=pos;r2=r if r2 is None else r2
        v=[]
        for zz,rr in [(z,r),(z+h,r2)]:
            for i in range(n):
                a=math.tau*i/n+math.pi/n
                v.append((x+rr*math.cos(a),y+rr*math.sin(a),zz))
        f=[tuple(reversed(range(n))),tuple(range(n,2*n))]
        f += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        self.add(v,f,color)
    def floor_strip(self,points,width,color,z=.026):
        for a,b in zip(points,points[1:]):
            dx=b[0]-a[0];dy=b[1]-a[1];length=math.hypot(dx,dy)
            if length<1e-5:continue
            nx=-dy*width/(2*length);ny=dx*width/(2*length)
            v=[(a[0]+nx,a[1]+ny,z),(a[0]-nx,a[1]-ny,z),
               (b[0]-nx,b[1]-ny,z),(b[0]+nx,b[1]+ny,z)]
            self.add(v,[(0,1,2,3)],color)

root=bpy.data.objects.new('Graph_Chamber_Environment',None)
bpy.context.collection.objects.link(root)
parts=[]
def obj(name,g,material=0):
    me=bpy.data.meshes.new(name+'_Mesh')
    me.from_pydata(g.v,[],g.f);me.update()
    ob=bpy.data.objects.new(name,me)
    bpy.context.collection.objects.link(ob)
    me.materials.append(materials[material])
    uv=me.uv_layers.new(name='PaletteUV')
    for face,col in zip(me.polygons,g.c):
        for li in face.loop_indices:
            uv.data[li].uv=((col%8+.5)/8,(col//8+.5)/8)
    ob.parent=root
    parts.append(ob)
    return ob

# Broad open gameplay floor: individual stone slabs, an inset circular guide,
# and the central nested diamond visible in the reference.
g=Geo();g.box((0,0,-.29),(15.5,11.6,.52),16,.05)
obj('Floor_SolidFoundation',g)
g=Geo()
cols,rows=25,18
for row in range(rows):
    for col in range(cols):
        x=-7.5+(col+.5)*15/cols
        y=-5.5+(row+.5)*11/rows
        # Small offset and randomized stone color create the illustrated joints.
        z=.018+random.choice([0,.002,.005,.008])
        color=random.choice([1,2,3,3,4,4,5,6,8,9,10,11,56,57,58,60])
        g.box((x,y,z-.014),(.588,.599,.060),color,.012)
obj('Floor_450_IndividualStoneTiles',g)
# Angular hairline fractures on selected slabs add the hand-cut character of
# the concept without replacing the floor with a dense sculpt.
g=Geo()
for i in range(165):
    col=random.randrange(cols);row=random.randrange(rows)
    x=-7.5+(col+.5)*15/cols;y=-5.5+(row+.5)*11/rows
    if abs(x)<.75 and abs(y)<.7:continue
    angle=random.random()*math.tau
    length=random.uniform(.10,.25)
    points=[(x,y),(x+length*math.cos(angle),y+length*math.sin(angle))]
    bend=angle+random.choice([-.8,.8])
    points.append((points[-1][0]+length*.60*math.cos(bend),
                   points[-1][1]+length*.60*math.sin(bend)))
    g.floor_strip(points,.012,16,.052)
obj('Floor_IrregularStoneHairlineCracks',g)
g=Geo()
for r,w in [(3.05,.040),(3.46,.035),(4.94,.040)]:
    pts=[(r*math.cos(i*math.tau/64),r*.70*math.sin(i*math.tau/64)) for i in range(65)]
    g.floor_strip(pts,w,26,.057)
for radius in [1.30,1.74]:
    pts=[(radius*math.cos(math.pi/4+i*math.pi/2),radius*.69*math.sin(math.pi/4+i*math.pi/2)) for i in range(5)]
    g.floor_strip(pts,.055,26,.062)
for x in [-5.2,5.2]:g.floor_strip([(x,-4.18),(x,3.76)],.035,25,.055)
obj('Floor_ConcentricInlayAndCentralDiamond',g,1)

# Interior back and side walls; the room is open at the front for inspection.
g=Geo();g.box((0,5.69,2.80),(15.4,.47,5.62),16)
g.box((-7.53,0,2.80),(.47,11.2,5.62),16)
g.box((7.53,0,2.80),(.47,11.2,5.62),16)
obj('Walls_SolidBackAndSides',g)
g=Geo()
for row in range(9):
    z=.36+row*.59
    offset=.45 if row%2 else 0
    for wall in ['Back','Left','Right']:
        length=15.1 if wall=='Back' else 10.7
        x=-length/2-offset
        while x<length/2:
            lo=max(x,-length/2);hi=min(x+1.06,length/2)
            if hi-lo>.1:
                c=random.choice([1,2,3,4,5,8,9,10,11,12,57,58,60])
                if wall=='Back':g.box(((lo+hi)/2,5.417,z),(hi-lo-.018,.045,.555),c,.008)
                elif wall=='Left':g.box((-7.282,(lo+hi)/2,z),(.045,hi-lo-.018,.555),c,.008)
                else:g.box((7.282,(lo+hi)/2,z),(.045,hi-lo-.018,.555),c,.008)
            x+=1.06
obj('Walls_StaggeredMasonryFacing',g)

g=Geo()
for y in [5.35]:
    for z,h,d,c in [(.24,.24,.32,9),(1.13,.12,.18,12),(4.95,.20,.24,11),(5.37,.24,.37,10)]:
        g.box((0,y,z),(15.3,d,h),c,.015)
for x in [-7.27,7.27]:
    for z,h,d,c in [(.24,.24,.32,9),(1.13,.12,.18,12),(4.95,.20,.24,11),(5.37,.24,.37,10)]:
        g.box((x,0,z),(d,11.0,h),c,.015)
obj('Walls_ContinuousStoneCourses',g)

# Pointed dark alcoves and their shallow, segmented stone arch frames.
def alcove(label,cx,cy,ux,uy,nx,ny,width=1.10,height=4.40):
    def P(s,d,z):return (cx+ux*s+nx*d,cy+uy*s+ny*d,z)
    # Local x is along the wall; positive d faces into the chamber.
    inner=[(-width*.82,.52),(-width*.82,2.93)]
    for i in range(1,7):
        t=i/6;inner.append((-width*.82*(1-t),2.93+(height-2.93)*math.sin(t*math.pi/2)))
    for i in range(5,-1,-1):
        t=i/6;inner.append((width*.82*(1-t),2.93+(height-2.93)*math.sin(t*math.pi/2)))
    inner.append((width*.82,.52))
    outer=[]
    for s,z in inner:
        outer.append((s+(-.18 if s<0 else .18 if s>0 else 0),z+.23 if z>2.9 else z))
    g=Geo();n=len(inner)
    v=[P(s,.04,z) for s,z in inner]+[P(s,.015,z) for s,z in inner]
    faces=[tuple(range(n)),tuple(reversed(range(n,2*n)))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    g.add(v,faces,17)
    obj('Alcove_'+label+'_DarkRecess',g)
    g=Geo()
    for i in range(len(inner)-1):
        a,b=inner[i],inner[i+1];c,d=outer[i+1],outer[i]
        v=[P(a[0],.13,a[1]),P(b[0],.13,b[1]),P(c[0],.13,c[1]),P(d[0],.13,d[1]),
           P(a[0],.105,a[1]),P(b[0],.105,b[1]),P(c[0],.105,c[1]),P(d[0],.105,d[1])]
        g.add(v,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),
                 (2,6,7,3),(3,7,4,0)],random.choice([4,5,6,9,10,11,12]))
    for side in [-1,1]:
        for z in [.52,1.10,1.70,2.29,2.91]:
            s=side*(width*.82+.10)
            if abs(nx)>.5:g.box(P(s,.14,z),(.18,.28,.055),12,.007)
            else:g.box(P(s,.14,z),(.28,.18,.055),12,.007)
    obj('Alcove_'+label+'_PointedStoneFrame',g)

for i,x in enumerate([-5.88,-2.94,0,2.94,5.88]):
    alcove('Rear_%02d'%i,x,5.39,1,0,0,-1,width=.95,height=4.63 if i==2 else 4.38)
for side,x,normal in [('West',-7.28,1),('East',7.28,-1)]:
    for i,y in enumerate([-3.4,.05,3.25]):
        alcove(side+'_%02d'%i,x,y,0,1,normal,0,width=.89,height=4.48)

# Layered square piers frame the wall rhythm, including the cropped front edge.
g=Geo()
def pier(x,y,variation=0):
    for z,w,d,h,c in [
        (.06,.76,.75,.18,9),(.21,.68,.67,.13,11),(.76,.49,.49,1.00,5),
        (1.35,.60,.59,.17,10),(2.85,.44,.45,2.84,4),
        (4.36,.57,.58,.18,10),(4.63,.66,.66,.31,11),
        (4.91,.78,.75,.24,9),(5.24,.72,.71,.20,11)]:
        g.box((x,y,z),(w,d,h),c,.025)
    for z in [1.72,2.50,3.25]:g.box((x,y,z),(.51,.51,.045),12,.005)
for x in [-7.26,-4.40,-1.49,1.49,4.40,7.26]:pier(x,5.16)
for x in [-7.28,7.28]:
    for y in [-5.10,-1.82,1.63,5.05]:pier(x,y)
obj('Architecture_SquarePiersAndCapstones',g)

# Blue banners, sculpted tapered hems, crossbars and cyan double diamonds.
cloth=Geo();gold=Geo();glow=Geo()
def banner(cx,cy,nx,ny,label):
    ux,uy=-ny,nx
    def P(s,d,z):return (cx+ux*s+nx*d,cy+uy*s+ny*d,z)
    poly=[(-.45,4.80),(.45,4.80),(.45,3.11),(0,2.75),(-.45,3.11)]
    v=[P(s,.10,z) for s,z in poly]+[P(s,.075,z) for s,z in poly]
    cloth.add(v,[(0,1,2,3,4),(9,8,7,6,5)]+
              [(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)],33)
    cloth.add([P(-.39,.115,4.69),P(0,.18,4.66),P(0,.18,2.91),P(-.39,.115,3.17)],[(0,1,2,3)],35)
    cloth.add([P(0,.18,4.66),P(.39,.115,4.69),P(.39,.115,3.17),P(0,.18,2.91)],[(0,1,2,3)],34)
    for s in [-.425,.425]:
        # Slender metal rods lie on the banner's wall-normal plane.
        for z in [3.17,3.56,3.95,4.34]:
            vv=[P(s-.012,.20,z),P(s+.012,.20,z),P(s+.012,.20,z+.38),P(s-.012,.20,z+.38),
                P(s-.012,.195,z),P(s+.012,.195,z),P(s+.012,.195,z+.38),P(s-.012,.195,z+.38)]
            gold.add(vv,[(0,1,2,3),(7,6,5,4)],27)
    v=[P(-.44,.20,3.13),P(0,.20,2.77),P(.44,.20,3.13),
       P(-.44,.195,3.13),P(0,.195,2.77),P(.44,.195,3.13)]
    gold.add(v,[(0,1,2),(5,4,3)],28)
    for zz,scale in [(4.17,.116),(3.88,.075)]:
        v=[P(0,.23,zz+scale*1.45),P(scale*.78,.23,zz),P(0,.23,zz-scale*1.45),P(-scale*.78,.23,zz),
           P(0,.225,zz+scale*1.45),P(scale*.78,.225,zz),P(0,.225,zz-scale*1.45),P(-scale*.78,.225,zz)]
        glow.add(v,[(0,1,2,3),(7,6,5,4)],44)
    # A faceted horizontal rod, end caps, and hanging straps.
    if abs(ny)>.5:
        gold.box((cx,cy+ny*.13,4.90),(1.16,.095,.095),26,.013)
        for s in [-.52,.52]:gold.cylinder(P(s,.13,4.84),.075,.13,28,n=6)
    else:
        gold.box((cx+nx*.13,cy,4.90),(.095,1.16,.095),26,.013)
        for s in [-.52,.52]:gold.cylinder(P(s,.13,4.84),.075,.13,28,n=6)
for x in [-4.33,4.33]:banner(x,4.66,0,-1,'Rear')
for y in [-4.05,3.92]:banner(-6.76,y,1,0,'West')
for y in [-4.05,3.92]:banner(6.76,y,-1,0,'East')
obj('Decor_RoyalBlueHangingBanners',cloth,2)
obj('Decor_BannerGoldTrimAndRods',gold,1)
obj('Decor_BannerCyanDiamondEmblems',glow,3)

# Low parapet and stairs at the approach, as visible across the image bottom.
g=Geo()
for a,b in [(-7.55,-1.44),(1.44,7.55)]:
    length=b-a;center=(a+b)/2
    g.box((center,-5.73,-.03),(length,.54,.49),9,.025)
    for i in range(max(1,round(length/.52))):
        x=a+(i+.5)*length/max(1,round(length/.52))
        g.box((x,-5.72,.29),(.48,.62,.20),random.choice([9,10,11,12,13]),.025)
for x in [-7.27,-5.92,-1.46,1.46,5.92,7.27]:
    for z,w,h,c in [(.12,.72,.30,10),(.49,.61,.45,5),(.78,.73,.17,12)]:
        g.box((x,-5.72,z),(w,.72,h),c,.025)
for i,(yy,zz) in enumerate([(-5.75,-.10),(-6.16,-.23),(-6.57,-.35),(-6.98,-.48)]):
    g.box((0,yy,zz),(2.85,.43,.20),random.choice([9,10,11,12]),.021)
for x in [-1.52,1.52]:
    g.box((x,-6.37,-.13),(.22,1.70,.42),11,.025)
obj('Entrance_LowStoneParapetAndStairs',g)

# Faceted stone braziers around the room. The flames are actual geometry.
g=Geo();metal=Geo();fire=Geo();torch_positions=[]
def torch(x,y,base,name):
    for z,s,h,c in [(base,.72,.18,10),(base+.20,.62,.19,11),
                     (base+.61,.43,.64,5),(base+1.01,.59,.17,9),
                     (base+1.13,.66,.14,11)]:
        g.box((x,y,z+h/2),(s,s,h),c,.026)
    metal.cylinder((x,y,base+1.20),.25,.11,28,n=8,r2=.33)
    metal.cylinder((x,y,base+1.30),.32,.10,26,n=8,r2=.19)
    fire.cylinder((x,y,base+1.38),.18,.13,50,n=7,r2=.14)
    for i in range(5):
        a=i*math.tau/5
        dx=.095*math.cos(a);dy=.095*math.sin(a)
        h=[.35,.47,.43,.29,.39][i]
        fire.cylinder((x+dx,y+dy,base+1.45),.093,h*.64,51,n=5,r2=.062)
        fire.cylinder((x+dx,y+dy,base+1.45+h*.64),.063,h*.46,52,n=5,r2=.003)
    torch_positions.append((x,y,base+1.66))
for i,x in enumerate([-6.20,-3.68,3.68,6.20]):torch(x,4.49,.02,'Rear')
for x in [-6.72,6.72]:
    for y in [-4.48,-.72,2.43]:torch(x,y,.02,'Side')
for x in [-5.48,-1.95,1.95,5.48]:torch(x,-5.03,.03,'Front')
obj('Props_14_StoneTorchPedestals',g)
obj('Props_14_GoldBrazierBowls',metal,1)
obj('Props_14_FacetedFlames',fire,4)

# Faceted blue crystal groups placed in alcoves, never in the open play area.
crystal=Geo();crystal_foot=Geo()
def crystal_shaft(x,y,z,r,h,seed):
    n=6;v=[];tilt=.07*math.sin(seed*2.3)
    for zz,scale in [(z,.56),(z+h*.22,1),(z+h*.72,.77)]:
        for i in range(n):
            a=i*math.tau/n+seed*.13
            v.append((x+r*scale*math.cos(a)+tilt*(zz-z)/h,
                      y+r*.75*scale*math.sin(a),zz))
    v.append((x+tilt,y-.02,z+h))
    f=[];colors=[]
    for row in range(2):
        for i in range(n):
            j=(i+1)%n
            f.append((row*n+i,row*n+j,(row+1)*n+j,(row+1)*n+i))
            colors.append([36,38,40,42,44,45][(i+seed+row)%6])
    for i in range(n):
        f.append((12+i,12+(i+1)%n,18));colors.append([36,38,44,45][(i+seed)%4])
    f.append(tuple(reversed(range(n))));colors.append(40)
    crystal.add(v,f,colors)
def crystal_group(x,y,z,scale,seed):
    crystal_foot.cylinder((x,y,z-.065),.45*scale,.14*scale,35,n=8,r2=.36*scale)
    for i,(dx,dy,r,h) in enumerate([(0,0,.25,1.08),(-.31,-.10,.16,.61),(.29,.03,.15,.54),(.13,.27,.10,.38)]):
        crystal_shaft(x+dx*scale,y+dy*scale,z,r*scale,h*scale,seed+i)
for i,x in enumerate([-5.85,-2.94,0,2.94,5.85]):
    crystal_group(x,4.96,.13,1.10 if i==2 else .73,i+3)
for x in [-6.85,6.85]:
    for i,y in enumerate([-3.52,3.15]):crystal_group(x,y,.12,.62,i+int(x))
obj('Props_CrystalClusterStoneBases',crystal_foot)
obj('Props_Nine_BlueFacetedCrystalClusters',crystal,3)

# Small masonry fragments in the wall corners, confined away from play space.
g=Geo()
for i in range(37):
    side=random.choice([-1,1])
    x=side*random.uniform(6.3,7.05)
    y=random.uniform(-4.65,4.60)
    if abs(y+4.48)<.57 or abs(y+3.52)<.43:continue
    w=random.uniform(.16,.35);d=random.uniform(.14,.29);h=random.uniform(.12,.32)
    g.box((x,y,h/2+.03),(w,d,h),random.choice([4,5,6,9,10,11,12]),.012)
obj('Props_SmallCornerStoneFragments',g)

anchor=bpy.data.objects.new('Anchor_OpenGameplayCenter',None)
bpy.context.collection.objects.link(anchor)
anchor.parent=root;anchor.location=(0,0,.08)
anchor.empty_display_type='PLAIN_AXES';anchor.empty_display_size=.3

scene=bpy.context.scene
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in obs if o.type=='MESH')
def measure(obs):
    out={'vertices':0,'faces':0,'triangles':0,'mesh_objects':0,'zero_area_faces':0,'loose_vertices':0}
    for o in obs:
        if o.type!='MESH':continue
        me=o.data;me.calc_loop_triangles()
        out['vertices']+=len(me.vertices);out['faces']+=len(me.polygons)
        out['triangles']+=len(me.loop_triangles);out['mesh_objects']+=1
        bm=bmesh.new();bm.from_mesh(me)
        out['zero_area_faces']+=sum(f.calc_area()<1e-10 for f in bm.faces)
        out['loose_vertices']+=sum(not v.link_faces for v in bm.verts)
        bm.free()
    return out
original=measure(parts)
original_by_name={o.name:len(o.data.loop_triangles) for o in parts}
assert original['zero_area_faces']==0 and original['loose_vertices']==0,original
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=Vector((0,0,1.5))
            area.spaces.active.region_3d.view_distance=17
            area.spaces.active.shading.type='MATERIAL'
select(parts+[root,anchor])
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Graph_Chamber_Environment.blend'))

# Export named, editable groups instead of a monolithic triangulated mesh.
select(parts+[root,anchor])
bpy.ops.export_scene.fbx(filepath=str(OUT/'Graph_Chamber_Environment.fbx'),
    use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,
    use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,
    path_mode='COPY',embed_textures=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'Graph_Chamber_Environment.fbx'))
imported=[o for o in scene.objects if o.type=='MESH']
back=measure(imported)
for o in imported:
    if len(o.data.loop_triangles)!=original_by_name.get(o.name):
        print('ROUNDTRIP_DIFFERENCE',o.name,original_by_name.get(o.name),len(o.data.loop_triangles))
assert back['triangles']==original['triangles'],(back,original)
assert back['zero_area_faces']==0 and back['loose_vertices']==0,back
assert back['mesh_objects']==original['mesh_objects'],(back,original)
assert all(not o.modifiers and all(abs(t-1)<1e-5 for t in o.scale) for o in imported)
assert bpy.data.objects.get('Anchor_OpenGameplayCenter')
report={**original,'fbx_roundtrip':'PASS','reimport_triangles':back['triangles'],
        'interior_dimensions_m':[15,11,5.6],
        'includes':['tiled floor','circular and diamond stone inlay','rear and side masonry walls','11 pointed alcoves','square piers','6 blue banners','14 torch pedestals and faceted flames','9 crystal clusters','front parapet and entrance stairs'],
        'excludes':['vertex platforms','edge bridges','book statue','information board'],
        'material_count':5,'modifiers':0}
(SRC/'geometry-report.json').write_text(json.dumps(report,indent=2))
print('GRAPH_CHAMBER_REPORT',json.dumps(report))

# Preview is rendered from the reimported FBX. Preview lights/camera are not exported.
scene.render.engine='CYCLES'
scene.cycles.samples=28
scene.cycles.use_denoising=True
scene.render.resolution_x=1536;scene.render.resolution_y=1024
scene.render.resolution_percentage=100
scene.world.color=(.025,.039,.079)
def area(pos,power,color,size,target):
    bpy.ops.object.light_add(type='AREA',location=pos)
    o=bpy.context.object;o.data.energy=power;o.data.color=color
    o.data.shape='DISK';o.data.size=size
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for x,y,z in torch_positions:
    bpy.ops.object.light_add(type='POINT',location=(x,y,z))
    o=bpy.context.object;o.data.energy=100;o.data.color=(1,.45,.13)
    o.data.shadow_soft_size=.38
area((0,-1,8),2400,(.39,.54,1),12,(0,0,0))
area((0,5.1,4.2),850,(.17,.38,1),6,(0,0,1.8))
area((0,-6.8,5.4),430,(1,.61,.31),7,(0,-2,0))
scene.use_nodes=True
nodes=scene.node_tree.nodes;nodes.clear()
rl=nodes.new('CompositorNodeRLayers')
gl=nodes.new('CompositorNodeGlare');gl.glare_type='FOG_GLOW';gl.threshold=2.5;gl.quality='HIGH'
co=nodes.new('CompositorNodeComposite')
scene.node_tree.links.new(rl.outputs['Image'],gl.inputs['Image'])
scene.node_tree.links.new(gl.outputs['Image'],co.inputs['Image'])
bpy.ops.object.camera_add()
cam=bpy.context.object;scene.camera=cam
cam.data.type='ORTHO';cam.data.ortho_scale=17.65
cam.location=(0,-17.5,11.7)
cam.rotation_euler=(Vector((0,.20,1.60))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SRC/'Graph_Chamber_Environment_Preview.png')
bpy.ops.render.render(write_still=True)
cam.data.type='PERSP';cam.data.lens=27
cam.location=(2.2,-13.5,8.8)
cam.rotation_euler=(Vector((0,0,1.7))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SRC/'Graph_Chamber_Environment_Angled.png')
bpy.ops.render.render(write_still=True)
