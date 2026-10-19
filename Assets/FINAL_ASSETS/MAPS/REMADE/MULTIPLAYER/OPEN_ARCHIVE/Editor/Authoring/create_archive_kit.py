"""Create the original Open Archive modular kit, palette and Unity FBX.

Run: blender --background --python create_archive_kit.py
All prototypes have identity transforms, origin on ground, Blender Z up.
The FBX is meters, -Y forward, +Z up in Blender / +Y up in Unity.
Only prototype meshes are exported; presentation staging is render-only.
"""
import bpy
import math
import random
import json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
MESHES = ROOT / 'Meshes'
TEXTURES = ROOT / 'Textures'
PREVIEWS = ROOT / 'Previews'
for directory in (MESHES, TEXTURES, PREVIEWS):
    directory.mkdir(parents=True, exist_ok=True)
random.seed(48271)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for datablock in list(datablocks):
        if datablock.users == 0:
            datablocks.remove(datablock)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0

# Row-major swatches: row zero is the bottom UV row. UVs address tile centers.
SWATCHES = [
 ('stone_slate','#6C7A88'), ('stone_light','#8D9BA3'),
 ('stone_shadow','#506274'), ('stone_blue','#788A9C'),
 ('stone_warm','#AAA99B'), ('stone_lichen','#8C9B7A'),
 ('capstone_cream','#D4C6A3'), ('capstone_sun','#E5D7B2'),
 ('gold','#C99742'), ('gold_light','#E6BC67'),
 ('gold_shadow','#91672F'), ('wood','#866039'),
 ('wood_light','#AC8050'), ('wood_shadow','#5C432C'),
 ('blue_banner','#1B567C'), ('blue_banner_light','#286F98'),
 ('leaf_dark','#2B6945'), ('leaf_mid','#42804A'),
 ('leaf_sun','#6D9E47'), ('leaf_lime','#9FB451'),
 ('leaf_teal','#437D64'), ('leaf_deep','#245C52'),
 ('grass','#6F9E4D'), ('grass_sun','#A4B65A'),
 ('flower_yellow','#F2CC63'), ('flower_cream','#FFF1BD'),
 ('flower_lavender','#A58AC3'), ('flower_pink','#E6A7AD'),
 ('earth','#7F7057'), ('earth_light','#A0926A'),
 ('turquoise','#27B8BD'), ('turquoise_light','#67D9D5'),
 ('gold_glow','#FFBC3A'), ('green_glow','#50F58D'),
 ('blue_glow','#5FD9EB'), ('flame_core','#FFF0A1'),
 ('flame_amber','#FFA72B'), ('flame_orange','#E66C26'),
 ('cliff_deep','#435566'), ('cliff_blue','#5C6D83'),
 ('cliff_mid','#7A8998'), ('cliff_light','#9AA7AF'),
 ('moss_dark','#4F6C53'), ('moss_light','#718654'),
 ('water_deep','#147D9E'), ('water_mid','#1B9FBB'),
 ('sand','#CDBA88'), ('path_light','#D8C49B'),
 ('path_mid','#C0AD83'), ('path_shadow','#A89470'),
 ('ruin_inset','#39566A'), ('plinth_dark','#596B79'),
 ('bark','#694C36'), ('bark_light','#92704A'),
 ('leaf_gold','#BFC65B'), ('leaf_soft','#82A95B'),
 ('white','#F2EEE3'), ('black_blue','#263C4B'),
 ('copper','#A8794A'), ('terracotta','#B68B6D'),
 ('sky','#85C3D9'), ('foam','#CCF0EB'),
 ('neutral_warm','#B5AD94'), ('neutral_cool','#9BABB0'),
]
SW = {label:i for i,(label,color) in enumerate(SWATCHES)}
EMISSIVE = {32:1.0,33:1.0,34:0.8,35:1.0,36:0.75,37:0.35}

def rgba(hex_color):
    h = hex_color.lstrip('#')
    return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))+(1.0,)

def atlas(name, emission=False):
    img = bpy.data.images.new(name, width=512, height=512, alpha=False)
    pixels = [0.0] * (512*512*4)
    for y in range(512):
        for x in range(512):
            index = (y//64)*8 + x//64
            col = rgba(SWATCHES[index][1])
            if emission:
                s = EMISSIVE.get(index,0.0)
                col = (col[0]*s,col[1]*s,col[2]*s,1.0)
            p = (y*512+x)*4
            pixels[p:p+4] = col
    img.pixels.foreach_set(pixels)
    img.filepath_raw = str(TEXTURES / (name+'.png'))
    img.file_format = 'PNG'
    img.save()
    return img

palette = atlas('OpenArchive_Palette')
emission = atlas('OpenArchive_Emission', True)
material = bpy.data.materials.new('OpenArchive_SharedPalette')
material.use_nodes = True
nodes = material.node_tree.nodes
links = material.node_tree.links
principled = nodes.get('Principled BSDF')
principled.inputs['Roughness'].default_value = 0.88
principled.inputs['Metallic'].default_value = 0.0
tex = nodes.new('ShaderNodeTexImage')
tex.name='Palette_8x8'; tex.image=palette; tex.interpolation='Closest'
links.new(tex.outputs['Color'],principled.inputs['Base Color'])
emit = nodes.new('ShaderNodeTexImage')
emit.name='Emission_8x8'; emit.image=emission; emit.interpolation='Closest'
links.new(emit.outputs['Color'],principled.inputs['Emission Color'])
principled.inputs['Emission Strength'].default_value=1.35
material.diffuse_color = rgba('#8D9BA3')
material['atlas_layout']='8x8; UV row zero at bottom; one swatch per face'
material['glow_swatches']='32 gold; 33 green; 34 turquoise; 35-37 flame'
PARTS=[]
PROTOS=[]

def shade(obj, colors, variation=True):
    colors = [SW[c] if isinstance(c,str) else c for c in colors]
    obj.data.materials.clear()
    obj.data.materials.append(material)
    # Primitives arrive with a default UVMap. Remove it so the exported first
    # channel is always the explicit swatch layout (also used by Blender).
    for old_uv in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(old_uv)
    uv = obj.data.uv_layers.new(name='PaletteUV')
    for face in obj.data.polygons:
        idx = random.choice(colors) if variation else colors[0]
        u=(idx%8+0.5)/8
        v=(idx//8+0.5)/8
        face.material_index=0
        face.use_smooth=False
        for loop in face.loop_indices:
            uv.data[loop].uv=(u,v)
    PARTS.append(obj)
    return obj

def cube(loc, scale, colors, bevel=0.0, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj=bpy.context.object
    obj.scale=scale
    if rotation: obj.rotation_euler=rotation
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier=obj.modifiers.new('Chipped edges','BEVEL')
        modifier.width=bevel; modifier.segments=1
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return shade(obj,colors)

def cone(loc, radius1, radius2, depth, colors, vertices=8, rotation=None):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1,
        radius2=radius2, depth=depth, end_fill_type='NGON', location=loc)
    obj=bpy.context.object
    if rotation: obj.rotation_euler=rotation
    return shade(obj,colors)

def ico(loc, scale, colors, subdivisions=1, seed=0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions,radius=1,location=loc)
    obj=bpy.context.object
    rng=random.Random(seed+int(loc[0]*100)+int(loc[2]*730))
    for vertex in obj.data.vertices:
        k=rng.uniform(.88,1.10)
        vertex.co.x *= scale[0]*k
        vertex.co.y *= scale[1]*k
        vertex.co.z *= scale[2]*k
    return shade(obj,colors)

def mesh(verts, faces, colors):
    data=bpy.data.meshes.new('HandcraftedGeometry')
    data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new('Part',data)
    bpy.context.collection.objects.link(obj)
    return shade(obj,colors)

def solid_sheet(verts, faces, outline, colors, thickness=.018):
    """A thin closed prism; FBX does not retain coincident reverse faces."""
    count=len(verts)
    normal=(Vector(verts[faces[0][1]])-Vector(verts[faces[0][0]])).cross(
        Vector(verts[faces[0][2]])-Vector(verts[faces[0][0]])).normalized()
    all_verts=list(verts)+[tuple(Vector(v)-normal*thickness) for v in verts]
    all_faces=list(faces)+[tuple(i+count for i in reversed(face)) for face in faces]
    for j,i in enumerate(outline):
        k=outline[(j+1)%len(outline)]
        all_faces.append((i,i+count,k+count,k))
    return mesh(all_verts,all_faces,colors)

def beam(a,b,width,depth,colors,bevel=.0):
    av=Vector(a); bv=Vector(b)
    direction=bv-av
    obj=cube((av+bv)*.5,(width,depth,direction.length),colors,bevel)
    obj.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    return obj

def diamond(loc, sx, sz, color, depth=.035):
    x,y,z=loc
    return mesh([(x,y-depth,z+sz),(x+sx,y-depth,z),
                 (x,y-depth,z-sz),(x-sx,y-depth,z),
                 (x,y,z+sz),(x+sx,y,z),(x,y,z-sz),(x-sx,y,z)],
                [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),
                 (2,6,7,3),(3,7,4,0)],[color])

def ring_vertical(center, outer, inner, depth, colors, sides=8):
    x,y,z=center; verts=[]
    for yy in (y-depth*.5,y+depth*.5):
        for r in (outer,inner):
            for i in range(sides):
                a=2*math.pi*i/sides
                verts.append((x+math.sin(a)*r,yy,z+math.cos(a)*r))
    faces=[]
    for i in range(sides):
        j=(i+1)%sides
        faces.extend([(i,j,sides+j,sides+i),(2*sides+i,3*sides+i,3*sides+j,2*sides+j),
                      (i,2*sides+i,2*sides+j,j),(sides+i,sides+j,3*sides+j,3*sides+i)])
    return mesh(verts,faces,colors)

def finish(name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS: obj.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    min_z=min(vertex.co.z for vertex in obj.data.vertices)
    for vertex in obj.data.vertices:
        vertex.co.z-=min_z
    obj.name=name; obj.data.name=name+'_Mesh'
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Collapsed UVs intentionally sample solid-color swatches, no tiling or seams.
    for polygon in obj.data.polygons: polygon.use_smooth=False
    obj.data.materials.clear(); obj.data.materials.append(material)
    for polygon in obj.data.polygons: polygon.material_index=0
    obj['purpose']='Original Open Archive reusable environment prototype'
    obj['units']='meters; ground at local Z=0'
    PROTOS.append(obj); PARTS.clear()
    return obj

STONE=['stone_slate','stone_light','stone_blue','stone_warm']
CAP=['capstone_cream','capstone_sun']
GOLD=['gold','gold_light']
LEAF=['leaf_mid','leaf_sun','leaf_lime','leaf_soft']

def pillar(height=8.0, broken=False):
    cube((0,0,.18),(1.85,1.85,.36),STONE,.10)
    cube((0,0,.48),(1.55,1.55,.28),CAP,.06)
    cone((0,0,1.0),.82,.66,.8,STONE,8)
    shaft_start=1.30
    shaft_end=height-.95 if not broken else height-.25
    cone((0,0,(shaft_start+shaft_end)*.5),.66,.53,
         shaft_end-shaft_start,STONE,8)
    for z in (1.45,shaft_end-.15):
        cube((0,0,z),(1.30,1.30,.18),CAP,.04)
    # Golden recessed academy lozenge on the south face.
    diamond((0,-.63,2.3),.26,.50,'gold')
    diamond((0,-.67,2.3),.12,.28,'turquoise')
    if not broken:
        cube((0,0,height-.70),(1.40,1.40,.30),STONE,.07)
        cube((0,0,height-.48),(1.75,1.75,.22),CAP,.06)
        cone((0,0,height-.23),.78,.10,.46,STONE,4,rotation=(0,0,math.pi/4))
    else:
        # Broken slab has a deliberately uneven sawtooth silhouette.
        verts=[(-.62,-.62,height-.35),(.62,-.62,height-.35),(.62,.62,height-.35),(-.62,.62,height-.35),
               (-.62,-.62,height-.08),(.62,-.62,height+.12),(.62,.62,height-.14),(-.62,.62,height+.28)]
        mesh(verts,[(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6),(4,6,7)],STONE)

pillar(); finish('Pillar_Intact')
pillar(4.0,True); finish('Pillar_Broken')

# Four-direction nest arch: opening extends x=-5.4..5.4, z=0..7.3.
for side in (-1,1):
    x=side*6.40
    cube((x,0,.22),(2.25,2.25,.44),STONE,.10)
    cube((x,0,.65),(1.95,1.95,.42),CAP,.07)
    cube((x,0,4.05),(1.80,1.60,6.4),STONE,.10)
    for z in (1.2,6.65,7.10):
        cube((x,0,z),(2.10,1.95,.26),CAP,.07)
    cube((x,0,7.48),(2.35,2.05,.50),STONE,.10)
    cube((x,0,7.83),(2.50,2.20,.22),CAP,.05)
    cone((x,0,8.42),1.10,.12,.95,STONE,4,rotation=(0,0,math.pi/4))
    cube((x,-.85,4.25),(.74,.08,3.8),['ruin_inset'],.03)
    diamond((x,-.94,4.55),.36,.72,'gold')
    diamond((x,-.98,4.55),.14,.35,'turquoise')

def arch_z(x): return 7.30+4.05*(1-(abs(x)/5.4)**1.3)
segments=14
for i in range(segments):
    x0=-5.4+10.8*i/segments+.018
    x1=-5.4+10.8*(i+1)/segments-.018
    z0=arch_z(x0); z1=arch_z(x1)
    verts=[(x0,-.79,z0),(x1,-.79,z1),(x1,-.79,z1+.90),(x0,-.79,z0+.90),
           (x0,.79,z0),(x1,.79,z1),(x1,.79,z1+.90),(x0,.79,z0+.90)]
    mesh(verts,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(0,3,7,4),(1,5,6,2)],CAP if i%4==0 else STONE)
    # Fine gold line follows the arch above the opening.
    beam((x0,-.84,z0+.19),(x1,-.84,z1+.19),.075,.08,['gold'])
diamond((0,-.89,11.55),.44,.64,'gold')
diamond((0,-.94,11.55),.22,.35,'turquoise_light')
finish('Arch_Wide')

# Broken wall, visible staggered masonry, short enough to preserve sightlines.
for course in range(3):
    for i in range(3):
        if course==2 and i==2: continue
        offset=.18 if course%2 else 0
        z=.30+course*.57
        cube((-1.02+i*1.01+offset,0,z),(.96,.78,.55),STONE,.045)
for x,z in [(-1.02,1.79),(0,1.78)]:
    cube((x,0,z),(.99,.88,.18),CAP,.03)
ico((1.19,.03,.3),(.48,.46,.36),STONE)
finish('RuinedWall_Short')

def cliff(name,seed,height=10):
    rng=random.Random(seed); n=7; verts=[]
    angles=[2*math.pi*i/n for i in range(n)]
    for ring in range(4):
        for i,a in enumerate(angles):
            r=rng.uniform(1.72,2.32)*(1.05 if ring==0 else .97 if ring==3 else 1)
            z=(0,3.7,7.7,height)[ring]
            if ring==3: z+=rng.uniform(-.40,.35)
            elif ring in (1,2): z+=rng.uniform(-.45,.45)
            verts.append((math.cos(a)*r,math.sin(a)*r,z))
    faces=[]
    for ring in range(3):
        for i in range(n):
            j=(i+1)%n
            if ring==1 and i%2:
                faces.extend([(ring*n+i,ring*n+j,(ring+1)*n+i),
                              (ring*n+j,(ring+1)*n+j,(ring+1)*n+i)])
            else: faces.append((ring*n+i,ring*n+j,(ring+1)*n+j,(ring+1)*n+i))
    faces.extend([tuple(reversed(range(n))),tuple(range(n*3,n*4))])
    mesh(verts,faces,['cliff_deep','cliff_blue','cliff_mid','cliff_light'])
    # Broken limestone shoulders and patches of lichen reinforce the stacked rock silhouette.
    for i in range(3):
        a=i*2.1+.4; r=1.6
        ico((math.cos(a)*r,math.sin(a)*r,height-rng.uniform(.6,1.3)),(.60,.68,.70),
            ['stone_light','stone_lichen','cliff_light'],1,seed+i)
    finish(name)

cliff('Cliff_Column_A',516)
cliff('Cliff_Column_B',927,10.7)
ico((0,0,.66),(1.07,.82,.78),STONE+['stone_lichen'],2,827)
finish('Rock_A')
ico((-.24,0,.43),(.80,.68,.55),STONE+['stone_lichen'],1,628)
ico((.47,.19,.31),(.52,.50,.40),STONE,1,425)
finish('Rock_B')

# 2.5m railing module along local X, all detail remains outside walking deck.
for x in (-1.25,1.25):
    cube((x,0,.68),(.20,.24,1.36),['wood','wood_light'],.035)
    cube((x,0,1.42),(.28,.30,.14),GOLD,.03)
cube((0,0,1.26),(2.62,.20,.20),['wood_light','wood'],.035)
cube((0,0,.44),(2.5,.13,.17),['wood','wood_shadow'],.025)
beam((-1.14,0,.50),(1.14,0,1.17),.09,.10,['wood_light'])
for x in (-1.25,1.25):
    for z in (.39,1.21):
        ico((x,-.13,z),(.045,.025,.045),['gold'],1)
finish('Bridge_Railing_Module')

def flame(loc,scale=1):
    x,y,z=loc
    # Opaque nested faceted flames, no transparent particle planes or local lights.
    cone((x,y,z+.45*scale),.38*scale,.03*scale,.90*scale,
         ['flame_amber','flame_orange'],5,rotation=(0,.15,0))
    cone((x-.13*scale,y,z+.72*scale),.18*scale,.015*scale,.80*scale,
         ['flame_amber','gold_glow'],5,rotation=(0,-.22,0))
    cone((x,y-.05*scale,z+.26*scale),.24*scale,.02*scale,.48*scale,
         ['flame_core','gold_glow'],5)

cube((0,0,.18),(1.25,1.25,.36),STONE,.09)
cube((0,0,.47),(1.00,1.00,.23),CAP,.04)
cone((0,0,1.45),.46,.34,1.80,STONE,8)
cube((0,0,2.42),(.85,.85,.24),CAP,.05)
cone((0,0,2.66),.34,.64,.30,['gold_shadow','gold'],8)
cone((0,0,2.86),.64,.64,.12,GOLD,8)
flame((0,0,2.88),.83)
diamond((0,-.39,1.47),.20,.40,'gold')
finish('Torch_Pedestal')

cube((0,0,.20),(1.45,1.45,.40),STONE,.08)
cube((0,0,.51),(1.18,1.18,.22),CAP,.05)
cone((0,0,3.35),.39,.31,5.45,STONE,8)
cube((0,0,6.17),(.98,.98,.28),CAP,.05)
cone((0,0,6.66),.57,.04,.70,STONE,4,rotation=(0,0,math.pi/4))
beam((.10,0,5.86),(2.05,0,5.86),.12,.15,GOLD)
# A front-facing blue pennant with a faceted fold and pointed foot.
verts=[(.57,-.12,5.78),(1.23,-.20,5.78),(1.91,-.08,5.78),
       (.57,-.12,3.43),(1.23,-.23,2.91),(1.91,-.08,3.43)]
cloth=solid_sheet(verts,[(0,3,4,1),(1,4,5,2)],
                  [0,3,4,5,2,1],['blue_banner','blue_banner_light'],.025)
beam((.57,-.15,5.78),(.57,-.15,3.43),.045,.05,['gold'])
beam((1.91,-.11,5.78),(1.91,-.11,3.43),.045,.05,['gold'])
beam((.57,-.15,3.43),(1.23,-.26,2.91),.045,.05,['gold'])
beam((1.23,-.26,2.91),(1.91,-.11,3.43),.045,.05,['gold'])
diamond((1.23,-.30,4.43),.34,.57,'gold_light')
diamond((1.23,-.34,4.43),.19,.37,'blue_banner')
diamond((1.23,-.38,4.43),.07,.13,'turquoise_light')
finish('Banner_Pillar')

cube((0,0,.16),(1.65,1.65,.32),STONE,.08)
cube((0,0,.43),(1.32,1.32,.24),CAP,.05)
cube((0,0,1.20),(.92,.82,1.28),STONE,.07)
cube((0,0,1.98),(1.24,1.14,.28),CAP,.05)
cone((0,0,2.48),.61,.13,.72,['gold','capstone_cream'],4,rotation=(0,0,math.pi/4))
diamond((0,-.45,1.26),.26,.43,'gold')
diamond((0,-.49,1.26),.12,.24,'turquoise')
ico((0,0,2.92),(.21,.21,.25),['turquoise_light','blue_glow'],1)
finish('Small_Monument')

for name,seed in [('Bush_A',48),('Bush_B',84)]:
    rng=random.Random(seed)
    for i in range(5):
        a=i*2.4; r=rng.uniform(.15,.46)
        size=rng.uniform(.44,.64)
        ico((math.cos(a)*r,math.sin(a)*r,.46+size*.20),
            (size,size*.85,size*.80),LEAF if name=='Bush_A' else ['leaf_teal','leaf_mid','leaf_sun'],1,seed+i)
    for i in range(3):
        a=i*2.1
        beam((0,0,.02),(math.cos(a)*.4,math.sin(a)*.4,.56),.055,.055,['bark'])
    finish(name)

for i in range(7):
    a=i*2.399; x=math.cos(a)*.18; y=math.sin(a)*.18
    height=.36+.08*(i%3); w=.065
    solid_sheet([(x-w,y,0),(x+w,y,0),(x+.16*math.cos(a),y+.16*math.sin(a),height)],
         [(0,1,2)],[0,1,2],['grass','grass_sun','leaf_lime'],.009)
finish('Grass_Clump')

for i in range(5):
    a=i*2.399; r=.18+.025*(i%2)
    x=math.cos(a)*r; y=math.sin(a)*r; z=.34+(i%3)*.075
    beam((x,y,0),(x,y,z),.021,.021,['leaf_mid'])
    for j in range(5):
        a2=j*math.pi*2/5
        px=x+.080*math.cos(a2); py=y+.080*math.sin(a2)
        ico((px,py,z),(.072,.066,.035),['flower_cream' if i%2 else 'flower_yellow'],1,i+j)
    ico((x,y,z+.015),(.052,.052,.038),['gold_light'],1,i)
    solid_sheet([(x,y,.08),(x+.14,y+.06,.18),(x+.05,y+.02,.21)],
                [(0,1,2)],[0,1,2],['leaf_sun'],.009)
finish('Flower_Cluster')

manifest={
 'name':'The Open Archive — Original modular environment kit',
 'authoring':'Blender 5.0, deterministic procedural source',
 'axis':'Source: Z up, -Y forward; FBX export: -Z forward, +Y up; 1 unit = 1 meter',
 'material':'OpenArchive_SharedPalette (one slot per prototype)',
 'palette':[list(rgba(color)[:3]) for label,color in SWATCHES],
 'paletteRoles':{'grass':22,'grassLight':23,'sand':46,'path':48,'pathLight':47,
                 'stone':0,'stoneLight':1,'wood':11,'water':45,'waterDeep':44,
                 'turquoise':30,'goldGlow':32,'greenGlow':33,'blueBanner':14},
 'paletteInfo':{'size':512,'grid':[8,8],'tile_pixels':64,'filter':'Point','wrap':'Clamp',
            'layout':'Index=(UV row from bottom)*8+column; face UV=(column+0.5,row+0.5)/8',
            'swatches':[{'index':i,'name':label,'hex':color,'emission':EMISSIVE.get(i,0)}
                        for i,(label,color) in enumerate(SWATCHES)]},
 'prototypes':[],
 'notes':['All prototype origins are at (0,0,0), transforms identity; ground at Z=0.',
          'Flat shading is deliberate; one solid-color palette UV per face.',
          'Cliff bases are Z=0 and caps near Z=10; sink/scale for island edge.',
          'Arch clear span 10.8m; clear height at springline 7.3m.',
          'Torch flame is opaque geometry with emission; no point lights.',
          'Banner, grass and flower leaves are opaque thin closed geometry; no cloth simulation.',
          'No collision or gameplay data is embedded in the FBX.'],
}
for obj in PROTOS:
    obj.data.calc_loop_triangles()
    coords=[obj.matrix_world@v.co for v in obj.data.vertices]
    low=[min(v[i] for v in coords) for i in range(3)]
    high=[max(v[i] for v in coords) for i in range(3)]
    manifest['prototypes'].append({'name':obj.name,'vertices':len(obj.data.vertices),
        'triangles':len(obj.data.loop_triangles),'bounds_min':[round(v,4) for v in low],
        'bounds_max':[round(v,4) for v in high],
        'boundsMin':[round(v,4) for v in low],'boundsMax':[round(v,4) for v in high],
        'dimensions':[round(high[i]-low[i],4) for i in range(3)],'material_slots':len(obj.data.materials)})
manifest['total_triangles']=sum(p['triangles'] for p in manifest['prototypes'])
assert manifest['total_triangles']<=25000
assert len(PROTOS)==16
assert all(len(obj.data.materials)==1 for obj in PROTOS)
with open(MESHES/'OpenArchive_KitManifest.json','w',encoding='utf-8') as handle:
    json.dump(manifest,handle,indent=2)

bpy.ops.object.select_all(action='DESELECT')
for obj in PROTOS: obj.select_set(True)
bpy.context.view_layer.objects.active=PROTOS[0]
palette.filepath='//../Textures/OpenArchive_Palette.png'
emission.filepath='//../Textures/OpenArchive_Emission.png'
bpy.ops.wm.save_as_mainfile(filepath=str(MESHES/'OpenArchive_Kit.blend'),relative_remap=False)
bpy.ops.export_scene.fbx(filepath=str(MESHES/'OpenArchive_Kit.fbx'),
    use_selection=True,object_types={'MESH'},global_scale=1.0,
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
    mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,
    bake_anim=False,path_mode='RELATIVE',embed_textures=False)
print('OPEN_ARCHIVE_KIT_EXPORTED '+str(manifest['total_triangles'])+' triangles; '+str(len(PROTOS))+' prototypes')

# Render-only presentation, deliberately not included in saved source or FBX.
scene=bpy.context.scene
for obj in PROTOS: obj.hide_render=True
for i,obj in enumerate(PROTOS):
    copy=obj.copy(); copy.data=obj.data
    bpy.context.collection.objects.link(copy); copy.hide_render=False
    col=i%4; row=i//4
    # Equal-size contact-sheet cells make small vegetation readable as well.
    factor=8.0/max(obj.dimensions)
    copy.scale=(factor,factor,factor)
    copy.location=((col-1.5)*12,(1.5-row)*12,0)
    if obj.name=='Arch_Wide': copy.location.z=0

bpy.ops.mesh.primitive_plane_add(size=180,location=(0,0,-.08))
ground=bpy.context.object
studio_mat=bpy.data.materials.new('RenderOnly_Studio')
studio_mat.diffuse_color=(.19,.27,.30,1); studio_mat.use_nodes=True
studio_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.19,.27,.30,1)
studio_mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.92
ground.data.materials.append(studio_mat)
bpy.ops.object.camera_add(location=(40,-64,65))
camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,2.0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'; camera.data.ortho_scale=71
scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-20,-28,65))
key=bpy.context.object; key.data.energy=35000; key.data.shape='DISK'; key.data.size=45
key.rotation_euler=(Vector((0,0,0))-key.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='SUN',location=(0,0,40))
sun=bpy.context.object; sun.data.energy=2.0
sun.rotation_euler=(math.radians(23),math.radians(-28),math.radians(-32))
scene.world.color=(.22,.30,.38)
scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.render.resolution_x=1800; scene.render.resolution_y=1400; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(PREVIEWS/'OpenArchive_KitReview.png')
scene.view_settings.view_transform='AgX'
bpy.ops.render.render(write_still=True)
print('OPEN_ARCHIVE_KIT_COMPLETE')
