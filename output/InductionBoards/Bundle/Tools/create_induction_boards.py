"""Build and texture-bake the two induction chamber props in Blender 4.5."""
import bpy, math, json, array
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/InductionBoards'; SRC=ROOT/'SourceAssets/InductionBoards'
TEX=OUT/'Textures'
for p in [OUT,SRC,TEX]:p.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.bake.margin=8;scene.render.bake.use_clear=True
recipes={
 'Stone':dict(low=(.022,.029,.061,1),high=(.036,.046,.085,1),scale=90,rough=.85,metal=0,bump=.005,size=1024),
 'Parchment':dict(low=(.59,.40,.21,1),high=(.85,.66,.39,1),scale=125,rough=.88,metal=0,bump=.004,size=1024),
 'Metal':dict(low=(.21,.125,.045,1),high=(.30,.19,.075,1),scale=100,rough=.39,metal=.65,bump=.002,size=512),
 'Crystal':dict(low=(.006,.16,.72,1),high=(.025,.47,1,1),scale=65,rough=.22,metal=.15,bump=.001,size=512),
}
materials={}; bake_report=[]
def save_image(im,name):
    im.filepath_raw=str(TEX/name);im.file_format='PNG';im.save()
    im.pack()
def bake_material(name,r):
    # Bake UV-driven procedural appearance on a canonical swatch, then remove
    # all procedural shading from the exported material entirely.
    bpy.ops.mesh.primitive_plane_add(size=1);swatch=bpy.context.object;swatch.name='BakeSwatch'
    m=bpy.data.materials.new('Chamber_'+name);m.use_nodes=True;swatch.data.materials.append(m)
    n=m.node_tree.nodes;lk=m.node_tree.links;n.clear()
    output=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled')
    uv=n.new('ShaderNodeTexCoord');noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=r['scale'];noise.inputs['Detail'].default_value=2
    lk.new(uv.outputs['UV'],noise.inputs['Vector']);ramp=n.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position=.18;ramp.color_ramp.elements[0].color=r['low']
    ramp.color_ramp.elements[1].position=.82;ramp.color_ramp.elements[1].color=r['high'];lk.new(noise.outputs['Fac'],ramp.inputs['Fac'])
    emission=n.new('ShaderNodeEmission');lk.new(ramp.outputs['Color'],emission.inputs['Color']);lk.new(emission.outputs[0],output.inputs[0])
    base=bpy.data.images.new(name+'_BaseColor',r['size'],r['size'],alpha=True)
    target=n.new('ShaderNodeTexImage');target.image=base;n.active=target;target.select=True
    bpy.ops.object.bake(type='EMIT');save_image(base,name+'_BaseColor.png')
    # Fine roughness variation is baked independently, with no baked lighting.
    mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY';mul.inputs[1].default_value=.12;lk.new(noise.outputs['Fac'],mul.inputs[0])
    add=n.new('ShaderNodeMath');add.operation='ADD';add.inputs[1].default_value=r['rough']-.06;lk.new(mul.outputs[0],add.inputs[0]);lk.new(add.outputs[0],emission.inputs['Color'])
    rough=bpy.data.images.new(name+'_Roughness',r['size'],r['size'],alpha=True);rough.colorspace_settings.name='Non-Color';target.image=rough
    bpy.ops.object.bake(type='EMIT');save_image(rough,name+'_Roughness.png')
    bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.22;bump.inputs['Distance'].default_value=r['bump']
    lk.new(noise.outputs['Fac'],bump.inputs['Height']);lk.new(bump.outputs['Normal'],bs.inputs['Normal']);lk.new(bs.outputs[0],output.inputs[0])
    normal=bpy.data.images.new(name+'_Normal',r['size'],r['size'],alpha=True);normal.colorspace_settings.name='Non-Color';target.image=normal
    bpy.ops.object.bake(type='NORMAL');save_image(normal,name+'_Normal.png')
    # Unity URP metallic workflow: R=metallic, A=smoothness (1-roughness).
    packed=bpy.data.images.new(name+'_MetallicSmoothness',r['size'],r['size'],alpha=True);packed.colorspace_settings.name='Non-Color'
    rp=array.array('f',[0])*(r['size']*r['size']*4);rough.pixels.foreach_get(rp)
    px=array.array('f',[0])*len(rp)
    for i in range(0,len(px),4):px[i]=r['metal'];px[i+1]=0;px[i+2]=0;px[i+3]=max(0,min(1,1-rp[i]))
    packed.pixels.foreach_set(px);save_image(packed,name+'_MetallicSmoothness.png')
    # Runtime Blender preview is now image-only; FBX never relies on Noise/Bump nodes.
    n.clear();output=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled');lk.new(bs.outputs[0],output.inputs[0])
    c=n.new('ShaderNodeTexImage');c.image=base;lk.new(c.outputs['Color'],bs.inputs['Base Color'])
    t=n.new('ShaderNodeTexImage');t.image=rough;lk.new(t.outputs['Color'],bs.inputs['Roughness'])
    t=n.new('ShaderNodeTexImage');t.image=normal;nm=n.new('ShaderNodeNormalMap');lk.new(t.outputs['Color'],nm.inputs['Color']);lk.new(nm.outputs[0],bs.inputs['Normal'])
    bs.inputs['Metallic'].default_value=r['metal']
    if name=='Crystal':lk.new(c.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.65
    m.diffuse_color=r['high'];bpy.data.objects.remove(swatch,do_unlink=True)
    bake_report.append(dict(surface=name,resolution=r['size'],method='Cycles EMIT color + EMIT roughness + tangent NORMAL',files=[name+'_'+s+'.png' for s in ['BaseColor','Roughness','Normal','MetallicSmoothness']],procedural_nodes_in_export=0))
    return m
for name,r in recipes.items():materials[name]=bake_material(name,r)

groups={};parts=[]
def add(ob,name,mat,group):
    ob.name=name;ob.data.materials.append(materials[mat]);groups.setdefault(group,[]).append(ob);return ob
def box(name,loc,size,mat,group,bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);ob=bpy.context.object;ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=ob.modifiers.new('Chamfer','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
    return add(ob,name,mat,group)
def cylinder(name,loc,r,depth,mat,group,axis='Z'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=r,depth=depth,location=loc);ob=bpy.context.object
    if axis=='X':ob.rotation_euler[1]=math.pi/2
    elif axis=='Y':ob.rotation_euler[0]=math.pi/2
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);return add(ob,name,mat,group)
def mesh(name,verts,faces,mat,group):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);return add(ob,name,mat,group)
def plate(name,loc,w,h,d,mat,group,ch=.06):
    x,y,z=loc;coords=[(-w/2+ch,-h/2),(w/2-ch,-h/2),(w/2,-h/2+ch),(w/2,h/2-ch),(w/2-ch,h/2),(-w/2+ch,h/2),(-w/2,h/2-ch),(-w/2,-h/2+ch)]
    verts=[(x+xx,y+yy,z+zz) for yy in [-d/2,d/2] for xx,zz in coords]
    return mesh(name,verts,[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)],mat,group)
def gem(name,loc,r,h,group):
    x,y,z=loc;v=[]
    for zz,rr in [(0,.65),(h*.35,1),(h*.72,.72)]:
        v.extend((x+r*rr*math.cos(i*math.tau/6),y+r*rr*math.sin(i*math.tau/6),z+zz) for i in range(6))
    v.append((x+r*.1,y,z+h));f=[tuple(reversed(range(6)))]
    for j in range(2):f.extend((j*6+i,j*6+(i+1)%6,(j+1)*6+(i+1)%6,(j+1)*6+i) for i in range(6))
    f.extend((12+i,12+(i+1)%6,18) for i in range(6));return mesh(name,v,f,'Crystal',group)
def diamond(name,x,y,z,r,group):return plate(name,(x,y,z),r*1.4,r*1.8,.03,'Crystal',group,r*.6)
def join_group(name,root):
    obs=groups.pop(name);bpy.ops.object.select_all(action='DESELECT')
    for ob in obs:ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name
    for slot in ob.material_slots:
        mat=slot.material;slot.link='DATA';slot.material=mat
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.uv.smart_project(island_margin=.012);bpy.ops.object.mode_set(mode='OBJECT')
    ob.parent=root;parts.append(ob);return ob
def root(name):
    ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob);return ob
def anchor(name,loc,parent):
    ob=root(name);ob.location=loc;ob.parent=parent;return ob
def parchment(name,w,h,y,z,group):
    nx=24 if h>1 else 12;nz=20 if h>1 else 6;v=[]
    for j in range(nz+1):
        for i in range(nx+1):
            x=-w/2+w*i/nx;zz=z-h/2+h*j/nz
            yy=y-.009*math.sin(i*math.pi/nx)+.008*math.cos(j*math.tau/nz)
            v.append((x,yy,zz))
    f=[]
    for j in range(nz):
        for i in range(nx):
            k=j*(nx+1)+i;f.append((k,k+1,k+nx+2,k+nx+1))
    ob=mesh(name,v,f,'Parchment',group);uv=ob.data.uv_layers.new(name='UVMap')
    for p in ob.data.polygons:
        for li in p.loop_indices:
            co=ob.data.vertices[ob.data.loops[li].vertex_index].co;uv.data[li].uv=((co.x+w/2)/w,(co.z-z+h/2)/h)
    bpy.context.view_layer.objects.active=ob;mod=ob.modifiers.new('ParchmentThickness','SOLIDIFY');mod.thickness=.012;bpy.ops.object.modifier_apply(modifier=mod.name)
    return ob

# Information Board: dark stone core, layered gold trim, unlettered parchment.
info=root('InformationBoard')
box('Foot',(0,0,.09),(1.96,.92,.18),'Stone','Info_StoneBase',.035)
box('UpperFoot',(0,.02,.23),(1.80,.79,.12),'Stone','Info_StoneBase')
for s in [-1,1]:
    box('SupportPost',(s*.73,.13,.58),(.25,.32,.62),'Stone','Info_StoneBase')
    box('SupportCollar',(s*.73,.13,.80),(.31,.37,.12),'Metal','Info_GoldFrame')
plate('BoardStoneBacking',(0,.12,2.02),2.09,2.40,.21,'Stone','Info_StoneFrame',.10)
plate('OuterGoldRim',(0,-.011,2.02),2.04,2.34,.072,'Metal','Info_GoldFrame',.09)
plate('InsetStone',(0,-.054,2.02),1.85,2.16,.05,'Stone','Info_StoneFrame',.045)
paper=parchment('Info_ParchmentSurface',1.66,1.98,-.096,2.02,'Info_Paper');paper.parent=info;parts.append(paper);groups.pop('Info_Paper')
for s in [-1,1]:
    for zz in [.84,3.20]:
        box('OrnateCorner',(s*.98,-.072,zz),(.22,.13,.18),'Metal','Info_GoldFrame')
        cylinder('CornerRivet',(s*.98,-.148,zz),.022,.012,'Metal','Info_GoldFrame','Y')
    for zz in [1.08,2.96]:diamond('ParchmentCornerCrystal',s*.77,-.135,zz,.062,'Info_CrystalAccents')
    gem('FootCrystal',(s*.88,-.24,.30),.06,.21,'Info_CrystalAccents')
box('GoldBottomRail',(0,-.125,1.01),(1.72,.035,.035),'Metal','Info_GoldFrame',.007)
box('GoldTopRail',(0,-.125,3.03),(1.72,.035,.035),'Metal','Info_GoldFrame',.007)
diamond('TopCrystal',0,-.09,3.23,.075,'Info_CrystalAccents')
for name in list(groups):join_group(name,info)
for name,z in [('ChallengeTitleAnchor',2.77),('ChallengeFormulaAnchor',2.22),('ChallengeInstructionsAnchor',1.61)]:
    anchor(name,(0,-.15,z),info);anchor(name+'_Facing',(0,-.25,z),info)

# Formula stand: sloping posts and four linked scroll meshes.
rack=root('FormulaScrollsStand');angle=math.radians(14)
box('RackFoot',(0,0,.075),(1.51,.96,.15),'Stone','Rack_StoneBase',.032)
box('RackUpperFoot',(0,.04,.185),(1.37,.83,.07),'Metal','Rack_MetalFrame')
for s in [-1,1]:
    # Vertical rear leg and angled front post make a stable freestanding rack.
    box('RearLeg',(s*.60,.32,.47),(.13,.16,.62),'Stone','Rack_StoneBase')
    ob=box('AngledSideRail',(s*.62,.105,1.22),(.14,.16,2.02),'Stone','Rack_StoneFrame');ob.rotation_euler.x=-angle
    ob=box('GoldSideInlay',(s*.62,.012,1.23),(.037,.026,1.94),'Metal','Rack_MetalFrame',.006);ob.rotation_euler.x=-angle
    gem('RackFootCrystal',(s*.64,-.30,.22),.045,.16,'Rack_CrystalAccents')
box('RackTopCap',(0,.35,2.22),(1.44,.26,.13),'Metal','Rack_MetalFrame')
diamond('RackTopCrystal',0,.20,2.22,.07,'Rack_CrystalAccents')
for z in [.55,.98,1.41,1.84]:
    y=.105+math.tan(angle)*(z-1.22)
    box('ScrollShelf',(0,y-.11,z-.19),(1.26,.19,.065),'Metal','Rack_MetalFrame',.015)
for name in list(groups):join_group(name,rack)
# Build one reusable local scroll; each slot references its exact same mesh data.
parchment('ScrollParchment',1.04,.30,-.040,0,'ReusableScroll')
for s in [-1,1]:
    cylinder('RolledParchmentEnd',(s*.53,-.036,0),.045,.35,'Parchment','ReusableScroll')
    for dz in [-.177,.177]:cylinder('ScrollEndCap',(s*.53,-.036,dz),.052,.025,'Metal','ReusableScroll')
scroll=join_group('ReusableScroll',None)
# Tilt is applied once to the shared mesh, leaving instance rotation/scale identity.
from mathutils import Matrix
scroll.data.transform(Matrix.Rotation(-angle,4,'X'));scroll.data.update()
slots=[]
for i,z in enumerate([1.94,1.51,1.08,.65],1):
    y=.105+math.tan(angle)*(z-1.22)-.115
    slot=anchor(f'ScrollSlot_{i:02d}',(0,y,z),rack);slots.append(slot)
    if i==1:ob=scroll
    else:ob=bpy.data.objects.new(f'Scroll_{i:02d}_ReusableParchment',scroll.data);bpy.context.collection.objects.link(ob);parts.append(ob)
    # Export instances directly below the root: nested transformed FBX empties
    # can introduce an extra axis rotation in Unity. Setup recreates slot groups.
    ob.name=f'Scroll_{i:02d}_ReusableParchment';ob.parent=rack;ob.location=(0,y,z)
    front=Vector((0,-math.cos(angle),math.sin(angle)))
    anchor(f'ScrollLabelAnchor_{i:02d}',Vector((0,y,z))+front*.105,rack)
    anchor(f'ScrollLabelAnchor_{i:02d}_Facing',Vector((0,y,z))+front*.205,rack)

def descendants(root):return [root]+list(root.children_recursive)
def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
def stats(objects):
    obs=[o for o in objects if o.type=='MESH'];tri=0;zero=0;uv=0
    for ob in obs:
        ob.data.calc_loop_triangles();tri+=len(ob.data.loop_triangles);zero+=sum(p.area<1e-10 for p in ob.data.polygons);uv+=len(ob.data.uv_layers)>0
    return dict(mesh_objects=len(obs),triangles=tri,zero_area_faces=zero,uv_mapped_objects=uv)
geometry=[]
for model in [info,rack]:
    obs=descendants(model);before=stats(obs);select(obs)
    filename=model.name+'.fbx'
    bpy.ops.export_scene.fbx(filepath=str(OUT/filename),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,bake_anim=False,path_mode='COPY',embed_textures=True)
    original=set(scene.objects);bpy.ops.import_scene.fbx(filepath=str(OUT/filename));imported=list(set(scene.objects)-original)
    assert stats(imported)==before,(stats(imported),before)
    assert all(all(abs(s-1)<1e-5 for s in o.scale) for o in imported)
    for o in imported:bpy.data.objects.remove(o,do_unlink=True)
    # Confirm portable relative file references inside the binary FBX.
    from io_scene_fbx import parse_fbx
    tree,version=parse_fbx.parse(str(OUT/filename));refs=[]
    def walk(e):
        if e.id==b'RelativeFilename' and e.props:refs.append(e.props[0].decode(errors='replace'))
        for c in e.elems:walk(c)
    walk(tree)
    assert refs and all(not Path(p.replace('\\','/')).is_absolute() and ':/' not in p.replace('\\','/') for p in refs),refs
    geometry.append(dict(name=model.name,**before,fbx_roundtrip='PASS',relative_texture_references=refs,pivot='bottom center',text_in_fbx=False))
assert all(n.type not in ['TEX_NOISE','TEX_VORONOI','TEX_MUSGRAVE','BUMP'] for m in materials.values() for n in m.node_tree.nodes)
for im in bpy.data.images:
    if im.name.startswith(tuple(recipes)):
        im.filepath='//../../Assets/Art/InductionBoards/Textures/'+Path(im.filepath).name
        im.pack()
select(descendants(info)+descendants(rack));bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'InductionBoards.blend'))
(SRC/'bake-report.json').write_text(json.dumps(bake_report,indent=2))
(SRC/'geometry-report.json').write_text(json.dumps(geometry,indent=2))
print('INDUCTION_BOARDS_GEOMETRY_PASS '+json.dumps(geometry))

# Studio previews of only these two assets; excluded from source/FBX exports.
for model in [info,rack]:model.hide_render=False
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.02));ground=bpy.context.object
m=bpy.data.materials.new('StudioGround');m.diffuse_color=(.03,.04,.065,1);ground.data.materials.append(m)
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for loc,power,size in [((-3,-4,6),800,4),((4,-2,4),600,3),((1,4,5),900,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.data.energy=power;l.data.size=size;aim(l,(0,0,1.5))
bpy.ops.object.camera_add(location=(3.5,-6.7,3.2));camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO'
scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.world.color=(.09,.11,.16);scene.view_settings.view_transform='AgX'
scene.render.resolution_x=1000;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
for model,other,scale,target in [(info,rack,4.0,(0,0,1.65)),(rack,info,2.9,(0,0,1.14))]:
    for ob in descendants(other):ob.hide_render=True
    for ob in descendants(model):ob.hide_render=False
    camera.data.ortho_scale=scale;aim(camera,target);scene.render.filepath=str(SRC/(model.name+'_BlenderPreview.png'));bpy.ops.render.render(write_still=True)
print('INDUCTION_BOARDS_BUILD_COMPLETE')
