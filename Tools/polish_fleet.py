"""Pose yards into the wind and UV the authored sails/deck. Fast finishing pass."""
import bpy,math
from pathlib import Path
R=Path(__file__).resolve().parents[1];bpy.ops.wm.open_mainfile(filepath=str(R/'ArtSource/Coastal_Fleet.blend'))
for rig in [o for o in bpy.data.objects if o.name.startswith('ANIM_Sail')]:
 if not rig.get('yard_pose'):
  sail=next(c for c in rig.children if c.name.startswith('Canvas billow'))
  y=min(v.co.y for v in sail.data.vertices);rig.location.y=y
  for c in rig.children:c.location.y-=y
  rig.rotation_euler.z=math.radians(38);rig['yard_pose']=True
for ob in [o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Jib')]:
 # Keep the staysail forward of the foremast and its braced square sails.
 brig=ob.parent.name=='Brigantine';top=6.2 if brig else 2.7;bow=13.5 if brig else 10
 for v,p in zip(ob.data.vertices,[(.2,bow,2.8),(.15,top,11.5),(.8,top+2.2,3.1)]):v.co=p
for ob in [o for o in bpy.data.objects if o.type=='MESH']:
 if not (ob.name.startswith('Canvas') or ob.name.startswith('Deck plank')):continue
 uv=ob.data.uv_layers.active or ob.data.uv_layers.new(name='Fabric / plank grain')
 for p in ob.data.polygons:
  for idx in p.loop_indices:
   v=ob.data.vertices[ob.data.loops[idx].vertex_index].co
   uv.data[idx].uv=(v.x*.22,(v.z if ob.name.startswith('Canvas') else v.y)*.22)
for name in ['Cutter','Brigantine','SteamTrawler']:
 root=bpy.data.objects[name];old=root.location.copy();root.location=(0,0,0)
 bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
 for c in root.children_recursive:c.select_set(True)
 bpy.context.view_layer.objects.active=root;bpy.context.view_layer.update()
 bpy.ops.export_scene.fbx(filepath=str(R/f'Lighthouse/Assets/Art/Fleet/{name}.fbx'),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE')
 root.location=old
bpy.ops.wm.save_as_mainfile(filepath=str(R/'ArtSource/Coastal_Fleet.blend'));print('FLEET_POLISH_COMPLETE')
