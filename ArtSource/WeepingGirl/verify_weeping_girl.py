"""One-off FBX round-trip and neutral motion review for this authored asset."""
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector

SRC=Path(__file__).resolve().parent
OUT=SRC/'V2'
contract=json.loads((OUT/'asset-contract.json').read_text())
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'WeepingGirlV2.fbx'))
girl=bpy.data.objects['WeepingGirl'];mesh=girl.data;keys=mesh.shape_keys.key_blocks
checks=[]
def check(name,ok,detail):
    checks.append({'name':name,'pass':bool(ok),'detail':detail})
check('shape_names',list(keys.keys())==contract['shape_keys'],list(keys.keys()))
check('no_attack','Attack' not in keys,'No camera-lunge mesh deformation')
check('finite_vertices',all(math.isfinite(c) for key in keys for point in key.data for c in point.co),'All exported full-pose keys and additive keys')
world=girl.matrix_world
stand=[world@point.co for point in keys['Basis'].data]
delta=max((world@p.co-q).length for p,q in zip(keys['Rise_08'].data,stand))
check('basis_is_standing',delta<.00001,delta)
foot_indices=[i for i,p in enumerate(stand) if p.z<.025 and abs(p.x)>.10]
foot_motion=max((world@key.data[i].co-stand[i]).length for key in keys if key.name.startswith('Rise_') or key.name=='Sob' for i in foot_indices)
check('planted_soles',foot_motion<.002,{'max_displacement_m':foot_motion,'vertices':len(foot_indices)})
sob_motion=max((world@p.co-q).length for p,q in zip(keys['Sob'].data,stand))
check('small_sob',sob_motion<.010,sob_motion)
for name in ('LookUp','LookDown','LookLeft','LookRight'):
    maximum=max((world@p.co-q).length for p,q in zip(keys[name].data,stand))
    check('reasonable_'+name,maximum<.40,maximum)
height=max(p.z for p in stand)-min(p.z for p in stand)
check('human_height',1.60<height<1.75,height)
check('material_slots',[m.name for m in mesh.materials]==contract['material_order'],[m.name for m in mesh.materials])
adjacent=max((world@keys['Rise_%02d'%(k+1)].data[i].co-world@keys['Rise_%02d'%k].data[i].co).length for k in range(8) for i in range(len(stand)))
check('adjacent_pose_bounds',adjacent<.50,adjacent)
for marker,name in [('SeatedFace','seated_face'),('StandingFace','standing_face'),('SeatContact','seat_contact_approximate')]:
    p=bpy.data.objects[marker].matrix_world.translation
    expected=contract['unity_markers'][name]
    found=[p.x,p.z,-p.y]
    error=max(abs(a-b) for a,b in zip(expected,found))
    check('marker_'+marker,error<.00001,found)
result={'pass':all(c['pass'] for c in checks),'checks':checks,'vertices':len(mesh.vertices),'polygons':len(mesh.polygons)}
(OUT/'import-validation.json').write_text(json.dumps(result,indent=2))
print('FBX_VALIDATION',json.dumps(result))
assert result['pass'],'FBX round-trip validation failed'

if '--motion' in sys.argv:
    bpy.ops.wm.open_mainfile(filepath=str(OUT/'WeepingGirlV2.blend'))
    girl=bpy.data.objects['WeepingGirl'];keys=girl.data.shape_keys.key_blocks
    scene=bpy.context.scene
    for key in keys:key.value=0
    for frame,pose in [(1,0)]+[(13+12*k,k) for k in range(9)]+[(121,8)]:
        for k in range(9):
            key=keys['Rise_%02d'%k];key.value=1 if k==pose else 0;key.keyframe_insert(data_path='value',frame=frame)
    action=girl.data.shape_keys.animation_data.action
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag=strip.channelbag(slot)
                if bag:
                    for fc in bag.fcurves:
                        for p in fc.keyframe_points:p.interpolation='LINEAR'
    scene.frame_start=1;scene.frame_end=121;scene.render.fps=30
    scene.render.resolution_x=480;scene.render.resolution_y=640;scene.render.resolution_percentage=100
    scene.cycles.samples=4;scene.cycles.use_denoising=True
    cam=scene.camera;cam.location=(1.6,-3.3,1.40);cam.data.lens=70
    cam.rotation_euler=(Vector((0,0,.85))-cam.location).to_track_quat('-Z','Y').to_euler()
    frames=OUT/'motion_frames';frames.mkdir(exist_ok=True)
    scene.render.filepath=str(frames/'frame_')
    scene.render.image_settings.file_format='PNG'
    bpy.ops.render.render(animation=True)
