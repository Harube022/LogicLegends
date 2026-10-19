"""Rebuild the Proof Devourer game asset in Blender 5.0 (no add-ons required).

Run: blender --background --factory-startup --python <this file>
All character surfaces, skin weights and thirteen baked actions are authored here.
Presentation objects are kept in a separate collection and excluded from FBX.
"""
import bpy, bmesh, math, json, random, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion, Euler
import numpy as np

BASE = Path(__file__).resolve().parents[1]
for folder in ('Models', 'Textures', 'Materials', 'Animations', 'Prefabs', 'Previews', 'Editor'):
    (BASE / folder).mkdir(parents=True, exist_ok=True)
random.seed(1783)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for block in list(bpy.data.actions):
    bpy.data.actions.remove(block)
scene = bpy.context.scene
scene.name = 'Proof Devourer | Asset and animation review'
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.fps = 30
asset_collection = bpy.data.collections.new('CHARACTER | export only this collection')
scene.collection.children.link(asset_collection)

# UV islands live inside padded palette tiles. Variation is painted per facet,
# with a very subtle mineral grain; there are no runtime procedural shaders.
palette = [
    (.62,.455,.46), (.70,.535,.54), (.77,.605,.60), (.83,.67,.65),
    (.89,.735,.70), (.58,.43,.46), (.72,.565,.59), (.79,.66,.68),
    (.53,.23,.25), (.66,.30,.32), (.76,.38,.38), (.41,.16,.20),
    (.82,.52,.48), (.90,.66,.57), (.95,.78,.66), (.99,.85,.74),
    (.29,.045,.07), (.43,.085,.11), (.56,.14,.17), (.18,.028,.05),
    (.022,.008,.016), (.10,.018,.034), (.20,.038,.055), (.32,.07,.10),
    (.91,.75,.65), (.81,.57,.50), (.46,.34,.39), (.64,.45,.48),
    (1.,.045,.075), (1.,.12,.16), (.86,.012,.032), (1.,.36,.35),
]
def make_atlas(name, size, emission=False):
    pixels = np.zeros((size, size, 4), dtype=np.float32)
    pixels[:, :, 3] = 1
    tile = size // 8
    yy, xx = np.mgrid[0:tile, 0:tile]
    for i, color in enumerate(palette):
        y, x = (i // 8) * tile, (i % 8) * tile
        grain = 1 + .022 * np.sin(xx * .32 + yy * .55) * np.sin(yy * .17 - xx * .29)
        if emission:
            color = color if i >= 28 else (0, 0, 0)
            grain = np.ones_like(grain)
        pixels[y:y+tile, x:x+tile, :3] = np.array(color)[None, None, :] * grain[:, :, None]
    image = bpy.data.images.new(name, width=size, height=size, alpha=False)
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(BASE / 'Textures' / (name + '.png'))
    image.file_format = 'PNG'
    image.save()
    return image
atlas = make_atlas('ProofDevourer_BaseColor', 1024)
emission_atlas = make_atlas('ProofDevourer_Emission', 512, True)
def make_material(name, glow=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = .86 if not glow else .58
    shader.inputs['Metallic'].default_value = 0
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = atlas
    mat.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
    if glow:
        emission = mat.node_tree.nodes.new('ShaderNodeTexImage')
        emission.image = emission_atlas
        mat.node_tree.links.new(emission.outputs['Color'], shader.inputs['Emission Color'])
        shader.inputs['Emission Strength'].default_value = 1.8
    return mat
materials = [make_material('Devourer_PaleSkin'), make_material('Devourer_RedRunes', True)]

verts, faces, face_colors, face_mats, weights = [], [], [], [], []
def vtx(point, weight):
    verts.append(tuple(point))
    weights.append({weight: 1.} if isinstance(weight, str) else dict(weight))
    return len(verts)-1
def face(indices, color, glow=False):
    faces.append(tuple(indices))
    face_colors.append(random.choice(color) if isinstance(color, (tuple, list)) else color)
    face_mats.append(int(glow))
def frame_for(direction):
    axis = Vector(direction).normalized()
    seed = Vector((0, 1, 0)) if abs(axis.y) < .9 else Vector((0, 0, 1))
    u = axis.cross(seed).normalized()
    return u, axis.cross(u).normalized()
def loft(points, radii, sides, weight, colors, caps=True, jitter=.04):
    rings = []
    for i, (p, radius) in enumerate(zip(points, radii)):
        p = Vector(p)
        axis = Vector(points[min(i+1, len(points)-1)]) - Vector(points[max(0,i-1)])
        u, v = frame_for(axis)
        r1, r2 = (radius, radius) if isinstance(radius, (float, int)) else radius
        w = weight[i] if isinstance(weight, list) else weight
        ring = []
        for j in range(sides):
            a = math.tau*j/sides + .13
            rough = 1 + random.uniform(-jitter, jitter)
            co=p + rough * (u*r1*math.cos(a) + v*r2*math.sin(a))
            ring.append(vtx(co, w(co) if callable(w) else w))
        rings.append(ring)
    for i in range(len(rings)-1):
        for j in range(sides):
            k = (j+1) % sides
            # Alternating diagonals produce broad, irregular mineral facets.
            a,b,c,d = rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]
            if (i+j)%2:
                face((a,b,d), colors); face((b,c,d), colors)
            else:
                face((a,b,c), colors); face((a,c,d), colors)
    cap_start,cap_end=caps if isinstance(caps,tuple) else (caps,caps)
    if cap_start:
        face(tuple(reversed(rings[0])), colors)
    if cap_end:
        face(rings[-1], colors)
    return rings
def rock(center, scale, weight, colors=(1,2,3), n=7, rings=4, rotation=None, rough=.07):
    center = Vector(center)
    rot = Euler(rotation or (0,0,0)).to_matrix()
    rows = []
    # Latitudes avoid UV poles and give a chiseled, broad-facet silhouette.
    for k in range(1, rings):
        a = math.pi*k/rings
        row=[]
        for j in range(n):
            b = math.tau*(j + (k%2)*.24)/n
            p = Vector((scale[0]*math.sin(a)*math.cos(b), scale[1]*math.sin(a)*math.sin(b), scale[2]*math.cos(a)))
            row.append(vtx(center + rot @ (p * (1+random.uniform(-rough,rough))), weight))
        rows.append(row)
    top=vtx(center+rot@Vector((0,0,scale[2])),weight)
    bottom=vtx(center+rot@Vector((0,0,-scale[2])),weight)
    for j in range(n):
        face((top,rows[0][j],rows[0][(j+1)%n]),colors)
        face((bottom,rows[-1][(j+1)%n],rows[-1][j]),colors)
    for k in range(len(rows)-1):
        for j in range(n):
            a,b,c,d=rows[k][j],rows[k][(j+1)%n],rows[k+1][(j+1)%n],rows[k+1][j]
            face((a,b,c),colors); face((a,c,d),colors)
def shard(base, tip, width, depth, weight, colors=(12,13,14,15), sides=5):
    base,tip=Vector(base),Vector(tip)
    direction=tip-base
    u,v=frame_for(direction)
    rows=[]
    for t,scale in [(0,.72),(.34,1),(.68,.58)]:
        rows.append([vtx(base+direction*t + u*width*scale*math.cos(math.tau*j/sides) + v*depth*scale*math.sin(math.tau*j/sides),weight) for j in range(sides)])
    for r in range(2):
        for j in range(sides):
            face((rows[r][j],rows[r][(j+1)%sides],rows[r+1][(j+1)%sides],rows[r+1][j]),colors)
    end=vtx(tip,weight)
    for j in range(sides):
        face((rows[-1][j],rows[-1][(j+1)%sides],end),colors)
    face(tuple(reversed(rows[0])),colors)
def ribbon(points, width, weight, color=29, normal=(0,-1,0), glow=True):
    normal=Vector(normal)
    rows=[]
    for i,p in enumerate(points):
        tangent=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        side=tangent.cross(normal).normalized()*width*.5
        rows.append((vtx(Vector(p)-side,weight),vtx(Vector(p)+side,weight)))
    for i in range(len(rows)-1):
        face((rows[i][0],rows[i][1],rows[i+1][1],rows[i+1][0]),color,glow)
def line_symbol(coords, pos, scale, weight, width=.024, color=29):
    for stroke in coords:
        ribbon([(pos[0]+x*scale,pos[1],pos[2]+z*scale) for x,z in stroke],width,weight,color)
SIGMA = [[(.8,1),(-.7,1),(.05,0),(-.7,-1),(.8,-1)],[(.8,1),(.8,.7)],[(.8,-1),(.8,-.7)]]
CUBE = [[(0,1),(-.85,.5),(0,0),(.85,.5),(0,1)],[(0,0),(0,-1),(-.85,-.5),(-.85,.5)],[(0,-1),(.85,-.5),(.85,.5)]]
FORALL = [[(-.8,1),(0,-1),(.8,1)],[(-.5,.2),(.5,.2)]]

# This creature has a different quadruped skeleton and entirely new geometry.
# Four radial mouth controls form an iris; there is no eye, wing or tail rig.
bone_defs=[]
def bone(name,head,tail,parent=None):bone_defs.append((name,Vector(head),Vector(tail),parent))
bone('Root',(0,0,0),(0,0,.30))
bone('Pelvis',(0,.95,1.40),(0,.72,1.80),'Root')
bone('Spine_01',(0,.72,1.80),(0,.20,2.13),'Pelvis')
bone('Spine_02',(0,.20,2.13),(0,-.30,2.48),'Spine_01')
bone('Chest',(0,-.30,2.48),(0,-.66,2.55),'Spine_02')
bone('Neck_01',(0,-.66,2.55),(0,-1.10,2.35),'Chest')
bone('Neck_02',(0,-1.10,2.35),(0,-1.53,2.12),'Neck_01')
bone('Head',(0,-1.53,2.12),(0,-2.04,2.08),'Neck_02')
MAW=Vector((0,-2.09,2.07))
mouth_controls=['Mouth_Left','Mouth_Upper','Mouth_Right','Mouth_Lower']
for i,name in enumerate(mouth_controls):
    a=i*math.pi/2;radial=Vector((math.cos(a),0,math.sin(a)))
    bone(name,MAW+radial*.54,MAW+radial*.30,'Head')
bone('BackRidge',(0,.1,2.90),(0,.55,3.03),'Spine_02')
for s,side in [(1,'L'),(-1,'R')]:
    bone('Shoulder.'+side,(0,-.48,2.44),(s*.91,-.55,2.35),'Chest')
    bone('UpperArm.'+side,(s*.91,-.55,2.35),(s*1.31,-.38,1.35),'Shoulder.'+side)
    bone('Forearm.'+side,(s*1.31,-.38,1.35),(s*1.47,-1.10,.44),'UpperArm.'+side)
    bone('Hand.'+side,(s*1.47,-1.10,.44),(s*1.48,-1.47,.22),'Forearm.'+side)
    bone('Digit_01.'+side,(s*1.25,-1.47,.21),(s*1.22,-1.79,.10),'Hand.'+side)
    bone('Digit_02.'+side,(s*1.70,-1.47,.21),(s*1.74,-1.79,.10),'Hand.'+side)
    bone('Thigh.'+side,(s*.62,.98,1.39),(s*1.01,1.35,.79),'Pelvis')
    bone('Shin.'+side,(s*1.01,1.35,.79),(s*1.03,.90,.27),'Thigh.'+side)
    bone('Foot.'+side,(s*1.03,.90,.27),(s*1.04,.48,.16),'Shin.'+side)
    bone('Toe.'+side,(s*1.04,.48,.16),(s*1.04,.17,.11),'Foot.'+side)
assert len(bone_defs)==33
rest={name:(h,t,p) for name,h,t,p in bone_defs}

def mouth_weight(point,head_mix=0):
    angle=math.atan2(point[2]-MAW.z,point[0])%math.tau
    sector=angle/(math.pi/2);i=int(sector);blend=sector-i
    result={mouth_controls[i%4]:(1-blend)*(1-head_mix),mouth_controls[(i+1)%4]:blend*(1-head_mix)}
    if head_mix:result['Head']=head_mix
    return {name:value for name,value in result.items() if value>.00001}

# The back, neck and face opening are a continuous surface, with no false cap
# over the mouth. A separate inward-facing funnel supplies real throat depth.
loft([(0,1.72,1.54),(0,1.53,1.74),(0,1.17,1.91),(0,.76,2.08),(0,.34,2.28),
      (0,-.10,2.40),(0,-.48,2.40),(0,-.80,2.32),(0,-1.10,2.22),(0,-1.44,2.10),(0,-1.76,2.07),(0,-2.015,2.07)],
     [(.10,.15),(.48,.50),(.67,.67),(.82,.77),(.91,.82),(.97,.86),(.98,.82),(.89,.74),(.75,.68),(.67,.64),(.635,.645),(.625,.665)],16,
     ['Pelvis','Pelvis',{'Pelvis':.7,'Spine_01':.3},'Spine_01',{'Spine_01':.5,'Spine_02':.5},'Spine_02',
      {'Spine_02':.4,'Chest':.6},'Chest',{'Chest':.2,'Neck_01':.8},{'Neck_01':.25,'Neck_02':.75},
      lambda p:mouth_weight(p,.80),lambda p:mouth_weight(p,.20)],(1,2,3,4,6,7),caps=(True,False),jitter=.018)

# Large masses, bent hocks and the low forefeet define the crawling silhouette.
for s,side in [(1,'L'),(-1,'R')]:
    rock((s*.64,.91,1.49),(.58,.69,.65),'Pelvis',(0,1,2,3,6),n=9,rings=5,rough=.028)
    rock((s*.93,-.48,2.20),(.60,.63,.68),{'Shoulder.'+side:.25,'UpperArm.'+side:.75},(1,2,3,4,6),n=10,rings=5,rough=.035)
    upper=rest['UpperArm.'+side][0];elbow=rest['Forearm.'+side][0];wrist=rest['Hand.'+side][0]
    points=[upper.lerp(elbow,t) for t in [0,.25,.6,1]]+[elbow.lerp(wrist,t) for t in [.32,.65,1]]
    loft(points,[(.46,.48),(.49,.46),(.41,.40),(.32,.32),(.35,.32),(.34,.31),(.29,.27)],10,
         ['UpperArm.'+side,'UpperArm.'+side,'UpperArm.'+side,{'UpperArm.'+side:.5,'Forearm.'+side:.5},'Forearm.'+side,'Forearm.'+side,{'Forearm.'+side:.6,'Hand.'+side:.4}],(0,1,2,3,4,6),jitter=.02)
    rock((s*1.47,-1.28,.33),(.43,.40,.27),'Hand.'+side,(1,2,3,4,6),n=8,rings=4,rough=.025)
    for j in range(4):
        x=s*(1.13+j*.225);fan=(j-1.5)*.065*s
        group=('Digit_01.' if j<2 else 'Digit_02.')+side
        # Tapered digits lead into hooked burgundy claws, with solid geometry.
        loft([(x,-1.39,.30),(x+fan*.4,-1.63,.23),(x+fan,-1.77,.15)],
             [(.13,.14),(.14,.12),(.10,.09)],6,[{'Hand.'+side:.7,group:.3},group,group],(0,1,2,3,6),jitter=.015)
        shard((x+fan,-1.74,.19),(x+fan*1.8,-2.09+abs(j-1.5)*.045,.055),.105,.078,group,(16,17,18,19),sides=5)
        rock((x+fan*.4,-1.61,.29),(.13,.15,.10),group,(1,2,3,4),n=5,rings=3,rough=.02)
    # A fifth, opposing inner digit follows the hand bone economically.
    loft([(s*1.18,-1.22,.36),(s*.99,-1.40,.24),(s*.94,-1.58,.16)],
         [.16,.135,.085],6,'Hand.'+side,(0,1,2,3),jitter=.02)
    shard((s*.94,-1.57,.17),(s*.82,-1.83,.055),.09,.065,'Hand.'+side,(16,17,18,19),sides=5)
    hip=rest['Thigh.'+side][0];knee=rest['Shin.'+side][0];ankle=rest['Foot.'+side][0]
    loft([hip,hip.lerp(knee,.35),hip.lerp(knee,.7),knee,knee.lerp(ankle,.45),ankle],
         [(.42,.46),(.48,.45),(.41,.40),(.28,.29),(.25,.26),(.22,.22)],9,
         ['Thigh.'+side,'Thigh.'+side,'Thigh.'+side,{'Thigh.'+side:.5,'Shin.'+side:.5},'Shin.'+side,'Shin.'+side],(0,1,2,3,6),jitter=.02)
    rock((s*1.04,.67,.19),(.34,.48,.19),'Foot.'+side,(0,1,2,3),n=8,rings=4,rough=.012)
    for j in range(3):
        x=s*(.82+j*.22)
        loft([(x,.48,.20),(x,.24,.14),(x,.12,.10)],[.12,.12,.08],5,'Toe.'+side,(0,1,2,3),jitter=.01)
        shard((x,.15,.14),(x,-.10,.045),.083,.06,'Toe.'+side,(16,17,18,19),sides=5)
    # Skin nodules share the surface's palette, without separate armor pieces.
    for j in range(7):
        a=-.25+j*.45
        center=(s*(1.02+.38*math.cos(a)),-.47+.36*math.sin(a),2.31+(.22 if j%2 else -.1))
        rock(center,(.22,.23,.20),'UpperArm.'+side,(1,2,3,4,6),n=6,rings=3,rough=.035)
    for j in range(4):
        center=(s*(1.51+.03*(j%2)),-.51-j*.15,1.32-j*.19)
        rock(center,(.19,.20,.16),'Forearm.'+side,(1,2,3,4,6),n=6,rings=3,rough=.03)
    for j in range(4):
        rock((s*(.91+.14*(j%2)),1.18-(j//2)*.24,1.20-(j//2)*.22),(.25,.22,.19),'Thigh.'+side,(0,1,2,3,6),n=6,rings=3,rough=.035)

# Irregular, broad skin facets run over the arch of the back and both flanks.
for y,z,rx_,rz_,weight in [(1.05,1.94,.69,.64,'Pelvis'),(.60,2.18,.86,.77,'Spine_01'),(.12,2.35,.94,.84,'Spine_02'),(-.40,2.40,.95,.77,'Chest')]:
    for j in range(9):
        a=.10+j*(math.pi-.20)/8
        center=(rx_*math.cos(a),y+random.uniform(-.10,.10),z+rz_*math.sin(a))
        rock(center,(random.uniform(.23,.30),random.uniform(.23,.32),.14),weight,(1,2,3,4,6,7),n=6,rings=3,rotation=(0,math.pi/2-a,0),rough=.035)
for j in range(8):
    y=-.68+j*.31
    z=3.04-.16*(abs(y-.15)/1.15)**1.5
    shard((0,y,z-.12),(random.uniform(-.07,.07),y+.07,z+.22),.19,.20,'BackRidge',(1,2,3,4,6),sides=5)

# The lips are a true torus. Neither an eye socket nor a flat mouth decal is
# used. Two staggered tooth rings point inward toward the deep opening.
lip_rows=[]
for i in range(24):
    a=math.tau*i/24;ring=[]
    for j in range(6):
        b=math.tau*j/6
        p=MAW+Vector(((.55+.105*math.cos(b))*math.cos(a),-.10*math.sin(b),(.60+.105*math.cos(b))*math.sin(a)))
        ring.append(vtx(p,mouth_weight(p)))
    lip_rows.append(ring)
for i in range(24):
    for j in range(6):
        face((lip_rows[i][j],lip_rows[(i+1)%24][j],lip_rows[(i+1)%24][(j+1)%6],lip_rows[i][(j+1)%6]),(8,9,10,11,12))
CAVITY_START=len(faces)
loft([(0,-2.09,2.07),(0,-1.92,2.07),(0,-1.67,2.08),(0,-1.43,2.10),(0,-1.18,2.12)],
     [(.445,.495),(.42,.46),(.32,.35),(.18,.20),(.025,.035)],20,
     [lambda p:mouth_weight(p,.05),lambda p:mouth_weight(p,.20),lambda p:mouth_weight(p,.55),'Head','Head'],(20,21,22,23),caps=(False,True),jitter=0)
CAVITY_END=len(faces)
for i in range(22):
    a=math.tau*i/22
    base=MAW+Vector((.445*math.cos(a),-.075,.488*math.sin(a)))
    tip=MAW+Vector(((.245+random.uniform(-.035,.025))*math.cos(a+.055),-.045,(.27+random.uniform(-.025,.025))*math.sin(a+.055)))
    shard(base,tip,.070+random.uniform(-.008,.008),.061,mouth_weight(base),(12,13,14,24,25),sides=5)
for i in range(14):
    a=math.tau*(i+.5)/14
    base=Vector((.34*math.cos(a),-1.82,2.07+.37*math.sin(a)))
    tip=Vector((.205*math.cos(a+.08),-1.80,2.07+.225*math.sin(a+.08)))
    shard(base,tip,.052,.042,mouth_weight(base,.20),(9,12,13,25),sides=4)

# Place each rune by ray-projecting it onto the real skin triangles, and
# barycentrically interpolate the skin weights. The markings cannot float
# above a bending limb or drift away from its surface.
from mathutils.bvhtree import BVHTree
skin_tris=[(f[0],f[i],f[i+1]) for f in faces for i in range(1,len(f)-1)]
skin_bvh=BVHTree.FromPolygons(verts,skin_tris,all_triangles=True)
rune_face_normals={}
def projected_vertex(p,normal):
    co,n,index,_=skin_bvh.ray_cast(Vector(p)+normal*2.5,-normal,5)
    if co is None:raise RuntimeError('Rune projection missed the skin: '+str(p))
    tri=skin_tris[index];a,b,c=[Vector(verts[k]) for k in tri]
    e0,e1,d=b-a,c-a,co-a
    aa,bb,cc=e0.dot(e0),e0.dot(e1),e1.dot(e1)
    den=aa*cc-bb*bb
    v=(cc*d.dot(e0)-bb*d.dot(e1))/den;w=(aa*d.dot(e1)-bb*d.dot(e0))/den
    influence={}
    for k,bary in zip(tri,[1-v-w,v,w]):
        for name,value in weights[k].items():influence[name]=influence.get(name,0)+max(0,bary)*value
    total=sum(influence.values());influence={name:value/total for name,value in influence.items() if value/total>.0001}
    # The BVH is built before the final winding repair; orient its hit normal
    # toward the casting surface so an inscription cannot be buried in skin.
    if n.dot(normal)<0:n.negate()
    return vtx(co+n*.007,influence)
def surface_rune(strokes,center,u,v,scale,width=.025):
    center,u,v=Vector(center),Vector(u),Vector(v)
    normal=u.cross(v).normalized()
    for stroke in strokes:
        coords=[]
        for first,second in zip(stroke,stroke[1:]):
            a,b=Vector(first),Vector(second)
            count=max(1,math.ceil((b-a).length*scale/.055))
            coords.extend(a.lerp(b,i/count) for i in range(count))
        coords.append(Vector(stroke[-1]));pairs=[]
        for i,p in enumerate(coords):
            delta=coords[min(i+1,len(coords)-1)]-coords[max(0,i-1)]
            side=Vector((-delta.y,delta.x)).normalized()*width/(2*scale)
            pair=[]
            for d in [-1,1]:
                q=p+side*d
                pair.append(projected_vertex(center+u*q.x*scale+v*q.y*scale,normal))
            pairs.append(pair)
        for i in range(len(pairs)-1):
            rune_face_normals[len(faces)]=normal.copy()
            face((pairs[i][0],pairs[i][1],pairs[i+1][1],pairs[i+1][0]),28,True)
TRIANGLE=[[(-.85,-.70),(0,.95),(.85,-.70),(-.85,-.70)]]
CIRCLE=[[(math.cos(i*math.tau/20),math.sin(i*math.tau/20)) for i in range(21)]]
for s in [-1,1]:
    # u x v points outward from the chosen flank.
    u=(0,-s,0);v=(0,0,1)
    surface_rune(SIGMA,(s*1.1,-.48,2.49),u,v,.18,.026)
    surface_rune(CUBE,(s*1.2,-.50,1.85),u,v,.145,.023)
    surface_rune(TRIANGLE,(s*1.52,-.85,.95),u,v,.15,.025)
    surface_rune(FORALL,(s*.85,.30,2.55),u,v,.16,.024)
    surface_rune(CIRCLE,(s*.90,1.10,1.69),u,v,.10,.019)
    # Front-facing shoulder and forearm inscriptions remain legible at the
    # normal encounter camera angle as well as in side/top model views.
    surface_rune(SIGMA if s==1 else CUBE,(s*1.13,-.85,2.40),(1,0,0),(0,0,1),.16,.027)
    surface_rune(TRIANGLE,(s*1.46,-1.0,1.12),(1,0,0),(0,0,1),.12,.024)
surface_rune(SIGMA,(.23,.00,3.2),(1,0,0),(0,1,0),.17,.025)
surface_rune(CUBE,(-.26,.68,2.98),(1,0,0),(0,1,0),.14,.023)

def triangle_total():return sum(len(f)-2 for f in faces)
print('GEOMETRY_TRIANGLES',triangle_total(),flush=True)
assert 5000<=triangle_total()<=8000,triangle_total()

mesh=bpy.data.meshes.new('ProofDevourer_SkinnedMesh')
mesh.from_pydata(verts,[],faces);mesh.update()
body=bpy.data.objects.new('ProofDevourer_Body',mesh);asset_collection.objects.link(body)
for mat in materials:mesh.materials.append(mat)
uv=mesh.uv_layers.new(name='AtlasUV')
for polygon,color,material in zip(mesh.polygons,face_colors,face_mats):
    polygon.material_index=material
    for j,index in enumerate(polygon.loop_indices):
        a=math.tau*j/len(polygon.loop_indices)
        uv.data[index].uv=((color%8+.5+.24*math.cos(a))/8,(color//8+.5+.24*math.sin(a))/8)
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
bm.faces.ensure_lookup_table()
for i in range(CAVITY_START,CAVITY_END):
    f=bm.faces[i];center=f.calc_center_median();radial=Vector((center.x,0,center.z-MAW.z))
    if (radial.length>.06 and f.normal.dot(radial)>0) or (radial.length<=.06 and f.normal.y>0):f.normal_flip()
for index,normal in rune_face_normals.items():
    if bm.faces[index].normal.dot(normal)<0:bm.faces[index].normal_flip()
bmesh.ops.triangulate(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
for name,_,_,_ in bone_defs:
    group=body.vertex_groups.new(name=name)
    for i,w in enumerate(weights):
        if name in w:group.add([i],w[name],'REPLACE')
rig_data=bpy.data.armatures.new('ProofDevourer_Skeleton')
rig=bpy.data.objects.new('DevourerRig',rig_data);asset_collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,h,t,parent in bone_defs:
    b=rig_data.edit_bones.new(name);b.head=h;b.tail=t
    if parent:b.parent=rig_data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
body.parent=rig
modifier=body.modifiers.new('Proof Devourer skeleton','ARMATURE');modifier.object=rig
rig.show_in_front=True;rig_data.display_type='STICK'
for pb in rig.pose.bones:pb.rotation_mode='QUATERNION'
rig['anatomy']='Blind quadruped; no eyes, wings or tail; four radial mouth controls.'
body['reference']='The Proof Devourer Asset Sheet0.png supplied by the user; original modeled geometry.'

def rx(a):return Matrix.Rotation(math.radians(a),4,'X')
def ry(a):return Matrix.Rotation(math.radians(a),4,'Y')
def rz(a):return Matrix.Rotation(math.radians(a),4,'Z')
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
def mix(a,b,t):return a+(b-a)*t
def solve_ik(a,c,l1,l2,pole):
    delta=c-a;d=max(.0001,min(delta.length,l1+l2-.0001));along=delta.normalized()
    mid=(d*d+l1*l1-l2*l2)/(2*d);height=math.sqrt(max(.0001,l1*l1-mid*mid))
    toward=Vector(pole)-a;toward=(toward-along*toward.dot(along)).normalized()
    return a+along*mid+toward*height
SLEEP=dict(drop=-.70,pitch=16,head=-15,mouth=-.20,arms=0)
W1=dict(drop=-.68,pitch=15,head=-14,mouth=-.19,arms=.035)
W2=dict(drop=-.48,pitch=11,head=-8,mouth=-.10,arms=.25)
W3=dict(drop=-.25,pitch=6,head=-4,mouth=-.03,arms=.67)
IDLE=dict(drop=0,pitch=0,head=0,mouth=0,arms=1)
def blend_pose(a,b,t):return {k:mix(a[k],b[k],smooth(t)) for k in a}
CLIPS=[('Sleep',4.4,True),('Wake_01',1.8,False),('Wake_02',2.2,False),('Wake_03',2.2,False),('Full_Awaken',3.0,False),('Idle',3.4,True),('Chase',.8,True),('Capture',1.8,False),('Jumpscare',1.8,False),('Barrier_Cast',3.0,False),('Attack',1.8,False),('Return_To_Sleep',3.2,False),('Stunned',1.8,False)]
def pose_params(name,t):
    p=dict(IDLE);wave=math.sin(math.tau*t)
    if name=='Sleep':
        p=dict(SLEEP);p['drop']+=.022*(1-math.cos(math.tau*t));p['mouth']+=.008*wave
    elif name.startswith('Wake_'):
        a,b={'Wake_01':(SLEEP,W1),'Wake_02':(W1,W2),'Wake_03':(W2,W3)}[name]
        p=blend_pose(a,b,t);p['head']-=2*math.sin(math.pi*t);p['twitch']=-8*math.sin(math.pi*t)
    elif name=='Full_Awaken':
        p=blend_pose(W3,IDLE,min(1,t/.40));roar=math.sin(math.pi*max(0,min(1,(t-.18)/.72)))**2
        p.update(mouth=p['mouth']+.17*roar,head=p['head']-7*roar,pitch=p['pitch']-3*roar,roar=roar)
    elif name=='Idle':
        p.update(drop=.022*(1-math.cos(math.tau*t)),head=1.4*wave,mouth=.012*wave,sway=.7*wave)
    elif name=='Chase':
        p.update(drop=.035+.055*math.cos(2*math.tau*t),pitch=3+2*wave,head=-2,mouth=.07,gait=math.tau*t)
    elif name=='Capture':
        amount=smooth(t/.32)*(1-smooth((t-.72)/.28))
        p.update(reach=amount,grip=smooth((t-.31)/.18)*(1-smooth((t-.80)/.20)),mouth=.10*amount,pitch=3*amount,neck=.12*amount)
    elif name=='Jumpscare':
        amount=smooth(t/.25)*(1-smooth((t-.73)/.27))
        p.update(neck=.38*amount,pitch=6*amount,head=-4*amount,mouth=.235*amount,lunge=amount)
    elif name=='Barrier_Cast':
        amount=smooth(t/.28)*(1-smooth((t-.76)/.24))
        p.update(cast=amount,pitch=-4*amount,mouth=.11*amount,drop=.04*amount)
    elif name=='Attack':
        # Three planted supports carry the weight while the right claw cuts across
        # the front. Wind-up .00-.38s, fast slash .40-.74s, settle .88-1.58s.
        seconds=t*1.8;wind=smooth(seconds/.38);cut=smooth((seconds-.40)/.34);settle=smooth((seconds-.88)/.70)
        effort=wind*(1-settle)
        p.update(slash=effort,slash_cut=cut,shift_x=.09*effort,
                 drop=(-.05-.025*math.sin(math.pi*cut))*effort,
                 pitch=(-1+5*cut)*effort,twist=(-11+24*cut)*effort,
                 head=-2*effort,head_yaw=(3-6*cut)*effort,
                 mouth=.045*effort,slash_curl=12*effort)
    elif name=='Return_To_Sleep':p=blend_pose(IDLE,SLEEP,t)
    elif name=='Stunned':
        amount=smooth(t/.13)*(1-smooth((t-.48)/.52))
        p.update(stun=amount,pitch=-7*amount,drop=-.045*amount,mouth=.09*amount,sway=5*amount*math.sin(3*math.pi*t))
    return p

def target_matrices(p,t):
    out={};pivot=rest['Pelvis'][0];body_rotation=rz(p.get('twist',0))@ry(p.get('sway',0))@rx(p['pitch'])
    def torso_point(x):return pivot+Vector((p.get('shift_x',0),0,p['drop']))+body_rotation.to_3x3()@(Vector(x)-pivot)
    def segment(name,a,b,extra=None):
        h,tail,_=rest[name];rotation=(tail-h).normalized().rotation_difference((b-a).normalized())
        orient=rotation.to_matrix().to_4x4()@rig_data.bones[name].matrix_local.to_3x3().to_4x4()
        if extra is not None:orient=extra@orient
        out[name]=Matrix.Translation(a)@orient
    segment('Root',Vector((0,0,0)),Vector((0,0,.3)))
    for name in ['Pelvis','Spine_01','Spine_02','Chest']:
        h,tail,_=rest[name];segment(name,torso_point(h),torso_point(tail))
    neck_extension=Vector((0,-p.get('neck',0),0))
    h,tail,_=rest['Neck_01'];segment('Neck_01',torso_point(h),torso_point(tail)+neck_extension*.36)
    h,tail,_=rest['Neck_02'];segment('Neck_02',torso_point(h)+neck_extension*.36,torso_point(tail)+neck_extension)
    head_position=torso_point(rest['Head'][0])+neck_extension
    head_rotation=body_rotation@rz(p.get('head_yaw',0))@rx(p['head'])
    def hp(x):return head_position+head_rotation.to_3x3()@(Vector(x)-rest['Head'][0])
    segment('Head',head_position,hp(rest['Head'][1]))
    for i,name in enumerate(mouth_controls):
        h,tail,_=rest[name];a=i*math.pi/2
        radial=Vector((math.cos(a),0,math.sin(a)))*p['mouth']
        segment(name,hp(h+radial),hp(tail+radial))
    h,tail,_=rest['BackRidge'];segment('BackRidge',torso_point(h),torso_point(tail),extra=rx(.8*math.sin(math.tau*t)))
    gait=p.get('gait')
    for s,side in [(1,'L'),(-1,'R')]:
        shoulder=torso_point(rest['UpperArm.'+side][0]);h,_,_=rest['Shoulder.'+side]
        segment('Shoulder.'+side,torso_point(h),shoulder)
        wrist=Vector((s*1.16,-1.42,.44)).lerp(rest['Hand.'+side][0],p['arms'])
        hand_rotation=Matrix.Identity(4)
        if gait is not None:
            phase=gait+(0 if s==1 else math.pi)
            wrist.y+=.62*math.cos(phase);wrist.z+=.35*max(0,math.sin(phase))
            hand_rotation=rx(-16*max(0,math.sin(phase)))
        reach=p.get('reach',0);cast=p.get('cast',0)
        wrist=wrist.lerp(Vector((s*.86,-2.10,.88)),reach)
        wrist=wrist.lerp(Vector((s*1.42,-1.26,1.80)),cast)
        wrist+=Vector((s*.10,0,.10))*p.get('roar',0)
        hand_rotation=rz(-s*25*cast)@rx(-25*cast-22*reach)@hand_rotation
        if s==-1:
            wrist=wrist.lerp(Vector((-1.6,-.18,1.65)),p.get('load',0))
            wrist=wrist.lerp(Vector((-.27,-2.10,.60)),p.get('strike',0))
            if 'slash' in p:
                cut=p['slash_cut'];effort=p['slash']
                sweep=Vector((-1.90,-.95,1.45)).lerp(Vector((.25,-1.65,.85)),cut)
                sweep+=Vector((0,-.40*math.sin(math.pi*cut),.08*math.sin(math.pi*cut)))
                wrist=wrist.lerp(sweep,effort)
                hand_rotation=rz(-32*cut*effort)@ry(8*effort)@rx((-22+8*cut)*effort)
        stun=p.get('stun',0)
        if s==1:wrist+=Vector((.26,.08,.18))*stun
        if 'slash' in p:
            maximum=((rest['UpperArm.'+side][1]-rest['UpperArm.'+side][0]).length+(rest['Forearm.'+side][1]-rest['Forearm.'+side][0]).length)-.025
            reach_vector=wrist-shoulder
            if reach_vector.length>maximum:wrist=shoulder+reach_vector.normalized()*maximum
        elbow=solve_ik(shoulder,wrist,(rest['UpperArm.'+side][1]-rest['UpperArm.'+side][0]).length,(rest['Forearm.'+side][1]-rest['Forearm.'+side][0]).length,(s*2.8,.45,1.45))
        segment('UpperArm.'+side,shoulder,elbow);segment('Forearm.'+side,elbow,wrist)
        handrest=rest['Hand.'+side][0]
        def handpoint(x):return wrist+hand_rotation.to_3x3()@(Vector(x)-handrest)
        segment('Hand.'+side,wrist,handpoint(rest['Hand.'+side][1]))
        for prefix in ['Digit_01.','Digit_02.']:
            name=prefix+side;h,tail,_=rest[name];point=handpoint(h)
            curl=rx(30*p.get('grip',0)+p.get('twitch',0)+(p.get('slash_curl',0) if s==-1 else 0))
            segment(name,point,point+(hand_rotation@curl).to_3x3()@(tail-h))
        hip=torso_point(rest['Thigh.'+side][0]);ankle=rest['Foot.'+side][0].copy()
        if gait is not None:
            phase=gait+(math.pi if s==1 else 0)
            ankle.y+=.50*math.cos(phase);ankle.z+=.28*max(0,math.sin(phase))
        knee=solve_ik(hip,ankle,(rest['Thigh.'+side][1]-rest['Thigh.'+side][0]).length,(rest['Shin.'+side][1]-rest['Shin.'+side][0]).length,(s*1.8,2.5,1.0))
        segment('Thigh.'+side,hip,knee);segment('Shin.'+side,knee,ankle)
        shift=ankle-rest['Foot.'+side][0]
        for prefix in ['Foot.','Toe.']:
            name=prefix+side;h,tail,_=rest[name];segment(name,h+shift,tail+shift)
    return out

def action_curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:yield from bag.fcurves
animation_info=[]
for name,duration,loop in CLIPS:
    action=bpy.data.actions.new(name);action.use_fake_user=True
    rig.animation_data_create();rig.animation_data.action=action
    count=round(duration*30);previous_quaternions={}
    for frame in range(count+1):
        t=frame/count;matrices=target_matrices(pose_params(name,t),t)
        for bname,_,_,parent in bone_defs:
            b=rig_data.bones[bname];pb=rig.pose.bones[bname]
            basis=b.matrix_local.inverted()@(rig_data.bones[parent].matrix_local@matrices[parent].inverted() if parent else Matrix.Identity(4))@matrices[bname]
            loc,quat,scale=basis.decompose()
            if bname in previous_quaternions and quat.dot(previous_quaternions[bname])<0:quat.negate()
            previous_quaternions[bname]=quat.copy()
            pb.location=loc;pb.rotation_quaternion=quat;pb.scale=scale
            for path in ['location','rotation_quaternion','scale']:pb.keyframe_insert(data_path=path,frame=frame+1,group=bname)
    for curve in action_curves(action):
        for key in curve.keyframe_points:key.interpolation='LINEAR'
    action['loop']=loop;action['duration_seconds']=duration;action['in_place']=True
    animation_info.append(dict(name=name,duration=duration,loop=loop,frames=count+1))
    print('ACTION_CREATED',name,flush=True)

# Evaluate the actual skinned surface, with no reliance on labels or empty takes.
validation=dict(triangles=len(mesh.polygons),vertices=len(mesh.vertices),bones=len(rig_data.bones),materials=2,animation_count=len(CLIPS),clips=animation_info,
                unweighted_vertices=sum(not bool(w) for w in weights),max_influences=max(len(w) for w in weights),loop_pose_max_error={},motion_max_distance={},bounds_by_clip={},awakening_stage_boundary_errors={},no_eyes_wings_or_tail=True)
assert validation['unweighted_vertices']==0
assert validation['max_influences']<=4
def sample_coords(name,frame):
    rig.animation_data.action=bpy.data.actions[name];scene.frame_set(frame)
    evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    return np.array([tuple(v.co) for v in evaluated.data.vertices])
for name,duration,loop in CLIPS:
    samples=[sample_coords(name,round(duration*30*t)+1) for t in [0,.25,.5,.75,1]]
    assert np.isfinite(samples).all(),name
    validation['motion_max_distance'][name]=float(max(np.linalg.norm(x-samples[0],axis=1).max() for x in samples[1:]))
    validation['bounds_by_clip'][name]={'min':np.min(samples,axis=(0,1)).tolist(),'max':np.max(samples,axis=(0,1)).tolist()}
    if loop:
        error=float(np.linalg.norm(samples[0]-samples[-1],axis=1).max());validation['loop_pose_max_error'][name]=error
        assert error<.001,(name,error)
    assert validation['motion_max_distance'][name]>.005,name
for first,second in [('Sleep','Wake_01'),('Wake_01','Wake_02'),('Wake_02','Wake_03'),('Wake_03','Full_Awaken'),('Full_Awaken','Idle'),('Return_To_Sleep','Sleep')]:
    duration=next(d for n,d,_ in CLIPS if n==first)
    error=float(np.linalg.norm(sample_coords(first,round(duration*30)+1)-sample_coords(second,1),axis=1).max())
    validation['awakening_stage_boundary_errors'][first+' -> '+second]=error
    assert error<.001,(first,second,error)
validation['limits']='No Android device profiling or player build has been performed.'
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_start=1;scene.frame_end=103;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
# Rotate the export assembly only: Unity prefab front is +Z and scales are one.
rig.rotation_euler.z=math.pi;bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(BASE/'Models/ProofDevourer.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
    use_space_transform=True,bake_space_transform=False,mesh_smooth_type='FACE',add_leaf_bones=False,use_armature_deform_only=False,
    bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_step=1,bake_anim_simplify_factor=.10,
    bake_anim_force_startend_keying=True,path_mode='RELATIVE',use_mesh_modifiers=True)
rig.rotation_euler.z=0;bpy.context.view_layer.update()

# Source-only studio. No scene props or lighting are included in the FBX.
studio=bpy.data.collections.new('STUDIO | excluded from export');scene.collection.children.link(studio)
def move_studio(obj):
    for c in list(obj.users_collection):c.objects.unlink(obj)
    studio.objects.link(obj)
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.02));floor=bpy.context.object;floor.name='Review floor';move_studio(floor)
mat=bpy.data.materials.new('Studio floor (not exported)');mat.use_nodes=True
mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.037,.043,.052,1)
mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.93;floor.data.materials.append(mat)
bpy.ops.object.camera_add(location=(6.5,-10,4.9));camera=bpy.context.object;move_studio(camera)
camera.name='Review camera';camera.data.type='ORTHO';camera.data.ortho_scale=5.9;aim(camera,(0,-.2,1.60));scene.camera=camera
for name,loc,power,size,color in [('Key',(-4,-6,7),1400,5,(1,.85,.80)),('Fill',(5,-4,4),1000,4,(.69,.80,1)),('Rim',(1,4,6),1700,4,(1,.45,.40))]:
    bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;move_studio(light)
    light.name='Studio '+name;light.data.energy=power;light.data.shape='DISK';light.data.size=size;light.data.color=color;aim(light,(0,0,1.5))
scene.world.color=(.16,.16,.16);scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=7
            area.spaces.active.region_3d.view_location=Vector((0,0,1.6));area.spaces.active.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Models/ProofDevourer.blend'))
(BASE/'Previews/BlenderValidation.json').write_text(json.dumps(validation,indent=2),encoding='utf-8')
scene.render.filepath=str(BASE/'Previews/ProofDevourer_Hero.png');bpy.ops.render.render(write_still=True)
print('PROOF_DEVOURER_COMPLETE',json.dumps(validation),flush=True)

