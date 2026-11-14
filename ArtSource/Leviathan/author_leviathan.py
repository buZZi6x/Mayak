"""Original distant sea serpent. Unity-space modelling helpers, editable Blender source.
No downloaded model or new dependency. Run once with Blender's background Python.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Lighthouse/Assets/Art/Leviathan'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
materials = {}
for name, color in {
    'Leviathan_Skin': (.035, .12, .135, 1),
    'Leviathan_Belly': (.31, .38, .32, 1),
    'Leviathan_Ridge': (.09, .21, .23, 1),
    'Leviathan_Bone': (.53, .57, .45, 1),
    'Leviathan_Mouth': (.015, .025, .03, 1),
    'Leviathan_Glow': (.12, .8, .76, 1),
}.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None: bsdf = mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Roughness'].default_value = .37
    materials[name] = mat

def convert(p): return (p[0], -p[2], p[1])
def mesh(name, verts, faces, material, smooth=True):
    data = bpy.data.meshes.new(name)
    data.from_pydata([convert(p) for p in verts], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    data.materials.append(materials[material])
    for face in data.polygons: face.use_smooth = smooth
    return obj

def ball(name, p, size, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=16, location=convert(p))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0], size[2], size[1])
    obj.data.materials.append(materials[material])
    for face in obj.data.polygons: face.use_smooth = True
    return obj

def tube(name, points, radii, material, radial=12):
    verts, faces = [], []
    for i, point in enumerate(points):
        forward = Vector(points[min(i+1,len(points)-1)]) - Vector(points[max(0,i-1)])
        forward.normalize()
        right = forward.cross(Vector((0,1,0)))
        if right.length < .01: right = forward.cross(Vector((1,0,0)))
        right.normalize()
        up = right.cross(forward).normalized()
        for j in range(radial):
            angle = j * math.tau / radial
            verts.append(Vector(point) + (right*math.cos(angle) + up*math.sin(angle))*radii[i])
    for i in range(len(points)-1):
        for j in range(radial):
            a=i*radial+j; b=i*radial+(j+1)%radial
            faces.append((a,b,b+radial,a+radial))
    faces.append(tuple(reversed(range(radial))))
    faces.append(tuple((len(points)-1)*radial+j for j in range(radial)))
    return mesh(name,verts,faces,material)

# A continuous closed hull, with a broad ribcage tapering into an eel tail.
profile=[(18,1.5,1.5),(12,5.4,4.8),(4,8,6.3),(-9,7,7),(-27,9.5,9),
         (-46,8.5,8),(-68,6.9,6.9),(-92,5.3,5.7),(-120,3,3.5),(-144,1.3,1.8),(-156,.15,.3)]
verts, faces, face_materials = [], [], []
rows, radial=121,40
for i in range(rows):
    z=18-i*174/(rows-1)
    for a,b in zip(profile,profile[1:]):
        if b[0] <= z <= a[0]:
            t=(a[0]-z)/(a[0]-b[0]); t=t*t*(3-2*t)
            rx=a[1]*(1-t)+b[1]*t; ry=a[2]*(1-t)+b[2]*t; break
    for j in range(radial):
        angle=j*math.tau/radial
        ripple=1+.025*math.cos(z*1.1+angle*6)*math.sin(math.pi*i/(rows-1))
        verts.append((rx*math.cos(angle)*ripple,ry*math.sin(angle)*ripple,z))
for i in range(rows-1):
    for j in range(radial):
        a=i*radial+j; b=i*radial+(j+1)%radial
        faces.append((a,a+radial,b+radial,b))
        face_materials.append(1 if math.sin((j+.5)*math.tau/radial)<-.48 else 0)
faces.extend([tuple(reversed(range(radial))),tuple((rows-1)*radial+j for j in range(radial))])
body=mesh('Continuous muscular body',verts,faces,'Leviathan_Skin')
body.data.materials.append(materials['Leviathan_Belly'])
for i, material in enumerate(face_materials): body.data.polygons[i].material_index=material

# Head: open dark maw between a brow shield and two sweeping lower jaws.
ball('Deep mouth', (0,-2,12),(5.8,3.8,8),'Leviathan_Mouth')
ball('Heavy brow shield',(0,3.8,8),(8.5,3.1,10.5),'Leviathan_Ridge')
for side in (-1,1):
    tube('Swept lower mandible',[(side*6,-1,1),(side*9,-5,8),(side*7.8,-6.8,17),(side*3.5,-5.4,23)],
         [3.1,2.2,1.25,.12],'Leviathan_Bone',16)
    tube('Backward crown horn',[(side*5,6,1),(side*9,11,-6),(side*12,14,-16),(side*12.5,13,-24)],
         [2.5,1.7,.8,.04],'Leviathan_Bone',16)
    ball('Eye socket',(side*7.3,2.2,9),(1.4,1.3,2),'Leviathan_Mouth')
    ball('Cold eye',(side*8.1,2.4,9.7),(.65,.7,1.1),'Leviathan_Glow')
    for i in range(6):
        z=7+i*2.1
        tube('Interlocking fang',[(side*(5.7-i*.35),.2,z),(side*(5.5-i*.35),-3.7,z+1.3)],
             [.65,.035],'Leviathan_Bone',8)
    for i in range(4):
        z=-9-i*3.8
        tube('Recessed gill',[(side*6.4,4.7,z),(side*7.7,1.8,z-1),(side*6.9,-3.2,z-1.5)],
             [.5,.65,.3],'Leviathan_Mouth',8)

def fin(name, points, thickness, material):
    # Closed, bevel-like membrane, not a flat single-sided triangle.
    center=sum((Vector(p) for p in points),Vector())/len(points)
    verts=points+[tuple(center+Vector((0,thickness,0))),tuple(center-Vector((0,thickness,0)))]
    n=len(points); faces=[]
    for i in range(n): faces.extend([(i,(i+1)%n,n),((i+1)%n,i,n+1)])
    return mesh(name,verts,faces,material)

for side in (-1,1):
    fin('Swept pectoral flipper',[(side*6,2,-19),(side*18,1,-25),(side*37,-2,-52),
        (side*24,-4,-48),(side*8,-3,-37)],1.5,'Leviathan_Ridge')
    tube('Flipper leading rib',[(side*6,2,-19),(side*18,1,-25),(side*37,-2,-52)],
         [1.9,1.25,.04],'Leviathan_Bone',10)
    fin('Rear swimming fin',[(side*4,1,-91),(side*18,-1,-107),(side*9,-2,-112),(side*3,-1,-105)],
        .75,'Leviathan_Ridge')
    fin('Lunate tail blade',[(side*.7,0,-144),(side*15,1,-152),(side*26,3,-169),
        (side*13,-1,-165),(0,0,-156)],.75,'Leviathan_Ridge')

# Unequal dorsal sails and ribbed armour make the horizon silhouette identifiable.
for i in range(15):
    z=-8-i*8.7
    height=12*(1-i/19)
    r=7.5*(1-i/18)
    fin('Dorsal sail %02d'%i,[(-.65,r,z),(0,r+height,z-6),(0,r+height*.45,z-10),(.65,r,z-9)],
        .65,'Leviathan_Ridge')
    tube('Dorsal spine',[(0,r,z),(0,r+height,z-6)],[1.0*(1-i/19),.025],'Leviathan_Bone',8)
    for side in (-1,1):
        tube('Lateral luminous pore',[(side*r*.9,r*.36,z),(side*r*.94,r*.3,z-1.8)],
             [.22,.12],'Leviathan_Glow',6)

for name,position in [('HeadAnchor',(0,0,0)),('TailAnchor',(0,0,-156))]:
    obj=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(obj);obj.location=convert(position)

# Recalculate outward normals for all closed anatomical pieces and author UVs.
bpy.ops.object.select_all(action='DESELECT')
for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH': continue
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=1.1519,island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT');obj.select_set(False)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('Leviathan.blend')))
bpy.ops.export_scene.fbx(filepath=str(OUT/'Leviathan.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
    axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE')
manifest={'identity':'Original abyssal sea serpent; no external model', 'source_length_metres':192,
          'runtime_scale':3,'length_metres':576,
          'spine_metres':156,'runtime':'27-bone arc-length skinning; one 23-second breach, about 306 metres above water; recorded roar and additive camera shake',
          'materials':list(materials),'vertices':sum(len(o.data.vertices) for o in bpy.context.scene.objects if o.type=='MESH')}
Path(__file__).with_name('asset-contract.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('LEVIATHAN_AUTHORING_COMPLETE',manifest)
