import bpy, math
from pathlib import Path
from mathutils import Vector, Matrix

root = Path(__file__).resolve().parents[1]
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(root/'ArtSource/CoastalLife/FlyingSeagull.glb'))
bird = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bird.name = 'Seagull'
original = [v.co.copy() for v in bird.data.vertices]
scale = .0085
texture = next(n.image for n in bird.active_material.node_tree.nodes if n.type == 'TEX_IMAGE')
pixels = list(texture.pixels)
width, height = texture.size
leg_vertices = set()
for polygon in bird.data.polygons:
    uv = sum((bird.data.uv_layers.active.data[j].uv for j in polygon.loop_indices), Vector((0,0))) / len(polygon.loop_indices)
    offset = ((int(uv.y*height)%height)*width + int(uv.x*width)%width)*4
    r,g,b,a = pixels[offset:offset+4]
    if r > b*2 and g > b*1.5 and polygon.center.x < -10:
        leg_vertices.update(polygon.vertices)

# Convert the source's raised wings into a level gliding pose, retaining its UVs.
# Local +X is right, -Y is the beak, +Z is up; FBX exports this to Unity.
def point(v, pose, is_leg=False):
    x, y, z = v
    side = 1 if y >= 0 else -1
    weight = max(0, min(1, (abs(y)-5.0)/4.0)) if z > 5.5 else 0
    p = Vector((y, -x, z))
    pivot = Vector((side*5, -4.5, 8.5))
    if weight:
        angle = {'Glide':-34, 'Up':18, 'Down':-62, 'Fold':-34}[pose]
        # Rotation around longitudinal Y: positive rotates the right wing down.
        q = Matrix.Rotation(math.radians(-side*angle), 4, 'Y')
        target = pivot + q @ (p-pivot)
        if pose == 'Fold':
            target = pivot + Matrix.Rotation(math.radians(side*78),4,'Z') @ (target-pivot)
            target.x = side*(5 + (abs(target.x)-5)*.37)
            target.z = 8.0 + (target.z-8.5)*.25
        p = p.lerp(target, weight)
    p = Vector((p.x*scale,p.y*scale,p.z*scale+.0863))
    if pose == 'Fold':
        p = Vector((0,.08,.04)) + Matrix.Rotation(math.radians(-19),4,'X') @ (p-Vector((0,.08,.04)))
        p.z += .095
        # The flying source has its legs swept back; plant the same webbed feet.
        if is_leg:
            hip = Vector((side*3.3*scale,.0782,.0387))
            rest_hip = Vector((0,.08,.04)) + Matrix.Rotation(math.radians(-19),4,'X') @ (hip-Vector((0,.08,.04))) + Vector((0,0,.095))
            leg = Vector((y*scale,-x*scale,z*scale+.0863)) - hip
            p = rest_hip + Matrix.Rotation(math.radians(-77),4,'X') @ leg * .68
            if x < -25:
                p.z = .008 + (z+10.15)*.002
    return p

for v, src in zip(bird.data.vertices, original):
    v.co = point(src, 'Glide')
bird.shape_key_add(name='Basis')
for pose in ['Up','Down','Fold']:
    shape = bird.shape_key_add(name=pose)
    for i,(v, src) in enumerate(zip(shape.data, original)):
        v.co = point(src, pose, i in leg_vertices)
    shape.value = 0

output = root/'Lighthouse/Assets/Art/CoastalLife'
output.mkdir(parents=True,exist_ok=True)
tex = next(n.image for n in bird.active_material.node_tree.nodes if n.type=='TEX_IMAGE')
tex.filepath_raw = str(output/'Seagull_BaseColor.png')
tex.file_format = 'PNG'
tex.save()
bird.active_material.name='Gull plumage'
for name in ['Gull mantle','Gull tips']:
    mat=bird.active_material.copy();mat.name=name;bird.data.materials.append(mat)
for poly in bird.data.polygons:
    center=sum((original[i] for i in poly.vertices),Vector())/len(poly.vertices)
    if abs(center.y)>7 and center.z>7:
        poly.material_index=2 if abs(center.y)>32 else 1
# Unity imports the FBX submeshes in mantle / tips / plumage order (50 / 15 /
# 345 triangles), unlike Blender's slot order. ResidentGull uses that order.
bpy.context.view_layer.objects.active=bird
bird.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(output/'Seagull.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    bake_anim=False,use_mesh_modifiers=False,add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtSource/CoastalLife/Seagull.blend'))
print('EXPORTED',len(bird.data.vertices),'vertices',len(bird.data.polygons),'triangles')
