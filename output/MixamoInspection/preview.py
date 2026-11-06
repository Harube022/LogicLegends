import bpy, sys, zipfile
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
with zipfile.ZipFile(r'C:\Users\harub\OneDrive\Documents\BLENDER\REVAMPSKINS\Female Skins\Female_Common\female_common_garden_day_Mixamo_UPLOAD - Copy.zip') as z:
    f=next(n for n in z.namelist() if n.lower().endswith('.fbx')); p=z.extract(f,sys.argv[sys.argv.index('--')+1])
bpy.ops.import_scene.fbx(filepath=p)
o=next(o for o in bpy.data.objects if o.type=='MESH')
center=sum((o.matrix_world@Vector(c) for c in o.bound_box),Vector())/8
bpy.ops.object.camera_add(location=center+Vector((0,-0.5,4)))
c=bpy.context.object;c.rotation_euler=(center-c.location).to_track_quat('-Z','Y').to_euler();c.data.type='ORTHO';c.data.ortho_scale=2.2
s=bpy.context.scene;s.camera=c;s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='MATERIAL';s.render.resolution_x=640;s.render.resolution_y=640;s.render.resolution_percentage=100;s.render.filepath=sys.argv[sys.argv.index('--')+1]+'/preview.png'
bpy.ops.render.render(write_still=True)
