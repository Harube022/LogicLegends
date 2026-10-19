"""Rebuild the Primal Axiom game asset in Blender 5.0 (no add-ons required).

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
random.seed(917)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for block in list(bpy.data.actions):
    bpy.data.actions.remove(block)
scene = bpy.context.scene
scene.name = 'Primal Axiom | Asset and animation review'
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.fps = 30
asset_collection = bpy.data.collections.new('CHARACTER | export only this collection')
scene.collection.children.link(asset_collection)

# UV islands live inside padded palette tiles. Variation is painted per facet,
# with a very subtle mineral grain; there are no runtime procedural shaders.
palette = [
    (0.145, .123, .112), (.20, .17, .143), (.245, .205, .165), (.31, .26, .207),
    (.38, .315, .245), (.12, .115, .109), (.265, .25, .223), (.35, .32, .277),
    (.32, .135, .075), (.48, .215, .105), (.61, .29, .135), (.23, .073, .046),
    (.73, .43, .135), (.87, .59, .24), (.98, .76, .39), (1., .89, .63),
    (.58, .34, .12), (.36, .22, .091), (.16, .098, .043), (.72, .51, .245),
    (.055, .022, .018), (.24, .045, .025), (.56, .12, .07), (.74, .28, .13),
    (.98, .82, .53), (.83, .66, .39), (.07, .046, .029), (.42, .29, .16),
    (1., .43, .025), (1., .70, .14), (1., .20, .012), (1., .91, .46),
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
atlas = make_atlas('PrimalAxiom_BaseColor', 1024)
emission_atlas = make_atlas('PrimalAxiom_Emission', 512, True)
def make_material(name, glow=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = .86 if not glow else .58
    shader.inputs['Metallic'].default_value = .05
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = atlas
    mat.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
    if glow:
        emission = mat.node_tree.nodes.new('ShaderNodeTexImage')
        emission.image = emission_atlas
        mat.node_tree.links.new(emission.outputs['Color'], shader.inputs['Emission Color'])
        shader.inputs['Emission Strength'].default_value = 2.2
    return mat
materials = [make_material('Axiom_StoneGold'), make_material('Axiom_RuneEmissive', True)]

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
            ring.append(vtx(p + rough * (u*r1*math.cos(a) + v*r2*math.sin(a)), w))
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
    if caps:
        face(tuple(reversed(rings[0])), colors)
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

# Explicit 34-bone hierarchy. Bone axes are derived from their rest segments;
# actions are baked from an analytic two-bone solver, so no export constraints.
bone_defs=[]
def bone(name, head, tail, parent=None):
    bone_defs.append((name,Vector(head),Vector(tail),parent))
bone('Root',(0,0,0),(0,0,.35))
bone('Pelvis',(0,.30,1.30),(0,.23,1.8),'Root')
bone('Spine_01',(0,.23,1.8),(0,.10,2.40),'Pelvis')
bone('Chest',(0,.10,2.4),(0,0,3.02),'Spine_01')
bone('Neck',(0,0,3.02),(0,-.55,3.33),'Chest')
bone('Head',(0,-.55,3.33),(0,-.64,4.04),'Neck')
bone('Jaw',(0,-1.05,3.25),(0,-1.52,3.08),'Head')
bone('Mane',(0,-.38,3.78),(0,-.18,4.30),'Head')
bone('Beard',(0,-1.07,3.03),(0,-1.00,2.51),'Jaw')
bone('Tablets',(0,-.64,2.72),(0,-.72,1.41),'Chest')
for s,side in [(1,'L'),(-1,'R')]:
    bone('Shoulder.'+side,(0,0,2.95),(s*1.03,.03,2.90),'Chest')
    bone('UpperArm.'+side,(s*1.03,.03,2.90),(s*1.58,-.06,1.88),'Shoulder.'+side)
    bone('Forearm.'+side,(s*1.58,-.06,1.88),(s*1.92,-.51,.91),'UpperArm.'+side)
    bone('Hand.'+side,(s*1.92,-.51,.91),(s*1.94,-.86,.58),'Forearm.'+side)
    bone('Finger_01.'+side,(s*1.72,-.83,.55),(s*1.73,-.98,.24),'Hand.'+side)
    bone('Finger_02.'+side,(s*2.13,-.83,.55),(s*2.14,-.98,.24),'Hand.'+side)
    bone('Thumb.'+side,(s*1.53,-.61,.72),(s*1.40,-.99,.54),'Hand.'+side)
    bone('Thigh.'+side,(s*.58,.32,1.31),(s*.76,.40,.72),'Pelvis')
    bone('Shin.'+side,(s*.76,.40,.72),(s*.69,-.05,.24),'Thigh.'+side)
    bone('Foot.'+side,(s*.69,-.05,.24),(s*.69,-.50,.13),'Shin.'+side)
    bone('Toe.'+side,(s*.69,-.50,.13),(s*.69,-.80,.10),'Foot.'+side)
    bone('Brow.'+side,(s*.26,-1.40,3.62),(s*.39,-1.43,3.64),'Head')
assert len(bone_defs)==34
rest={name:(head,tail,parent) for name,head,tail,parent in bone_defs}

# A continuous weighted torso core and limbs fill the armor gaps.
loft([(0,.31,1.13),(0,.29,1.4),(0,.25,1.77),(0,.14,2.1),(0,.09,2.44),(0,.08,2.78),(0,.09,3.02),(0,.13,3.21)],
     [(.47,.43),(.64,.52),(.70,.58),(.88,.63),(1.06,.66),(1.19,.63),(1.02,.54),(.62,.39)],14,
     ['Pelvis','Pelvis',{'Pelvis':.5,'Spine_01':.5},'Spine_01',{'Spine_01':.45,'Chest':.55},'Chest','Chest','Chest'],(0,1,2,5,6))
for s,side in [(1,'L'),(-1,'R')]:
    rock((s*.63,-.40,2.61),(.66,.31,.53),'Chest',(1,2,3,4),n=9,rings=4)
    rock((s*.66,.48,2.72),(.65,.33,.57),'Chest',(0,1,2,3),n=8,rings=4)
    rock((s*1.14,.02,2.83),(.67,.61,.62),{'Shoulder.'+side:.3,'UpperArm.'+side:.7},(0,1,2,3),n=9,rings=5)
    p0=rest['UpperArm.'+side][0]; p1=rest['Forearm.'+side][0]; p2=rest['Hand.'+side][0]
    points=[p0.lerp(p1,t) for t in [0,.28,.63,1]]+[p1.lerp(p2,t) for t in [.32,.67,1]]
    loft(points,[(.48,.50),(.53,.47),(.42,.41),(.36,.35),(.43,.39),(.47,.42),(.36,.33)],10,
         ['UpperArm.'+side,'UpperArm.'+side,'UpperArm.'+side,{'UpperArm.'+side:.45,'Forearm.'+side:.55},'Forearm.'+side,'Forearm.'+side,{'Forearm.'+side:.5,'Hand.'+side:.5}],(0,1,2,3,6))
    rock((s*1.94,-.69,.72),(.57,.48,.46),'Hand.'+side,(0,1,2,3,4),n=9,rings=4)
    # Four large articulated fingers share two economical phalange clusters.
    for j in range(4):
        x=s*(1.60+j*.225)
        group=('Finger_01.' if j<2 else 'Finger_02.')+side
        loft([(x,-.79,.65),(x,-1.00,.44),(x,-1.02,.23),(x,-.89,.17)],
             [(.155,.16),(.175,.17),(.15,.14),(.095,.10)],6,
             [{'Hand.'+side:.75,group:.25},group,group,group],(1,2,3,4))
        shard((x,-1.04,.31),(x,-1.21,.14),.105,.077,group,(25,27),sides=4)
        rock((x,-1.02,.56),(.165,.13,.17),group,(3,4,7),n=5,rings=3)
    loft([(s*1.57,-.58,.85),(s*1.40,-.79,.75),(s*1.38,-1.00,.55)],
         [.19,.19,.13],6,'Thumb.'+side,(1,2,3))
    shard((s*1.40,-1.02,.56),(s*1.43,-1.18,.39),.10,.075,'Thumb.'+side,(25,27),sides=4)
    hip,knee,_=rest['Thigh.'+side]; ankle=rest['Foot.'+side][0]
    loft([hip,hip.lerp(knee,.45),knee,knee.lerp(ankle,.48),ankle],
         [(.42,.44),(.42,.39),(.28,.27),(.28,.26),(.22,.22)],9,
         ['Thigh.'+side,'Thigh.'+side,{'Thigh.'+side:.5,'Shin.'+side:.5},'Shin.'+side,'Shin.'+side],(0,1,2,3))
    rock((s*.69,-.26,.21),(.36,.55,.21),'Foot.'+side,(0,1,2,3),n=8,rings=4,rough=.025)
    for j in range(3):
        x=s*(.46+j*.22)
        rock((x,-.67,.15),(.13,.23,.13),'Toe.'+side,(2,3,4),n=5,rings=3)
        shard((x,-.78,.15),(x,-.96,.075),.09,.055,'Toe.'+side,(25,27),sides=4)
    # Layered volcanic shoulder/arm plates have broad, angular shapes.
    for x,y,z,sc in [(1.17,-.40,3.04,(.42,.24,.35)),(1.55,-.22,2.86,(.28,.27,.34)),(1.40,.44,3.03,(.34,.22,.32)),(1.10,.39,3.28,(.35,.27,.22))]:
        rock((s*x,y,z),sc,'UpperArm.'+side,(2,3,4,7),n=6,rings=3)
    for j in range(3):
        rock((s*(1.7+j*.07),-.36-j*.10,1.64-j*.20),(.35,.20,.22),'Forearm.'+side,(1,2,3,4),n=6,rings=3)
    for j in range(3):
        shard((s*(1.05+j*.22),.13,3.22-j*.07),(s*(1.24+j*.28),.22,3.55-j*.06),.15,.14,'UpperArm.'+side,(12,13,16),sides=4)

# Cuffs are thick bands around the forearms, with inset diamond sigils.
def band(center, axis, radius, width, weight):
    center=Vector(center); axis=Vector(axis).normalized();u,v=frame_for(axis)
    rows=[]
    for t,r in [(-.5, radius*.92),(-.36,radius),(.36,radius),(.5,radius*.92)]:
        rows.append([vtx(center+axis*width*t+(u*math.cos(math.tau*j/10)+v*math.sin(math.tau*j/10))*r,weight) for j in range(10)])
    for i in range(3):
        for j in range(10):
            face((rows[i][j],rows[i][(j+1)%10],rows[i+1][(j+1)%10],rows[i+1][j]),(16,17,19))
for s,side in [(1,'L'),(-1,'R')]:
    axis=rest['Hand.'+side][0]-rest['Forearm.'+side][0]
    for t in [.23,.72]:
        center=rest['Forearm.'+side][0].lerp(rest['Hand.'+side][0],t)
        band(center,axis,.47,.19,'Forearm.'+side)
        # The visible front face of each cuff carries an emissive lozenge.
        x,y,z=center
        line_symbol([[(-.55,0),(0,1),(.55,0),(0,-1),(-.55,0)]],(x,y-.46,z),.10,'Forearm.'+side,width=.025)
    ribbon([(s*.87,-.57,3.23),(s*1.05,-.58,3.06),(s*1.02,-.62,2.87),(s*1.30,-.57,2.75)],.025,'UpperArm.'+side)
    ribbon([(s*1.24,-.52,3.22),(s*1.32,-.59,3.05),(s*1.56,-.48,2.99)],.022,'UpperArm.'+side,28)
    ribbon([(s*1.79,-.81,1.70),(s*1.83,-.85,1.54),(s*1.65,-.83,1.43)],.022,'Forearm.'+side)

# Front ceremonial tablets: chunky dark slabs rather than alpha planes.
for x,width,length,tilt in [(0,.35,1.31,0),(-.48,.23,1.06,-.08),(.48,.23,1.06,.08)]:
    top=Vector((x,-.64,2.72)); bottom=top+Vector((tilt,-.08,-length))
    corners=[(-width,0,0),(width,0,0),(width,-.08,-length+.1),(0,-.08,-length),(-width,-.08,-length+.1)]
    ids=[vtx(top+Vector(p),'Tablets') for p in corners]
    back=[vtx(top+Vector(p)+Vector((0,.075,0)),'Tablets') for p in corners]
    face(ids,18);face(tuple(reversed(back)),18)
    for i in range(5):
        face((ids[i],back[i],back[(i+1)%5],ids[(i+1)%5]),(16,17))
    ribbon([(x-width*.90,-.657,2.68),(x-width*.90,-.72,2.72-length+.16),(x,-.736,2.72-length+.045),(x+width*.90,-.72,2.72-length+.16),(x+width*.90,-.657,2.68)],.018,'Tablets',28)
    if x==0:
        line_symbol(SIGMA,(0,-.69,2.40),.19,'Tablets',width=.032)
        line_symbol(CUBE,(0,-.725,1.88),.18,'Tablets',width=.025)
    else:
        line_symbol(FORALL,(x,-.69,2.40),.115,'Tablets',width=.024)
        line_symbol([[(-.6,.3),(.6,.3)],[(-.6,-.3),(.6,-.3)]],(x,-.72,2.0),.12,'Tablets',width=.02)

# Skull, deep open mouth, nose, heavy brow and a separately skinned jaw.
rock((0,-.80,3.58),(.61,.48,.62),'Head',(8,9,10,11),n=11,rings=5)
rock((0,-1.19,3.20),(.43,.30,.44),'Head',20,n=12,rings=4,rough=0)
for s,side in [(1,'L'),(-1,'R')]:
    rock((s*.43,-1.16,3.44),(.20,.24,.37),'Head',(8,9,10),n=6,rings=4)
    rock((s*.25,-1.33,3.70),(.31,.16,.135),'Head',(8,9,10),n=6,rings=3,rotation=(0,s*.22,s*-.20))
    # Glowing eyes are fully enclosed by ochre eyelid geometry, not sprites.
    eye=[(.105,3.626),(.385,3.687),(.372,3.59),(.245,3.573)]
    face([vtx((s*x,-1.481,z),'Brow.'+side) for x,z in eye],30,True)
    iris=[(.205,3.637),(.278,3.653),(.274,3.592),(.222,3.588)]
    face([vtx((s*x,-1.491,z),'Brow.'+side) for x,z in iris],29,True)
    rock((s*.47,-1.32,3.22),(.10,.12,.27),'Head',(9,10),n=5,rings=3)
rock((0,-1.43,3.49),(.25,.18,.16),'Head',(8,11),n=7,rings=3,rough=.02)
for s in [-1,1]:
    rock((s*.105,-1.594,3.49),(.068,.018,.04),'Head',20,n=5,rings=3,rough=0)
rock((0,-1.17,2.97),(.40,.30,.18),'Jaw',(8,9,11),n=9,rings=4)
rock((0,-1.40,3.045),(.32,.065,.075),'Jaw',(21,22),n=8,rings=3,rough=0)
rock((0,-1.31,3.06),(.22,.10,.07),'Jaw',(22,23),n=7,rings=3,rough=0)
for s in [-1,1]:
    shard((s*.275,-1.43,3.44),(s*.225,-1.54,3.04),.095,.085,'Head',(15,24,25),sides=5)
    shard((s*.29,-1.44,3.02),(s*.26,-1.50,3.23),.068,.060,'Jaw',(15,24,25),sides=5)
for x in [-.13,0,.13]:
    shard((x,-1.452,3.43),(x,-1.48,3.32),.057,.053,'Head',(24,25),sides=4)
    shard((x,-1.46,3.04),(x,-1.48,3.12),.050,.045,'Jaw',(24,25),sides=4)

# Mane: broad swept wedges in three layers, asymmetrical tips, warm ivory front.
for s in [-1,1]:
    for layer in range(3):
        for j in range(7):
            a=-.42+j*.26
            base=(s*(.32+layer*.12+math.sin(a)*.18),-.39+layer*.22,3.72+math.cos(a)*.23)
            tip=(s*(.34+layer*.18+math.sin(a)*.63),-.48+layer*.36,4.10+math.cos(a)*.64-layer*.10+random.uniform(-.10,.10))
            shard(base,tip,.155+layer*.014,.12,'Mane',(12,13,14,15),sides=4)
    # Forehead locks sweep into the brow and close the crown's central gap.
    for j in range(4):
        shard((s*(.10+j*.13),-1.02,4.00+j*.035),
              (s*(.06+j*.15),-1.36,3.77+j*.025),.14,.10,'Head',(13,14,15),sides=4)
    for j in range(6):
        z=3.66-j*.17
        shard((s*.45,-.77,z),(s*(.85+.10*math.sin(j)), -1.01,z-.17),.17,.11,'Head',(13,14,15),sides=4)
    for j in range(4):
        shard((s*(.14+j*.15),-.99,2.99),(s*(.09+j*.19),-1.13,2.44+j*.12),.12,.095,'Beard',(13,14,15,25),sides=4)
    # Back mantle follows the upper torso and gives the side/back a designed silhouette.
    for j in range(5):
        shard((s*(.30+j*.20),.49,3.22),(s*(.43+j*.24),.88,3.61-j*.075),.19,.12,'Chest',(12,13,16),sides=4)
for j in range(4):
    shard((0,.56,2.98-j*.25),(0,.91,3.12-j*.25),.16,.12,'Chest' if j<3 else 'Spine_01',(12,16,17),sides=4)

# Horns are curved, seven-sided tubes with segment bands and branching runes.
for s in [-1,1]:
    points=[(s*.58,-.50,3.80),(s*.90,-.46,3.78),(s*1.20,-.43,3.96),(s*1.37,-.44,4.22),(s*1.39,-.48,4.50),(s*1.29,-.54,4.74)]
    loft(points,[.225,.23,.20,.16,.10,.012],7,'Head',(12,13,16,17),jitter=.025)
    for i in range(1,5):
        c=Vector(points[i]);d=Vector(points[min(i+1,5)])-Vector(points[i-1]);u,v=frame_for(d)
        r=[.225,.23,.20,.16,.10][i]+.008
        path=[c+(u*math.cos(math.tau*j/7)+v*math.sin(math.tau*j/7))*r for j in range(8)]
        ribbon(path,.035,'Head',29,normal=d.normalized())
    ribbon([(s*.75,-.68,3.79),(s*.91,-.68,3.86),(s*1.02,-.62,3.96),(s*1.13,-.62,3.96),(s*1.21,-.58,4.13),(s*1.30,-.57,4.22)],.035,'Head',29)

# Add limited, purposeful rear armor if there is room in the triangle budget.
def triangle_total():return sum(len(f)-2 for f in faces)
for s,side in [(1,'L'),(-1,'R')]:
    for j in range(4):
        rock((s*(.40+(j%2)*.36),.64,2.9-(j//2)*.34),(.28,.15,.25),'Chest',(1,2,3,4),n=6,rings=3)
triangles=triangle_total()
print('GEOMETRY_TRIANGLES',triangles,flush=True)

mesh=bpy.data.meshes.new('PrimalAxiom_SkinnedMesh')
mesh.from_pydata(verts,[],faces);mesh.update()
body=bpy.data.objects.new('PrimalAxiom_Body',mesh)
asset_collection.objects.link(body)
for mat in materials:mesh.materials.append(mat)
uv=mesh.uv_layers.new(name='AtlasUV')
for polygon,color,material in zip(mesh.polygons,face_colors,face_mats):
    polygon.material_index=material
    # Each facet has a small, independent island inside its padded atlas tile.
    for j,index in enumerate(polygon.loop_indices):
        a=math.tau*j/len(polygon.loop_indices)
        uv.data[index].uv=((color%8+.5+.24*math.cos(a))/8,(color//8+.5+.24*math.sin(a))/8)
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
bmesh.ops.triangulate(bm,faces=bm.faces)
bm.to_mesh(mesh);bm.free()
for name,_,_,_ in bone_defs:
    group=body.vertex_groups.new(name=name)
    for i,w in enumerate(weights):
        if name in w:group.add([i],w[name],'REPLACE')
rig_data=bpy.data.armatures.new('PrimalAxiom_Skeleton')
rig=bpy.data.objects.new('AxiomRig',rig_data)
asset_collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in bone_defs:
    b=rig_data.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=rig_data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
body.parent=rig
mod=body.modifiers.new('Axiom skeleton | four weights maximum','ARMATURE');mod.object=rig
rig.show_in_front=True;rig_data.display_type='STICK'
for pb in rig.pose.bones:pb.rotation_mode='QUATERNION'
rig['asset_notes']='34 bones; 13 baked actions; in-place; +Z forward in Unity; no gameplay.'
body['reference']='SirRajjang.png supplied by user; original mesh reconstruction, no borrowed model.'

# Analytic pose authoring. Every action uses the same rest skeleton and rest
# weights. Feet and knuckles are solved against support positions each frame.
def rx(a):return Matrix.Rotation(math.radians(a),4,'X')
def rz(a):return Matrix.Rotation(math.radians(a),4,'Z')
def ry(a):return Matrix.Rotation(math.radians(a),4,'Y')
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
def mix(a,b,t):return a+(b-a)*t
def solve_ik(a,c,length1,length2,pole):
    delta=c-a;d=max(.0001,min(delta.length,length1+length2-.0001))
    along=delta.normalized();mid=(d*d+length1*length1-length2*length2)/(2*d)
    height=math.sqrt(max(.0001,length1*length1-mid*mid))
    toward=Vector(pole)-a;toward=(toward-along*toward.dot(along)).normalized()
    return a+along*mid+toward*height
SLEEP=dict(drop=-.58,pitch=62,head=-44,jaw=1,arms=0,blink=.08)
W1=dict(drop=-.55,pitch=60,head=-49,jaw=1,arms=.03,blink=.09)
W2=dict(drop=-.40,pitch=39,head=-30,jaw=3,arms=.20,blink=.3)
W3=dict(drop=-.16,pitch=15,head=-11,jaw=6,arms=.65,blink=.75)
IDLE=dict(drop=0,pitch=0,head=0,jaw=3,arms=1,blink=1)
def blend_pose(a,b,t):return {key:mix(a[key],b[key],smooth(t)) for key in a}
CLIPS=[('Sleep',4.,True),('Wake_01',1.8,False),('Wake_02',2.2,False),('Wake_03',2.0,False),('Full_Awaken',3.,False),('Idle',3.2,True),('Chase',.8,True),('Capture',1.8,False),('Jumpscare',1.8,False),('Barrier_Cast',3.,False),('Attack',2.,False),('Return_To_Sleep',3.,False),('Stunned',1.8,False)]
def pose_params(name,t):
    p=dict(IDLE);wave=math.sin(math.tau*t)
    if name=='Sleep':
        p=dict(SLEEP);p['drop']+=.019*(1-math.cos(math.tau*t));p['head']+=.65*wave
    elif name.startswith('Wake_'):
        a,b={'Wake_01':(SLEEP,W1),'Wake_02':(W1,W2),'Wake_03':(W2,W3)}[name]
        p=blend_pose(a,b,t);p['head']-=3*math.sin(math.pi*t)
    elif name=='Full_Awaken':
        p=blend_pose(W3,IDLE,min(t/.38,1))
        roar=math.sin(math.pi*max(0,min(1,(t-.20)/.68)))**2
        p.update(jaw=p['jaw']+34*roar,head=p['head']-17*roar,pitch=p['pitch']-9*roar,roar=roar)
    elif name=='Idle':
        p['drop']=.025*(1-math.cos(math.tau*t));p['head']=1.2*wave;p['sway']=1.0*wave
        p['blink']=1-.88*max(0,1-abs(t-.73)/.025)
    elif name=='Chase':
        p.update(drop=.08+.10*math.cos(2*math.tau*t),pitch=8+4*wave,head=-7,jaw=11,gait=math.tau*t)
    elif name=='Capture':
        amount=smooth(t/.35)*(1-smooth((t-.72)/.28))
        p.update(reach=amount,grip=smooth((t-.30)/.18)*(1-smooth((t-.82)/.18)),pitch=10*amount,head=-6*amount,jaw=3+8*amount)
    elif name=='Jumpscare':
        amount=smooth(t/.30)*(1-smooth((t-.74)/.26))
        p.update(pitch=24*amount,head=-12*amount,jaw=3+36*amount,lunge=.24*amount,roar=amount)
    elif name=='Barrier_Cast':
        amount=smooth(t/.28)*(1-smooth((t-.78)/.22))
        p.update(cast=amount,pitch=-8*amount,head=-10*amount,jaw=3+13*amount,drop=.08*amount)
    elif name=='Attack':
        load=smooth(t/.32);slam=smooth((t-.38)/.14);recover=smooth((t-.68)/.32)
        amount=load*(1-slam)
        impact=slam*(1-recover)
        p.update(cast=amount,slam=impact,drop=-.22*impact,pitch=-10*amount+29*impact,jaw=3+22*impact)
    elif name=='Return_To_Sleep':p=blend_pose(IDLE,SLEEP,t)
    elif name=='Stunned':
        hit=smooth(t/.14)*(1-smooth((t-.50)/.50))
        p.update(pitch=-18*hit,head=-12*hit,drop=-.12*hit,sway=8*hit*math.sin(3*math.pi*t),jaw=3+22*hit,stun=hit)
    return p

def target_matrices(p,t):
    out={};pivot=rest['Pelvis'][0]
    body_rot=rz(p.get('sway',0))@rx(p['pitch'])
    shift=Vector((0,-p.get('lunge',0),p['drop']))
    def torso_point(x):return pivot+shift+body_rot.to_3x3()@(Vector(x)-pivot)
    def segment(name,a,b,extra=None,scale=None):
        rh,rt,_=rest[name]
        delta=(rt-rh).normalized().rotation_difference((b-a).normalized())
        orient=delta.to_matrix().to_4x4()@rig_data.bones[name].matrix_local.to_3x3().to_4x4()
        if extra is not None:orient=extra@orient
        m=Matrix.Translation(a)@orient
        if scale is not None:m=m@Matrix.Diagonal(Vector((*scale,1)))
        out[name]=m
    segment('Root',Vector((0,0,0)),Vector((0,0,.35)))
    for name in ['Pelvis','Spine_01','Chest','Neck']:
        h,tail,_=rest[name];segment(name,torso_point(h),torso_point(tail))
    head_pos=torso_point(rest['Head'][0]);head_rot=body_rot@rx(p['head'])
    def hp(x):return head_pos+head_rot.to_3x3()@(Vector(x)-rest['Head'][0])
    segment('Head',head_pos,hp(rest['Head'][1]))
    jaw_pos=hp(rest['Jaw'][0]);jaw_rot=head_rot@rx(p['jaw'])
    def jp(x):return jaw_pos+jaw_rot.to_3x3()@(Vector(x)-rest['Jaw'][0])
    segment('Jaw',jaw_pos,jp(rest['Jaw'][1]))
    h,tail,_=rest['Mane'];segment('Mane',hp(h),hp(tail),extra=rx(.8*math.sin(math.tau*t)))
    h,tail,_=rest['Beard'];beard=jp(h)
    # Fold the long beard onto the forearms as the head settles to the floor.
    beard_rotation=jaw_rot@rx(-1.0*p['pitch'])
    segment('Beard',beard,beard+beard_rotation.to_3x3()@(tail-h))
    h,tail,_=rest['Tablets'];tablets=torso_point(h)
    segment('Tablets',tablets,tablets+rx(-1.29*p['pitch']).to_3x3()@(tail-h))
    for s,side in [(1,'L'),(-1,'R')]:
        shoulder=torso_point(rest['UpperArm.'+side][0])
        h,_,_=rest['Shoulder.'+side];segment('Shoulder.'+side,torso_point(h),shoulder)
        standing=Vector((s*1.92,-.51,.91))
        sleeping=Vector((s*1.04,-1.63,.90))
        wrist=sleeping.lerp(standing,p['arms'])
        wrist.z+=.016*(1-math.cos(math.tau*t)) if p['pitch']<20 else 0
        hand_rotation=Matrix.Identity(4)
        gait=p.get('gait')
        if gait is not None:
            phase=gait+(0 if s==1 else math.pi)
            wrist.y+=.63*math.cos(phase)
            wrist.z+=.50*max(0,math.sin(phase))
            hand_rotation=rx(-22*max(0,math.sin(phase)))
        reach=p.get('reach',0)
        wrist=wrist.lerp(Vector((s*1.08,-1.77,1.94)),reach)
        cast=p.get('cast',0)
        wrist=wrist.lerp(Vector((s*1.63,-.42,3.99)),cast)
        hand_rotation=rx(-105*cast-46*reach)@hand_rotation
        slam=p.get('slam',0)
        wrist=wrist.lerp(Vector((s*1.17,-1.39,.92)),slam)
        wrist+=Vector((0,-.12*p.get('roar',0),.12*p.get('roar',0)))
        stun=p.get('stun',0);wrist+=Vector((s*.15*stun,.15*stun,.25*stun))
        elbow=solve_ik(shoulder,wrist,(rest['UpperArm.'+side][1]-rest['UpperArm.'+side][0]).length,(rest['Forearm.'+side][1]-rest['Forearm.'+side][0]).length,(s*3,.6,2.1))
        segment('UpperArm.'+side,shoulder,elbow)
        segment('Forearm.'+side,elbow,wrist)
        handrest=rest['Hand.'+side][0]
        def handpoint(x):return wrist+hand_rotation.to_3x3()@(Vector(x)-handrest)
        segment('Hand.'+side,wrist,handpoint(rest['Hand.'+side][1]))
        for prefix in ['Finger_01.','Finger_02.','Thumb.']:
            name=prefix+side;h,tail,_=rest[name]
            curl=rx(34*p.get('grip',0)+6*math.sin(math.pi*t)*(1-p['arms']))
            point=handpoint(h)
            segment(name,point,point+(hand_rotation@curl).to_3x3()@(tail-h))
        hip=torso_point(rest['Thigh.'+side][0]);ankle=rest['Foot.'+side][0].copy()
        if gait is not None:
            phase=gait+(math.pi if s==1 else 0)
            ankle.y+=.48*math.cos(phase)
            ankle.z+=.38*max(0,math.sin(phase))
        knee=solve_ik(hip,ankle,(rest['Thigh.'+side][1]-rest['Thigh.'+side][0]).length,(rest['Shin.'+side][1]-rest['Shin.'+side][0]).length,(s*1.2,1.4,.8))
        segment('Thigh.'+side,hip,knee);segment('Shin.'+side,knee,ankle)
        footshift=ankle-rest['Foot.'+side][0]
        for prefix in ['Foot.','Toe.']:
            name=prefix+side;h,tail,_=rest[name];segment(name,h+footshift,tail+footshift)
        name='Brow.'+side;h,tail,_=rest[name]
        segment(name,hp(h),hp(tail),scale=(1,1,max(.06,p['blink'])))
    return out

def action_curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves

animation_info=[]
for name,duration,loop in CLIPS:
    action=bpy.data.actions.new(name)
    action.use_fake_user=True
    rig.animation_data_create();rig.animation_data.action=action
    count=round(duration*30)
    last_quats={}
    for frame in range(count+1):
        t=frame/count
        matrices=target_matrices(pose_params(name,t),t)
        for bname,_,_,parent in bone_defs:
            b=rig_data.bones[bname];pb=rig.pose.bones[bname]
            basis=b.matrix_local.inverted()@(rig_data.bones[parent].matrix_local@matrices[parent].inverted() if parent else Matrix.Identity(4))@matrices[bname]
            loc,quat,scale=basis.decompose()
            if bname in last_quats and quat.dot(last_quats[bname])<0:quat.negate()
            last_quats[bname]=quat.copy()
            pb.location=loc;pb.rotation_quaternion=quat;pb.scale=scale
            for path in ['location','rotation_quaternion','scale']:
                pb.keyframe_insert(data_path=path,frame=frame+1,group=bname)
    for curve in action_curves(action):
        for key in curve.keyframe_points:key.interpolation='LINEAR'
    action['loop']=loop;action['duration_seconds']=duration;action['in_place']=True
    animation_info.append(dict(name=name,duration=duration,loop=loop,frames=count+1))
    print('ACTION_CREATED',name,len(list(action_curves(action))),flush=True)
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_start=1;scene.frame_end=97;scene.frame_set(1)

# Deterministic structural and animation validation in Blender.
validation={'triangles':len(mesh.polygons),'vertices':len(mesh.vertices),'bones':len(rig_data.bones),'materials':len(mesh.materials),'animation_count':len(CLIPS),'clips':animation_info}
validation['unweighted_vertices']=sum(not bool(w) for w in weights)
validation['max_influences']=max(len(w) for w in weights)
validation['loop_pose_max_error']={}
validation['motion_max_distance']={}
validation['bounds_by_clip']={}
validation['unsupported_claims']=['No Android device profiling has been performed.']
assert 5000<=validation['triangles']<=8000,validation
assert validation['unweighted_vertices']==0
for name,duration,loop in CLIPS:
    rig.animation_data.action=bpy.data.actions[name]
    samples=[]
    for frame in [1,round(duration*30*.25)+1,round(duration*30*.5)+1,round(duration*30*.75)+1,round(duration*30)+1]:
        scene.frame_set(frame)
        evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        coords=np.array([tuple(v.co) for v in evaluated.data.vertices])
        assert np.isfinite(coords).all(),name
        samples.append(coords)
    validation['motion_max_distance'][name]=float(max(np.linalg.norm(x-samples[0],axis=1).max() for x in samples[1:]))
    validation['bounds_by_clip'][name]={'min':np.min(samples,axis=(0,1)).tolist(),'max':np.max(samples,axis=(0,1)).tolist()}
    if loop:
        error=float(np.linalg.norm(samples[0]-samples[-1],axis=1).max())
        validation['loop_pose_max_error'][name]=error
        assert error<.001,(name,error)
    assert validation['motion_max_distance'][name]>.005,name
rig.animation_data.action=bpy.data.actions['Return_To_Sleep'];scene.frame_set(91)
return_pose=[pb.matrix.copy() for pb in rig.pose.bones]
rig.animation_data.action=bpy.data.actions['Sleep'];scene.frame_set(1)
validation['return_sleep_pose_error']=max(max(abs(a-b) for ra,rb in zip(m,pb.matrix) for a,b in zip(ra,rb)) for m,pb in zip(return_pose,rig.pose.bones))
assert validation['return_sleep_pose_error']<.001
validation['awakening_stage_boundary_errors']={}
for previous,next_clip in [('Sleep','Wake_01'),('Wake_01','Wake_02'),('Wake_02','Wake_03'),('Wake_03','Full_Awaken'),('Full_Awaken','Idle')]:
    endpoints=[]
    for clip,last in [(previous,True),(next_clip,False)]:
        rig.animation_data.action=bpy.data.actions[clip]
        duration=next(d for n,d,_ in CLIPS if n==clip)
        scene.frame_set(round(duration*30)+1 if last else 1)
        evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        endpoints.append(np.array([tuple(v.co) for v in evaluated.data.vertices]))
    error=float(np.linalg.norm(endpoints[0]-endpoints[1],axis=1).max())
    validation['awakening_stage_boundary_errors'][previous+' -> '+next_clip]=error
    assert error<.001,(previous,next_clip,error)
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)

# FBX has the complete skinned mesh and all named takes. The editor setup
# extracts them as standalone .anim files with matching hierarchy bindings.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
# Blender is modeled toward -Y. Rotate only the export assembly so the game
# prefab faces Unity +Z, with scale one and its origin still on the floor.
rig.rotation_euler.z=math.pi
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(BASE/'Models/PrimalAxiom.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
    use_space_transform=True,bake_space_transform=False,mesh_smooth_type='FACE',
    add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,
    bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_step=1,
    bake_anim_simplify_factor=.15,bake_anim_force_startend_keying=True,
    path_mode='RELATIVE',use_mesh_modifiers=True)
rig.rotation_euler.z=0
bpy.context.view_layer.update()

# Independent studio review collection: no bloom, particles or transparent FX.
studio=bpy.data.collections.new('STUDIO | excluded from export')
scene.collection.children.link(studio)
def move_studio(obj):
    for c in list(obj.users_collection):c.objects.unlink(obj)
    studio.objects.link(obj)
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.025))
floor=bpy.context.object;floor.name='Review ground';move_studio(floor)
mat=bpy.data.materials.new('Studio ground (not exported)');mat.diffuse_color=(.028,.037,.045,1);mat.use_nodes=True
mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.028,.037,.045,1)
mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.9
floor.data.materials.append(mat)
bpy.ops.object.camera_add(location=(7,-11,6.2));camera=bpy.context.object;move_studio(camera)
camera.name='Review camera';camera.data.type='ORTHO';camera.data.ortho_scale=6.9;aim(camera,(0,-.2,2.35));scene.camera=camera
for name,loc,power,size,color in [('Key',(-4,-6,8),1650,5,(1,.82,.61)),('Fill',(5,-4,5),1100,4,(.64,.79,1)),('Rim',(1,4,7),2200,4,(1,.56,.20))]:
    bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;move_studio(light)
    light.name='Studio '+name;light.data.energy=power;light.data.shape='DISK';light.data.size=size;light.data.color=color;aim(light,(0,0,2))
scene.world.color=(.16,.16,.16)
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
# Make the source convenient to inspect when opened interactively.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=8
            area.spaces.active.region_3d.view_location=Vector((0,0,2.3))
            area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Models/PrimalAxiom.blend'))
(BASE/'Previews/BlenderValidation.json').write_text(json.dumps(validation,indent=2),encoding='utf-8')
scene.render.filepath=str(BASE/'Previews/PrimalAxiom_Hero.png')
bpy.ops.render.render(write_still=True)
print('PRIMAL_AXIOM_COMPLETE',json.dumps(validation),flush=True)
