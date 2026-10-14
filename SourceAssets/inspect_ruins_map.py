import bpy,json
from mathutils import Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
folder=ROOT/'Assets/FINAL_ASSETS/MAPS/REMADE/REMAKE TUTS/2nd'
for name in ['FLOOR.fbx','RUINS.fbx','PLATFORM.fbx']:bpy.ops.import_scene.fbx(filepath=str(folder/name))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)));center=(lo+hi)*.5
print('MAP_BOUNDS',list(lo),list(hi),'PARTS',[(o.name,[min((o.matrix_world@v.co)[i] for v in o.data.vertices) for i in range(3)],[max((o.matrix_world@v.co)[i] for v in o.data.vertices) for i in range(3)]) for o in objects])
(ROOT/'SourceAssets/RuinsProps/map-fit-measurements.json').write_text(json.dumps([dict(name=o.name,min=[min((o.matrix_world@v.co)[i] for v in o.data.vertices) for i in range(3)],max=[max((o.matrix_world@v.co)[i] for v in o.data.vertices) for i in range(3)]) for o in objects],indent=2))
span=max(hi.x-lo.x,hi.y-lo.y)
bpy.ops.object.camera_add(location=center+Vector((span*.7,-span,span)));cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=span*1.35
scene=bpy.context.scene;scene.camera=cam;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True
scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.resolution_percentage=100;scene.render.filepath=str(ROOT/'SourceAssets/RuinsProps/Map_Identity_Check.png');bpy.ops.render.render(write_still=True)
