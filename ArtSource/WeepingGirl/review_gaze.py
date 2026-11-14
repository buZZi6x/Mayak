import bpy
from pathlib import Path
from mathutils import Vector
out=Path(__file__).resolve().parent/'V2'
bpy.ops.wm.open_mainfile(filepath=str(out/'WeepingGirlV2.blend'))
girl=bpy.data.objects['WeepingGirl'];keys=girl.data.shape_keys.key_blocks
scene=bpy.context.scene;scene.cycles.samples=12
scene.render.resolution_x=700;scene.render.resolution_y=850
cam=scene.camera;cam.location=(.25,-1.0,1.55);cam.data.lens=75
cam.rotation_euler=(Vector((0,-.03,1.48))-cam.location).to_track_quat('-Z','Y').to_euler()
for name in ('LookUp','LookDown','LookLeft','LookRight'):
    for key in keys:key.value=0
    keys[name].value=1
    scene.render.filepath=str(out/(name+'.png'))
    bpy.ops.render.render(write_still=True)
