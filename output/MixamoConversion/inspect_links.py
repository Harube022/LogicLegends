import bpy,pathlib,json
root=pathlib.Path(r'C:\Users\harub\OneDrive\Documents\BLENDER\REVAMPSKINS')
for gender in ['Female Skins','Male Skins']:
 for p in sorted((root/gender).glob('*/*.blend')):
  bpy.ops.wm.open_mainfile(filepath=str(p))
  for o in bpy.context.scene.objects:
   if o.type!='MESH':continue
   for m in o.data.materials:
    if not m or not m.use_nodes:continue
    print('LINKS',p.parent.name,m.name,[(l.from_node.name,l.from_socket.name,l.to_node.name,l.to_socket.name) for l in m.node_tree.links])
