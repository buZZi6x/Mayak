"""Blender finishing pass: scanned CC0 surfaces, UVs, instrument lettering and practical lights."""
import bpy,math,json,shutil
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Lighthouse/Assets/Art/Coastal';SRC=ROOT/'ArtSource/Surfaces'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Lighthouse_Coastal.blend'))
world=bpy.data.objects['Lighthouse_Blockout'];camera=bpy.data.objects['Camera_Blockout']

def read(path):
    im=bpy.data.images.load(str(path),check_existing=False);im.colorspace_settings.name='Non-Color'
    data=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(data);data=data.reshape(im.size[1],im.size[0],4);bpy.data.images.remove(im);return data
def write(name,data):
    im=bpy.data.images.new('export_'+name,width=data.shape[1],height=data.shape[0],alpha=True)
    im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(data.ravel());im.filepath_raw=str(OUT/'Textures'/name);im.file_format='PNG';im.save();bpy.data.images.remove(im)
for mat,asset in {'Coast_Wood':'weathered_planks','Coast_Plaster':'white_plaster_rough_01','Coast_Rock':'rock_face'}.items():
    diffuse=read(SRC/(asset+'_Diffuse.png'));arm=read(SRC/(asset+'_arm.png'))
    if mat=='Coast_Rock':
        grey=diffuse[:,:,:3].mean(axis=2);diffuse[:,:,:3]=grey[:,:,None]*np.array((.83,.92,1.0))
    if mat=='Coast_Wood':diffuse[:,:,:3]*=np.array((.88,.83,.75))
    write(mat+'_Base.png',diffuse);shutil.copyfile(SRC/(asset+'_nor_gl.png'),OUT/'Textures'/(mat+'_Normal.png'))
    mask=np.ones_like(arm);mask[:,:,0]=arm[:,:,2];mask[:,:,1]=arm[:,:,0];mask[:,:,3]=1-arm[:,:,1];write(mat+'_Mask.png',mask)

# Cloth should read as cloth at arm's length, without a high-frequency checkerboard.
for mat,color in {'Coast_Cloth':(.09,.25,.42),'Coast_Linen':(.75,.72,.64)}.items():
    a=read(OUT/'Textures'/(mat+'_Base.png'));g=a[:,:,:3].mean(axis=2);g=(g-g.mean())*.12
    a[:,:,:3]=np.array(color)[None,None,:]+g[:,:,None];write(mat+'_Base.png',a)
    n=read(OUT/'Textures'/(mat+'_Normal.png'));n[:,:,:2]=.5+(n[:,:,:2]-.5)*.10;n[:,:,2]=.998;write(mat+'_Normal.png',n)

def cube(name,loc,size,mat,parent=world):
    bpy.ops.mesh.primitive_cube_add(size=1);o=bpy.context.object;o.name=name;o.location=loc;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.parent=parent;o.data.materials.append(bpy.data.materials[mat]);return o
def cyl(name,loc,r,h,mat,parent=world):
    bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=r,depth=h);o=bpy.context.object;o.name=name;o.location=loc;o.parent=parent;o.data.materials.append(bpy.data.materials[mat])
    for p in o.data.polygons:p.use_smooth=abs(p.normal.z)<.5
    return o
def bar(name,a,b,r,mat,parent=world):
    a,b=Vector(a),Vector(b);o=cyl(name,(a+b)/2,r,(a-b).length,mat,parent);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

if not bpy.context.scene.get('coastal_polish_v1'):
    radio=bpy.data.objects['STATION_Radio']
    # The merged brass lettering was above the case. Bring it onto its lower front panel.
    for o in radio.children:
        if o.type!='MESH' or not o.name.startswith('VIS_'):continue
        if o.data.materials[0].name=='Coast_Brass':
            to=radio.matrix_world.inverted()@o.matrix_world;back=to.inverted()
            for v in o.data.vertices:
                p=to@v.co
                if p.z>1.57 and p.y<-.25:p.x*=.62;p.z=1.01+(p.z-1.6)*.62;p.y=-.322;v.co=back@p
        if o.data.materials[0].name=='Coast_Linen':o.data.materials[0]=bpy.data.materials['Coast_Paper']
    bpy.data.objects['Pillow'].data.materials[0]=bpy.data.materials['Coast_Linen']
    flame=bpy.data.objects['ANIM_Flame']
    for v in flame.data.vertices:v.co.x*=.32;v.co.y*=.32;v.co.z*=.72
    for p in flame.data.polygons:p.use_smooth=True
    bpy.data.objects['SOLID_RoofRim'].data.materials[0]=bpy.data.materials['Coast_Wood']
    core=bpy.data.objects['SOLID_CoreColumn']
    for p in core.data.polygons:p.use_smooth=abs(p.normal.z)<.2
    for i in range(12):
        a=i*math.tau/12;bar('VIS_Ceiling_Beam',(.85*math.cos(a),.85*math.sin(a),12.57),(5.45*math.cos(a),5.45*math.sin(a),12.57),.065,'Coast_Iron')
    cyl('VIS_DeskLamp_Base',(-.73,.22,.99),.10,.04,'Coast_Brass',radio)
    bar('VIS_DeskLamp_Stem',(-.73,.22,1.0),(-.73,.22,1.62),.018,'Coast_Brass',radio)
    bar('VIS_DeskLamp_Arm',(-.73,.22,1.62),(-.66,-.16,1.72),.018,'Coast_Brass',radio)
    bpy.ops.mesh.primitive_cone_add(vertices=48,radius1=.14,radius2=.045,depth=.13);shade=bpy.context.object;shade.name='VIS_DeskLamp_Shade';shade.parent=radio;shade.location=(-.66,-.16,1.7);shade.data.materials.append(bpy.data.materials['Coast_Teal'])
    cyl('VIS_DeskLamp_Diffuser',(-.66,-.16,1.63),.115,.008,'Coast_Paper',radio)
    marker=bpy.data.objects.new('MARK_RadioTaskLight',None);bpy.context.collection.objects.link(marker);marker.parent=radio;marker.location=(-.66,-.16,1.60)
    cube('VIS_Entry_Lantern_Back',(-1.72,-3.67,6.53),(.22,.10,.55),'Coast_Iron')
    for x in (-1.83,-1.61):
        for y in (-3.9,-3.68):bar('VIS_Entry_Lantern_Cage',(x,y,6.34),(x,y,6.73),.012,'Coast_Brass')
    cube('VIS_Entry_Lantern_Glass',(-1.72,-3.80,6.53),(.18,.20,.35),'Glass')
    for z in (6.32,6.75):cube('VIS_Entry_Lantern_Rim',(-1.72,-3.79,z),(.28,.28,.05),'Coast_Iron')
    marker=bpy.data.objects.new('MARK_EntryTaskLight',None);bpy.context.collection.objects.link(marker);marker.parent=world;marker.location=(-1.72,-3.98,6.53)
    bpy.context.scene['coastal_polish_v1']=True

if not bpy.context.scene.get('coastal_polish_v2'):
    radio=bpy.data.objects['STATION_Radio']
    for name in ('VIS_DeskLamp_Shade','VIS_DeskLamp_Diffuser','MARK_RadioTaskLight'):bpy.data.objects[name].location.y-=.30
    bpy.data.objects.remove(bpy.data.objects['VIS_DeskLamp_Arm'],do_unlink=True)
    bar('VIS_DeskLamp_Arm',(-.73,.22,1.62),(-.66,-.46,1.72),.018,'Coast_Brass',radio)
    bpy.data.objects['SOLID_LampPlatform'].data.materials[0]=bpy.data.materials['Coast_Masonry']
    bpy.context.scene['coastal_polish_v2']=True

if not bpy.context.scene.get('coastal_polish_v3'):
    # Remove the old coplanar column cap / bridge / lamp base overlap.
    bpy.data.objects['SOLID_LampPlatform'].location.z+=.028
    for name in ('TelescopeTube','TelescopeLens','LensCore','LampFuelTank','LampPedestal','ANIM_RadioDial_-0.45','ANIM_RadioDial_0.45','DoorHandle'):
        o=bpy.data.objects[name];mat=o.data.materials[0]
        radius=max(math.hypot(v.co.x,v.co.y) for v in o.data.vertices);depth=max(v.co.z for v in o.data.vertices)-min(v.co.z for v in o.data.vertices)
        bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=radius,depth=depth)
        temporary=bpy.context.object;o.data=temporary.data;bpy.data.objects.remove(temporary,do_unlink=True);o.data.materials.append(mat)
        for p in o.data.polygons:p.use_smooth=abs(p.normal.z)<.5
        bpy.context.view_layer.objects.active=o
        mod=o.modifiers.new('Machined edge bevel','BEVEL');mod.width=.004;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Weighted machined normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    for o in world.children_recursive:
        if o.type=='MESH' and o.name.startswith(('SOLID_UpperRail','SOLID_UpperPost','SOLID_WindowPost','LensRib','TelescopeTripod','RadioAntenna')):
            for p in o.data.polygons:p.use_smooth=abs(p.normal.z)<.5
    bpy.context.scene['coastal_polish_v3']=True

# Continuous cylinder UVs remove the quadrant seams in the inside/outside plaster.
bpy.context.view_layer.update()
for o in world.children_recursive:
    if o.type!='MESH' or o.name.startswith('COLL_'):continue
    if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
    if o.data.materials and o.data.materials[0].name=='Coast_Plaster':
        uv=o.data.uv_layers.active.data
        for poly in o.data.polygons:
            coords=[o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co for li in poly.loop_indices]
            angles=[math.atan2(v.y,v.x) for v in coords]
            if max(angles)-min(angles)>math.pi:angles=[a+math.tau if a<0 else a for a in angles]
            for li,p,a in zip(poly.loop_indices,coords,angles):uv[li].uv=(a*math.hypot(p.x,p.y)*.5,p.z*.5)
    elif o.name.startswith('VIS_Detail_Coast_Wood'):
        uv=o.data.uv_layers.active.data
        for poly in o.data.polygons:
            for li in poly.loop_indices:
                v=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co
                if abs(v.z-9.84)<.07 or abs(v.z-4.44)<.07:
                    row=math.floor((v.y+5.55)/.25);across=((v.y+5.55)/.25)-row
                    uv[li].uv=(.18+(row%7)*.091+across*.065,v.x*.4+row*.131)
    elif o.name.startswith(('VIS_DeskLamp','VIS_Entry_Lantern','VIS_Ceiling_Beam')):
        uv=o.data.uv_layers.active.data
        for poly in o.data.polygons:
            axis=max(range(3),key=lambda i:abs(poly.normal[i]));axes=[i for i in range(3) if i!=axis]
            for li in poly.loop_indices:
                v=o.data.vertices[o.data.loops[li].vertex_index].co;uv[li].uv=(v[axes[0]]*.5,v[axes[1]]*.5)

for name in [m.name for m in bpy.data.materials if m.name.startswith('Coast_')]:
    mat=bpy.data.materials[name];nodes=mat.node_tree.nodes;links=mat.node_tree.links
    p=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    for socket,suffix in (('Base Color','Base'),):
        tex=p.inputs[socket].links[0].from_node;tex.image=bpy.data.images.load(str(OUT/'Textures'/(name+'_'+suffix+'.png')),check_existing=False);tex.image.colorspace_settings.name='sRGB'
    normal=p.inputs['Normal'].links[0].from_node;tex=normal.inputs['Color'].links[0].from_node
    tex.image=bpy.data.images.load(str(OUT/'Textures'/(name+'_Normal.png')),check_existing=False);tex.image.colorspace_settings.name='Non-Color'
    mask=nodes.get('HDRP packed mask') or nodes.new('ShaderNodeTexImage');mask.name='HDRP packed mask';mask.image=bpy.data.images.load(str(OUT/'Textures'/(name+'_Mask.png')),check_existing=False);mask.image.colorspace_settings.name='Non-Color'
    separate=nodes.new('ShaderNodeSeparateColor');links.new(mask.outputs['Color'],separate.inputs[0]);links.new(separate.outputs[0],p.inputs['Metallic'])
    invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1;links.new(mask.outputs['Alpha'],invert.inputs[1]);links.new(invert.outputs[0],p.inputs['Roughness'])
camera.location=(0,0,0)
for root,name in ((world,'LighthouseEnvironment.fbx'),(camera,'Camera.fbx')):
    bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
    for o in root.children_recursive:o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/name),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',path_mode='AUTO')
camera.location=(0,-18,5);bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Lighthouse_Coastal.blend'))
tris=0
for o in world.children_recursive:
    if o.type=='MESH':o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles)
manifest=json.loads((ROOT/'ArtSource/coastal-art-manifest.json').read_text());manifest.update(triangles=tris,environment_meshes=len([o for o in world.children_recursive if o.type=='MESH']),packed_textures=True,scanned_surfaces=['weathered_planks','white_plaster_rough_01','rock_face'],polish='In-world instrument lettering, cloth, continuous plaster UVs, practical fixtures')
(ROOT/'ArtSource/coastal-art-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');print('POLISH_COMPLETE',flush=True)
