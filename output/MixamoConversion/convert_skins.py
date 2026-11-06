"""Non-destructive Blender-to-OBJ export and local verification for Mixamo."""
import bpy, pathlib, json, sys, hashlib, re, zipfile, math
from mathutils import Vector
ROOT=pathlib.Path(r'C:\Users\harub\OneDrive\Documents\BLENDER\REVAMPSKINS')
OUT=pathlib.Path(sys.argv[sys.argv.index('--')+1]).resolve()
OUT.mkdir(parents=True,exist_ok=True)
reports=[]
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def bounds(objects):
 pts=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
 return Vector(tuple(min(p[i] for p in pts) for i in range(3))),Vector(tuple(max(p[i] for p in pts) for i in range(3)))
for gender in ['Female Skins','Male Skins']:
 for source in sorted((ROOT/gender).glob('*/*.blend')):
  original_hash=digest(source)
  name=source.parent.name.lower()
  dest=OUT/gender/source.parent.name
  dest.mkdir(parents=True,exist_ok=True)
  textures=dest/'textures'; textures.mkdir(exist_ok=True)
  bpy.ops.wm.open_mainfile(filepath=str(source))
  meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
  assert meshes, str(source)
  assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects),'Unexpected rig; inspect before export'
  used_mats=list({m for o in meshes for m in o.data.materials if m})
  exported_images={}
  relinked=[]
  for mi,m in enumerate(sorted(used_mats,key=lambda m:m.name)):
   m.name=f'{name}_material_{mi+1}'
   for n in m.node_tree.nodes:
    if n.type!='TEX_IMAGE' or not n.image:continue
    img=n.image
    if img.as_pointer() in exported_images:
     n.image=exported_images[img.as_pointer()];continue
    label='basecolor' if n.name=='Image Texture' else 'roughness' if n.name=='Image Texture.001' else 'metallic' if n.name=='Image Texture.002' else 'normal' if n.name=='Image Texture.003' else re.sub('[^a-zA-Z0-9]+','_',n.name)
    path=textures/f'{name}_m{mi+1}_{label}.png'
    colorspace=img.colorspace_settings.name
    if img.packed_file:
     data=bytes(img.packed_file.data)
     assert data[:8]==b'\x89PNG\r\n\x1a\n','Unexpected packed image format'
     path.write_bytes(data)
    else:
     existing=pathlib.Path(bpy.path.abspath(img.filepath))
     if not existing.is_file():
      existing=source.parent/pathlib.Path(img.filepath.replace('\\','/')).name
      relinked.append(str(existing))
     assert existing.is_file(),f'Missing image {img.filepath}'
     path.write_bytes(existing.read_bytes())
    new=bpy.data.images.load(str(path),check_existing=False)
    new.colorspace_settings.name=colorspace
    assert new.size[0]>0 and new.size[1]>0
    exported_images[img.as_pointer()]=new;n.image=new
   diffuse=m.node_tree.nodes.get('Image Texture')
   if diffuse: m.node_tree.nodes.active=diffuse
  bpy.ops.object.select_all(action='DESELECT')
  for o in meshes:
   o.hide_render=False;o.hide_viewport=False;o.hide_set(False);o.select_set(True)
   assert o.data.uv_layers, f'No UV map: {o.name}'
  low,high=bounds(meshes)
  offset=Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z))
  for o in meshes:o.location+=offset
  bpy.context.view_layer.update()
  low,high=bounds(meshes)
  assert (high-low).z > (high-low).y*2,'Character not upright'
  expected_faces=sum(len(o.data.polygons) for o in meshes)
  expected_verts=sum(len(o.data.vertices) for o in meshes)
  obj=dest/f'{name}.obj'
  bpy.ops.wm.obj_export(filepath=str(obj),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',apply_modifiers=True,export_uv=True,export_normals=True,export_materials=True,export_pbr_extensions=True,path_mode='RELATIVE',export_triangulated_mesh=False)
  mtl=obj.with_suffix('.mtl')
  assert obj.exists() and mtl.exists()
  refs=[]
  for line in mtl.read_text().splitlines():
   if line.startswith(('map_','bump ','disp ','norm ')):
    ref=line.split()[-1];target=dest/ref
    assert target.is_file(),f'Broken MTL reference {ref}'
    refs.append(ref)
  assert any(l.startswith('map_Kd ') for l in mtl.read_text().splitlines()),'Diffuse texture missing'
  # Round-trip the exported OBJ in a clean scene, including its MTL and images.
  bpy.ops.wm.read_factory_settings(use_empty=True)
  bpy.ops.wm.obj_import(filepath=str(obj),forward_axis='NEGATIVE_Z',up_axis='Y')
  imported=[o for o in bpy.context.scene.objects if o.type=='MESH']
  actual_faces=sum(len(o.data.polygons) for o in imported)
  actual_verts=sum(len(o.data.vertices) for o in imported)
  assert actual_faces==expected_faces and actual_verts==expected_verts,'Geometry changed on round trip'
  ilow,ihigh=bounds(imported)
  assert abs(ilow.z)<0.0001 and (ihigh-ilow).z > (ihigh-ilow).y*2
  for m in bpy.data.materials:
   if m.use_nodes:
    for n in m.node_tree.nodes:
     if n.type=='TEX_IMAGE' and n.image:
      assert pathlib.Path(bpy.path.abspath(n.image.filepath)).is_file()
      assert n.image.size[0]>0
      if n.image.colorspace_settings.name=='sRGB':m.node_tree.nodes.active=n
  # Neutral studio preview from actual imported output; never saved back to sources.
  scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH'
  scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE'
  scene.display.shading.show_shadows=True
  center=(ilow+ihigh)/2
  bpy.ops.object.camera_add(location=center+Vector((0,-4,0.05)))
  camera=bpy.context.object;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
  camera.data.type='ORTHO';camera.data.ortho_scale=max((ihigh-ilow).x,(ihigh-ilow).z)*1.15
  scene.camera=camera;scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.resolution_percentage=100
  scene.render.filepath=str(dest/'preview.png');bpy.ops.render.render(write_still=True)
  readme=f'''{source.parent.name} - Mixamo OBJ package\n\nUpload {name}_Mixamo_OBJ.zip to Mixamo, not the bare OBJ, to include materials/textures.\nThe upload ZIP contains the OBJ, MTL and textures/ folder. Keep these together.\nMesh only: no bones, animation, cameras or scene props. Neutral source pose retained.\nY-up OBJ export; feet grounded and horizontally centered. Original scale retained.\nSource: {source}\nGeometry: {actual_verts} vertices, {actual_faces} faces.\nLocal OBJ/MTL round-trip and all texture references verified.\nMixamo server auto-rigging has NOT been tested; approval still depends on its geometry detector.\nOBJ/MTL cannot preserve Blender procedural normal-strength masks; supplied normal/roughness/metallic maps are included, but Mixamo may display only base color.\n'''
  (dest/'README.txt').write_text(readme,encoding='utf-8')
  upload=dest/f'{name}_Mixamo_OBJ.zip'
  with zipfile.ZipFile(upload,'w',zipfile.ZIP_DEFLATED) as z:
   for p in [obj,mtl]+sorted(textures.glob('*')):z.write(p,p.relative_to(dest).as_posix())
  assert digest(source)==original_hash,'Source changed'
  report={'skin':source.parent.name,'source':str(source),'source_sha256':original_hash,'obj':str(obj.relative_to(OUT)),'upload_zip':str(upload.relative_to(OUT)),'vertices':actual_verts,'faces':actual_faces,'dimensions_blender_z_up':list(ihigh-ilow),'mtl_texture_references':refs,'relinked_images':relinked,'roundtrip_passed':True}
  reports.append(report)
  (OUT/'conversion_report.json').write_text(json.dumps(reports,indent=2))
  print('CONVERTED',json.dumps(report),flush=True)
(OUT/'README.txt').write_text('12 unrigged character OBJ packages for Mixamo.\nOpen Female Skins or Male Skins, then the rarity folder. Upload the *_Mixamo_OBJ.zip inside that folder to include textures.\nEach folder also includes the OBJ, MTL, textures, local preview and instructions.\nSources were not modified. Every export was reimported locally and checked; Mixamo uploads were not performed.\n',encoding='utf-8')
print('FINISHED',len(reports),flush=True)
