import bpy,json,pathlib
root=pathlib.Path(r'C:\Users\harub\OneDrive\Documents\BLENDER\REVAMPSKINS')
reports=[]
for gender in ['Female Skins','Male Skins']:
 for path in sorted((root/gender).glob('*/*.blend')):
  bpy.ops.wm.open_mainfile(filepath=str(path))
  entry={'path':str(path),'objects':[],'materials':[]}
  for o in bpy.context.scene.objects:
   entry['objects'].append({'name':o.name,'type':o.type,'hide':o.hide_render,'dimensions':list(o.dimensions),'rotation':list(o.rotation_euler),'verts':len(o.data.vertices) if o.type=='MESH' else None,'modifiers':[m.type for m in o.modifiers]})
  for m in bpy.data.materials:
   entry['materials'].append({'name':m.name,'nodes':[{'type':n.type,'name':n.name,'image':n.image.name if n.type=='TEX_IMAGE' and n.image else None,'path':n.image.filepath if n.type=='TEX_IMAGE' and n.image else None,'packed':bool(n.image.packed_file) if n.type=='TEX_IMAGE' and n.image else None} for n in m.node_tree.nodes] if m.use_nodes else []})
  reports.append(entry)
pathlib.Path('output/MixamoConversion/source_inspection.json').write_text(json.dumps(reports,indent=2))
print('INSPECTION',json.dumps(reports))
