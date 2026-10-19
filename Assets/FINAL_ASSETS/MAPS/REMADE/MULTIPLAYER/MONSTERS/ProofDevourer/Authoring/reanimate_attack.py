"""Update only the existing Attack action and re-export the existing character.

blender --background --factory-startup --python <this file>
The mesh, materials, weights, rig and other twelve actions are preserved.
"""
import ast
import bpy
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector, Matrix

BASE=Path(__file__).resolve().parents[1]
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.open_mainfile(filepath=str(BASE/'Models/ProofDevourer.blend'))
scene=bpy.context.scene
rig=bpy.data.objects['DevourerRig'];rig_data=rig.data
bone_defs=[(b.name,b.head_local.copy(),b.tail_local.copy(),b.parent.name if b.parent else None) for b in rig_data.bones]
rest={name:(h,t,parent) for name,h,t,parent in bone_defs}
mouth_controls=['Mouth_Left','Mouth_Upper','Mouth_Right','Mouth_Lower']

# Import only the existing animation solver, never the procedural model rebuild.
tree=ast.parse((BASE/'Authoring/create_proof_devourer.py').read_text(encoding='utf-8-sig'))
functions={'rx','ry','rz','smooth','mix','solve_ik','blend_pose','pose_params','target_matrices','action_curves'}
constants={'SLEEP','W1','W2','W3','IDLE','CLIPS'}
nodes=[node for node in tree.body if (isinstance(node,ast.FunctionDef) and node.name in functions) or
       (isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id in constants for t in node.targets))]
exec(compile(ast.Module(body=nodes,type_ignores=[]),'existing_animation_solver','exec'),globals())

def action_hash(action):
    values=[(c.data_path,c.array_index,[(tuple(k.co),k.interpolation) for k in c.keyframe_points]) for c in action_curves(action)]
    return hashlib.sha256(repr(values).encode()).hexdigest()
def geometry_hash():
    values=[]
    for obj in bpy.data.objects:
        if obj.type!='MESH' or obj.name=='Review floor':continue
        values.append((obj.name,[tuple(v.co) for v in obj.data.vertices],
                       [tuple(p.vertices) for p in obj.data.polygons],
                       [[(g.group,g.weight) for g in v.groups] for v in obj.data.vertices]))
    values.append([(name,tuple(h),tuple(t),parent) for name,h,t,parent in bone_defs])
    return hashlib.sha256(repr(values).encode()).hexdigest()

original_actions={a.name:action_hash(a) for a in bpy.data.actions if a.name!='Attack'}
original_geometry=geometry_hash()
old_frame=scene.frame_current;old_action=rig.animation_data.action
bpy.data.actions.remove(bpy.data.actions['Attack'],do_unlink=True)
action=bpy.data.actions.new('Attack');action.use_fake_user=True;rig.animation_data.action=action
previous={};samples=[]
for frame in range(55):
    t=frame/54;matrices=target_matrices(pose_params('Attack',t),t)
    for name,_,_,parent in bone_defs:
        bone=rig_data.bones[name];pb=rig.pose.bones[name]
        basis=bone.matrix_local.inverted()@(rig_data.bones[parent].matrix_local@matrices[parent].inverted() if parent else Matrix.Identity(4))@matrices[name]
        loc,rotation,scale=basis.decompose()
        if name in previous and rotation.dot(previous[name])<0:rotation.negate()
        previous[name]=rotation.copy();pb.location=loc;pb.rotation_quaternion=rotation;pb.scale=scale
        for path in ('location','rotation_quaternion','scale'):pb.keyframe_insert(data_path=path,frame=frame+1,group=name)
    samples.append({'seconds':round(frame/30,5),'right_wrist':list(matrices['Hand.R'].translation),
                    'left_wrist':list(matrices['Hand.L'].translation),
                    'left_foot':list(matrices['Foot.L'].translation),'right_foot':list(matrices['Foot.R'].translation)})
for curve in action_curves(action):
    for key in curve.keyframe_points:key.interpolation='LINEAR'
action['loop']=False;action['duration_seconds']=1.8;action['in_place']=True
action['motion']='Right claw cross-body slash; planted left forepaw and hind feet; brief anticipation and weighted recovery.'
assert original_geometry==geometry_hash(),'Character geometry or rig changed'
assert original_actions=={a.name:action_hash(a) for a in bpy.data.actions if a.name!='Attack'},'Another action changed'

# Re-export the same character using its original FBX axis/hierarchy settings.
# This preserves Unity's bone coordinate conversion and the existing asset IDs.
scene.render.fps=30;scene.frame_start=1;scene.frame_end=55;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
for obj in rig.children:
    if obj.type=='MESH':obj.select_set(True)
rig.rotation_euler.z=math.pi;bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(BASE/'Models/ProofDevourer.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
    use_space_transform=True,bake_space_transform=False,mesh_smooth_type='FACE',add_leaf_bones=False,use_armature_deform_only=False,
    bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_step=1,bake_anim_simplify_factor=0,
    bake_anim_force_startend_keying=True,path_mode='RELATIVE',use_mesh_modifiers=True)
rig.rotation_euler.z=0;rig.animation_data.action=old_action
scene.frame_start=1;scene.frame_end=103;scene.frame_set(old_frame);bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Models/ProofDevourer.blend'))
report={'geometry_and_rig_unchanged':True,'other_actions_unchanged':True,'other_action_count':len(original_actions),
        'duration':1.8,'samples':samples,'source':'Existing 33-bone rig and mesh; only Attack action changed.'}
(BASE/'Previews/Slash_SourceValidation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('KHEZU_SLASH_READY',json.dumps({k:v for k,v in report.items() if k!='samples'}),flush=True)

