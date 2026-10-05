"""Add a seamless three-second, rightward page-turn idle to the book statue."""
import bpy, bmesh, math, json, sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'SourceAssets/BookStatue'
OUT=ROOT/'Assets/Art/BookStatue'
bpy.ops.wm.open_mainfile(filepath=str(SRC/'Book_Statue.blend'))
scene=bpy.context.scene
scene.name='Book_Idle_PageFlip_Right_3s'
scene.camera.data.ortho_scale=3.6
root=bpy.data.objects['Book_Statue']
for name in ['Idle turning page','Page rig']:
    if bpy.data.objects.get(name):
        bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
for old_action in list(bpy.data.actions):
    if old_action.users==0:
        bpy.data.actions.remove(old_action)
scene.render.fps=30
scene.frame_start=1
scene.frame_end=91
tilt=math.atan(.62)
alpha=math.atan(.10*math.cos(tilt))
width=.798
depth=.905/math.cos(tilt)
n=12
verts=[]
for side in [-1,1]:
    for i in range(n+1):
        for y in [-depth/2,depth/2]:
            verts.append((width*i/n,y,side*.0015))
stride=2*(n+1)
faces=[]
for i in range(n):
    a=2*i
    faces.extend([(a,a+2,a+3,a+1),(stride+a+1,stride+a+3,stride+a+2,stride+a),
                  (a,stride+a,stride+a+2,a+2),(a+1,a+3,stride+a+3,stride+a+1)])
faces.extend([(0,1,stride+1,stride),(2*n,stride+2*n,stride+2*n+1,2*n+1)])
me=bpy.data.meshes.new('Flexible page mesh')
me.from_pydata(verts,[],faces)
me.update()
bm=bmesh.new()
bm.from_mesh(me)
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
bm.to_mesh(me)
bm.free()
page=bpy.data.objects.new('Idle turning page',me)
bpy.context.collection.objects.link(page)
me.materials.append(bpy.data.materials['Warm gray stone pages'])
page.parent=root
page.location=(0,0,1.62+.096)
page.rotation_euler.x=tilt
arm=bpy.data.armatures.new('Page skeleton')
rig=bpy.data.objects.new('Page rig',arm)
bpy.context.collection.objects.link(rig)
rig.parent=root
rig.location=page.location
rig.rotation_euler=page.rotation_euler
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for i in range(n+1):
    bone=arm.edit_bones.new(f'Page_{i:02d}')
    bone.head=(width*i/n,0,0)
    bone.tail=(width*i/n,.12,0)
bpy.ops.object.mode_set(mode='OBJECT')
for i in range(n+1):
    group=page.vertex_groups.new(name=f'Page_{i:02d}')
    group.add([2*i,2*i+1,stride+2*i,stride+2*i+1],1,'REPLACE')
mod=page.modifiers.new('Flexible page rig','ARMATURE')
mod.object=rig

# One forward turn per 90 frames; the identical blank stacks conceal the reset.
# Hold for two seconds, turn over .9 seconds, then tuck below the static page.
for frame in range(1,92):
    t=max(0,min(1,(frame-61)/27))
    eased=t*t*(3-2*t)
    theta=(math.pi-alpha)*(1-eased)+alpha*eased
    curl=.32*math.sin(math.pi*t)
    visible=61<=frame<=88
    if not visible:
        theta=math.pi-alpha
        curl=0
    p=[0.,0.]
    for i in range(n+1):
        s=width*i/n
        angle=theta+curl*math.sin(math.pi*i/n)
        if i:
            mid=theta+curl*math.sin(math.pi*(i-.5)/n)
            p[0]+=width/n*math.cos(mid)
            p[1]+=width/n*math.sin(mid)
        bone=rig.pose.bones[f'Page_{i:02d}']
        bone.rotation_mode='XYZ'
        bone.location=(p[0]-s,0,p[1] if visible else -.04)
        bone.rotation_euler=(0,-angle,0)
        bone.scale=(1,1,1) if visible else (.00001,.00001,.00001)
        for path in ['location','rotation_euler','scale']:
            bone.keyframe_insert(data_path=path,frame=frame,group=bone.name)
action=rig.animation_data.action
action.name='Book_Idle_PageFlip_Right_3s'
for curve in action.fcurves:
    for key in curve.keyframe_points:
        key.interpolation='LINEAR'
    curve.modifiers.new('CYCLES')
scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for ob in [root]+list(root.children_recursive):
    ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'Book_Statue.fbx'),use_selection=True,
    object_types={'MESH','EMPTY','ARMATURE'},axis_forward='-Z',axis_up='Y',
    apply_unit_scale=True,add_leaf_bones=False,bake_anim=True,
    bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0)
bpy.ops.export_scene.gltf(filepath=str(SRC/'Book_Statue.glb'),use_selection=True,
    export_format='GLB',export_animations=True,export_frame_range=True,
    export_animation_mode='SCENE',export_anim_scene_split_object=False)
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Book_Statue.blend'))

# Validate the evaluated page moves from left to right and the loop closes.
positions={}
for frame in [1,61,74,88,91]:
    scene.frame_set(frame)
    deps=bpy.context.evaluated_depsgraph_get()
    evaluated=page.evaluated_get(deps)
    positions[frame]=list(evaluated.matrix_world @ evaluated.data.vertices[2*n].co)
assert positions[61][0]<-.7 and positions[88][0]>.7, positions
assert positions[74][2]>positions[61][2]+.5, positions
assert max(abs(a-b) for a,b in zip(positions[1],positions[91]))<1e-6
(SRC/'AnimationValidation.json').write_text(json.dumps({'duration_seconds':3,'fps':30,
    'frame_range':[1,91],'page_tip_positions':positions,'checks':'passed'},indent=2))
(SRC/'README.txt').write_text('Book statue with a rightward page-turn idle.\n'
    'Animation: Book_Idle_PageFlip_Right_3s, 30 FPS, frames 1-91, duration 3 seconds.\n'
    'The page rests for two seconds, flips from left to right, and resets concealed by the blank page stacks.\n'
    'Unity: import FBX with animation enabled, use Generic rig, enable Loop Time for the imported clip, and assign it as the Animator default state.\n'
    'Origin: base center (0,0,0), Y-up FBX. Source BLEND and GLB also updated.\n'
    'No Unity scene or gameplay logic was changed.\n',encoding='utf-8')
scene.render.resolution_x=480
scene.render.resolution_y=480
scene.render.resolution_percentage=100
scene.cycles.samples=4
scene.cycles.use_denoising=True
preview=SRC/'IdlePreviewFrames'
preview.mkdir(exist_ok=True)
for i in ([] if '--skip-preview' in sys.argv else range(24 if '--flip-preview' in sys.argv else 0,36)):
    value=1+i*2.5
    scene.frame_set(int(value),subframe=value-int(value))
    scene.render.filepath=str(preview/f'{i:03d}.png')
    bpy.ops.render.render(write_still=True)
print('ANIMATION_COMPLETE: 3-second loop, rightward turn, matching endpoints verified.')
