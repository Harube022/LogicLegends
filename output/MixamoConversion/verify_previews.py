import bpy,pathlib,zipfile,json
from mathutils import Vector
root=pathlib.Path('output/MixamoConversion/BLENDER_OBJ').resolve()
for obj in sorted(root.glob('*/*/*.obj')):
 mtl=obj.with_suffix('.mtl');s=mtl.read_text();s=s.replace('\nKs ','\nKa 0.000000 0.000000 0.000000\nKd 1.000000 1.000000 1.000000\nNs 32.000000\nKs ');mtl.write_text(s)
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.wm.obj_import(filepath=str(obj),forward_axis='NEGATIVE_Z',up_axis='Y')
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 for m in bpy.data.materials:
  bsdf=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
  base=bsdf.inputs['Base Color'].links[0].from_node
  assert base.type=='TEX_IMAGE' and 'basecolor' in base.image.filepath
  for n in list(m.node_tree.nodes):
   if n.type=='TEX_IMAGE' and n!=base:m.node_tree.nodes.remove(n)
  m.node_tree.nodes.active=base
 points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
 low=Vector(tuple(min(v[i] for v in points) for i in range(3)));high=Vector(tuple(max(v[i] for v in points) for i in range(3)));center=(low+high)/2
 bpy.ops.object.camera_add(location=center+Vector((0,-4,.05)))
 cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=max((high-low).x,(high-low).z)*1.15
 scene=bpy.context.scene;scene.camera=cam;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE';scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.resolution_percentage=100;scene.render.filepath=str(obj.parent/'preview.png');bpy.ops.render.render(write_still=True)
 with zipfile.ZipFile(obj.parent/(obj.stem+'_Mixamo_OBJ.zip'),'w',zipfile.ZIP_DEFLATED) as z:
  for p in [obj,mtl]+sorted((obj.parent/'textures').glob('*')):z.write(p,p.relative_to(obj.parent).as_posix())
print('PREVIEWS_FIXED')
