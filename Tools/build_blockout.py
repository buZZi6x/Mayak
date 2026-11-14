"""Create authored Blender blockout sources and Unity FBX exports (meters, Z up)."""
import bpy, math, random, os, json
from mathutils import Vector
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Lighthouse/Assets/Art/Blockout'
SOURCE = ROOT / 'ArtSource'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(1113)
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene.unit_settings.scale_length=1.0
M={}
COLORS={
 'Stone':(.30,.36,.40,1), 'PaleStone':(.63,.66,.63,1), 'Ivory':(.84,.79,.64,1),
 'Red':(.62,.17,.09,1), 'Roof':(.14,.23,.25,1), 'Iron':(.07,.12,.15,1),
 'Wood':(.37,.23,.12,1), 'Brass':(.85,.52,.12,1), 'Light':(1,.65,.19,1),
 'Blue':(.10,.30,.57,1), 'Teal':(.10,.53,.53,1), 'Glass':(.32,.66,.74,.13),
 'White':(.94,.91,.78,1), 'Kraken':(.60,.25,.12,1), 'Eye':(.96,.76,.24,1),
 'Water':(.05,.25,.33,1), 'Beam':(1,.72,.30,.04)
}
for name,color in COLORS.items():
 m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True
 nodes=m.node_tree.nodes;p=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
 if p is None:
  p=nodes.new('ShaderNodeBsdfPrincipled');output=nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(p.outputs['BSDF'],output.inputs['Surface'])
 p.inputs['Base Color'].default_value=color;p.inputs['Roughness'].default_value=.68
 M[name]=m

def empty(name,loc=(0,0,0),parent=None):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.location=loc
 if parent:o.parent=parent
 return o

def material(o,mat,parent=None):
 o.data.materials.append(M[mat])
 if parent:o.parent=parent
 return o

def cube(name,loc,size,mat='Ivory',parent=None,bevel=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Readable edges','BEVEL');mod.width=bevel;mod.segments=1
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return material(o,mat,parent)

def cyl(name,loc,radius,depth,mat='Iron',parent=None,vertices=16):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
 o=bpy.context.object;o.name=name;return material(o,mat,parent)

def ico(name,loc,scale,mat='Stone',parent=None,sub=1):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=loc)
 o=bpy.context.object;o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return material(o,mat,parent)

def mesh(name,verts,faces,mat,parent=None):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
 o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return material(o,mat,parent)

def bar(name,a,b,r=.04,mat='Iron',parent=None):
 a,b=Vector(a),Vector(b);o=cyl(name,(a+b)/2,r,(b-a).length,mat,parent,8)
 o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

def wedge(name,rin,rout,a,b,z,thick,mat,parent=None):
 vs=[(r*math.cos(t),r*math.sin(t),zz) for zz in (z-thick,z) for r,t in ((rin,a),(rout,a),(rout,b),(rin,b))]
 return mesh(name,vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,parent)

def ring(name,rin,rout,z,thick,mat,parent=None,n=48):
 vs=[];fs=[]
 for i in range(n):
  a=2*math.pi*i/n
  vs.extend([(rin*math.cos(a),rin*math.sin(a),z-thick),(rout*math.cos(a),rout*math.sin(a),z-thick),(rin*math.cos(a),rin*math.sin(a),z),(rout*math.cos(a),rout*math.sin(a),z)])
 for i in range(n):
  a=i*4;b=((i+1)%n)*4
  fs.extend([(a,a+1,b+1,b),(a+2,b+2,b+3,a+3),(a,b,b+2,a+2),(a+1,a+3,b+3,b+1)])
 return mesh(name,vs,fs,mat,parent)

def group_move(root,loc,rz=0):root.location=loc;root.rotation_euler.z=rz

world=empty('Lighthouse_Blockout')
# Flat walkable cap over an irregular faceted rock, rather than a floating plane.
verts=[];faces=[];n=32
for z,radius in [(-3,13.5),(0,16.0),(2.8,13.4),(4.0,12.2)]:
 for i in range(n):
  a=i*math.tau/n;r=radius+random.uniform(-.7,.7);verts.append((r*math.cos(a),r*math.sin(a),z+random.uniform(-.18,.18)))
for j in range(3):
 for i in range(n):
  a=j*n+i;b=j*n+(i+1)%n;faces.extend([(a,b,b+n),(a,b+n,a+n)])
faces.append(tuple(range(3*n,4*n)))
mesh('SOLID_IslandRock',verts,faces,'Stone',world)
cyl('SOLID_Terrace',(0,0,3.92),12.1,.48,'PaleStone',world,48)
for i,(x,y,s) in enumerate([(12,6,3.5),(-13,3,3),(7,13,3.2),(-8,-12,2.6),(15,-7,2.7)]):
 ico('SOLID_ShoreRock_%02d'%i,(x,y,.2),(s,s*.8,s*1.1),'Stone',world,1)
for i in range(18):
 a=i*math.tau/18
 if i in (12,13):continue
 x,y=11.8*math.cos(a),11.8*math.sin(a)
 cyl('SOLID_TerracePost_%02d'%i,(x,y,4.68),.09,1.1,'Iron',world,8)
 b=(i+1)*math.tau/18
 if i not in (11,12,13):bar('SOLID_TerraceRail_%02d'%i,(x,y,5.1),(11.8*math.cos(b),11.8*math.sin(b),5.1),.055,'Iron',world)
# Exterior walkway and readable approach.
for i in range(9):cube('SOLID_ApproachSlab_%02d'%i,(0,-10+i*.72,4.20),(1.85,.66,.10),'Ivory',world,.025)
cube('SOLID_DoorStep',(0,-3.85,4.24),(2.3,1.2,.18),'PaleStone',world,.04)
cyl('SOLID_TowerFoot',(0,0,4.38),4.0,.5,'Stone',world,24)
# Keep the ground-room walkable surface level with the step.
cyl('SOLID_GroundFloor',(0,0,4.36),3.65,.20,'Wood',world,32)
base=4.46;top=9.86
for i in range(12):
 a=i*math.tau/12
 if i==9:
  z0=base+2.65;h=top-z0
 else:z0=base;h=top-base
 o=cube('SOLID_TowerWall_%02d'%i,(3.63*math.cos(a),3.63*math.sin(a),z0+h/2),(1.98,.30,h),'Ivory',world)
 o.rotation_euler.z=a+math.pi/2
 o=cube('SOLID_RedBand_%02d'%i,(3.80*math.cos(a),3.80*math.sin(a),8.25),(2.05,.065,.60),'Red',world)
 o.rotation_euler.z=a+math.pi/2
cyl('SOLID_CoreColumn',(0,0,(base+top)/2),1.12,top-base,'Ivory',world,24)
# 30 broad, low risers. Open undersides preserve the entry route.
for i in range(30):
 a=i*math.tau/30;b=(i+1)*math.tau/30;z=base+(i+1)*.18
 wedge('SOLID_Stair_%02d'%i,1.45,3.08,a,b,z,.22,'Wood',world)
 if i%2==0:
  for r in (1.48,3.02):
   if r>3 and i>=28:continue
   bar('SOLID_StairPost_%02d_%d'%(i,int(r)),(r*math.cos(a),r*math.sin(a),z),(r*math.cos(a),r*math.sin(a),z+.88),.035,'Iron',world)
 for r in (1.48,3.02):
  if r>3 and i>=28:continue
  bar('SOLID_StairHandrail_%02d_%d'%(i,int(r)),(r*math.cos(a),r*math.sin(a),z+.88),(r*math.cos(b),r*math.sin(b),z+1.06),.035,'Brass',world)
 empty('MARK_Route_%02d'%(i+5),(2.25*math.cos((a+b)/2),2.25*math.sin((a+b)/2),z+.05),world)
ring('SOLID_WatchFloor',3.20,5.55,top,.26,'Wood',world)
wedge('SOLID_StairLanding',2.85,4.0,math.radians(344),math.radians(372),top,.18,'Wood',world)
cube('SOLID_LampBridge',(-2.2,0,top-.12),(3.0,1.35,.24),'Wood',world)
cyl('SOLID_LampPlatform',(0,0,top-.12),1.42,.24,'Wood',world,24)
ring('SOLID_Balcony',5.50,6.45,top-.06,.24,'PaleStone',world)
for i in range(24):
 a=i*math.tau/24;b=(i+1)*math.tau/24
 for r in (3.18,6.34):
  if r<4 and i in (0,11,12,23):continue
  bar('SOLID_UpperPost_%02d_%d'%(i,int(r)),(r*math.cos(a),r*math.sin(a),top),(r*math.cos(a),r*math.sin(a),top+1),.045,'Iron',world)
  bar('SOLID_UpperRail_%02d_%d'%(i,int(r)),(r*math.cos(a),r*math.sin(a),top+1),(r*math.cos(b),r*math.sin(b),top+1),.045,'Iron',world)
for i in range(12):
 a=i*math.tau/12
 cyl('SOLID_WindowPost_%02d'%i,(5.35*math.cos(a),5.35*math.sin(a),top+1.4),.09,2.8,'Roof',world,8)
 # Leave front and seaward openings for balcony access and unobstructed observation.
 if i not in (2,3,8,9):
  t=a+math.pi/12
  o=cube('GLASS_Window_%02d'%i,(5.35*math.cos(t),5.35*math.sin(t),top+1.45),(2.62,.035,2.35),'Glass',world)
  o.rotation_euler.z=t+math.pi/2
  o=cube('SOLID_WindowSill_%02d'%i,(5.35*math.cos(t),5.35*math.sin(t),top+.28),(2.75,.16,.18),'Ivory',world)
  o.rotation_euler.z=t+math.pi/2
cyl('SOLID_RoofRim',(0,0,top+2.88),5.8,.20,'Red',world,24)
bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=6.0,radius2=.7,depth=2.2,location=(0,0,top+4.0))
roof=bpy.context.object;roof.name='SOLID_Roof';material(roof,'Roof',world)
cyl('RoofFinial',(0,0,top+5.8),.1,1.4,'Brass',world,12)

# Door hinge uses a real pivot; the leaf is animated in Unity.
door=empty('STATION_Door',(-.97,-3.72,base),world)
leaf=cube('DoorLeaf',(.97,0,1.25),(1.94,.14,2.5),'Red',door,.045)
cube('DoorInset',(.97,-.09,1.35),(1.50,.05,1.65),'Wood',door,.04)
knob=cyl('DoorHandle',(1.62,-.16,1.1),.07,.14,'Brass',door,12);knob.rotation_euler.x=math.pi/2
empty('MARK_DoorLabel',(.97,-.2,2.8),door)
# Central lamp station: brass pump + rotating lens + flame.
lamp=empty('STATION_Lamp',(0,0,top),world)
cyl('LampPedestal',(0,0,.55),.65,1.1,'Iron',lamp,16)
cyl('LampFuelTank',(0,0,.95),.8,.35,'Brass',lamp,16)
lens=empty('ANIM_Lens',(0,0,1.72),lamp)
cyl('LensCore',(0,0,0),.60,1.20,'Glass',lens,24)
for z in (-.58,.58):ring('LensRing_'+str(z),.58,.72,z,.10,'Brass',lens,24)
for i in range(8):
 a=i*math.tau/8;bar('LensRib_%02d'%i,(.66*math.cos(a),.66*math.sin(a),-.6),(.66*math.cos(a),.66*math.sin(a),.6),.028,'Iron',lens)
bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=.12,radius2=4.2,depth=34,location=(0,17,0))
beam=bpy.context.object;beam.name='VIS_LightBeam';beam.rotation_euler.x=-math.pi/2;material(beam,'Beam',lens)
flame=ico('ANIM_Flame',(0,0,1.7),(.30,.30,.56),'Light',lamp,2)
pump=empty('ANIM_Pump',(-.75,0,.98),lamp)
bar('PumpArm',(0,0,0),(-.48,0,.38),.065,'Red',pump)
bar('PumpGrip',(-.48,-.20,.38),(-.48,.20,.38),.08,'Iron',pump)
empty('MARK_LampLabel',(-.85,0,2.45),lamp)

# Cyan radio desk and a clearly separate blue sleeping station.
radio=empty('STATION_Radio',(-4.1,1.30,top),world)
cube('DeskTop',(0,0,.9),(1.65,.85,.12),'Wood',radio,.04)
for x in (-.68,.68):
 for y in (-.30,.30):cube('DeskLeg',(x,y,.43),(.09,.09,.86),'Iron',radio)
cube('RadioCase',(0,0,1.25),(1.20,.52,.57),'Teal',radio,.06)
cube('RadioDisplay',(0,-.275,1.31),(.60,.025,.25),'Light',radio,.02)
for x in (-.45,.45):
 dial=cyl('ANIM_RadioDial_'+str(x),(x,-.31,1.15),.11,.06,'Brass',radio,12);dial.rotation_euler.x=math.pi/2
bar('RadioAntenna',(.5,.15,1.5),(.65,.2,2.15),.025,'Iron',radio)
paper=cube('ANIM_ReportPaper',(.55,-.42,.99),(.45,.40,.025),'White',radio,.02)
empty('MARK_RadioLabel',(0,0,2.5),radio)
bed=empty('STATION_Bed',(1.3,4.15,top),world)
cube('BedFrame',(0,0,.40),(2.25,1.05,.22),'Wood',bed,.05)
cube('Mattress',(0,0,.61),(2.12,1.0,.28),'White',bed,.08)
cube('Pillow',(-.76,0,.82),(.50,.84,.20),'Ivory',bed,.08)
cube('ANIM_Blanket',(.31,0,.81),(1.45,1.02,.14),'Blue',bed,.045)
for x in (-.93,.93):
 for y in (-.42,.42):cube('BedLeg',(x,y,.19),(.1,.1,.38),'Iron',bed)
empty('MARK_BedLabel',(0,0,1.9),bed)
# Telescope has a distinct long silhouette and a swaying bracket.
scope=empty('STATION_Telescope',(0,5.9,top),world)
for x,y in [(-.45,-.25),(.45,-.25),(0,.45)]:bar('TelescopeTripod',(x,y,0),(0,0,1.10),.045,'Iron',scope)
swivel=empty('ANIM_Telescope',(0,0,1.3),scope)
tube=cyl('TelescopeTube',(0,.18,0),.16,1.12,'Brass',swivel,16);tube.rotation_euler.x=math.pi/2
rim=cyl('TelescopeLens',(0,.76,0),.18,.09,'Blue',swivel,16);rim.rotation_euler.x=math.pi/2
empty('MARK_TelescopeLabel',(0,0,2.15),scope)

# Flag mesh: independent vertices support wind animation without an armature.
bar('SOLID_Flagpole',(7,-7,4.16),(7,-7,8.3),.065,'Iron',world)
flagverts=[];flagfaces=[]
for i in range(9):
 for j in range(3):flagverts.append((i*.24,0,j*.45))
for i in range(8):
 for j in range(2):a=i*3+j;flagfaces.append((a,a+3,a+4,a+1))
flag=mesh('ANIM_Flag',flagverts,flagfaces,'Red',world);flag.location=(7,-7,7.3)
# Wave grid is authored in Blender and displaced in the Unity shader.
bpy.ops.mesh.primitive_grid_add(x_subdivisions=100,y_subdivisions=100,size=800,location=(0,0,-.08))
o=bpy.context.object;o.name='WATER_Ocean';material(o,'Water',world)
for name,loc in [('MARK_PlayerSpawn',(0,-9.4,4.35)),('MARK_PlayerLook',(0,-3,6.1)),('MARK_Observation',(0,5.9,top+.08)),('MARK_SeaEvent',(0,85,0)),('MARK_Overview',(24,-30,20)),('MARK_Route_00',(0,-8,4.3)),('MARK_Route_01',(0,-4.6,4.5)),('MARK_Route_02',(0,-2.65,4.5)),('MARK_Route_03',(1.65,-1.75,4.5)),('MARK_Route_04',(2.24,-.30,4.5)),('MARK_Route_35',(3.65,-.30,top+.05)),('MARK_Route_36',(4.1,.3,top+.05))]:empty(name,loc,world)

# Separate reusable boat, creature and held-camera sources in the same .blend.
boat=empty('Boat_Blockout')
mesh('BoatHull',[(-1.4,-4,0),(1.4,-4,0),(1.65,2.8,0),(0,5.1,0),(-1.65,2.8,0),(-1,-3.5,-1),(1,-3.5,-1),(1.1,2.5,-1),(0,4,-1),(-1.1,2.5,-1)],[(0,1,2,3,4),(0,5,6,1),(1,6,7,2),(2,7,8,3),(3,8,9,4),(4,9,5,0),(5,9,8,7,6)],'Red',boat)
cube('BoatDeck',(0,0,.08),(2.65,6.6,.15),'Wood',boat,.04)
cube('BoatCabin',(0,-1,1),(2,2.5,1.7),'Ivory',boat,.1)
cube('BoatCabinWindow',(0,.28,1.20),(1.5,.04,.55),'Blue',boat)
cyl('BoatStack',(.45,-1.4,2.5),.26,1.3,'Iron',boat,12)
bar('BoatMast',(0,1.3,.2),(0,1.3,5.4),.07,'Iron',boat)
bar('BoatYard',(-1.5,1.3,4),(1.5,1.3,4),.05,'Iron',boat)
flagboat=mesh('ANIM_BoatFlag',flagverts,flagfaces,'White',boat);flagboat.location=(0,1.3,4.4);flagboat.scale=(.65,.65,.65)
kraken=empty('Kraken_Blockout')
ico('KrakenHead',(0,0,.6),(3.6,3.3,3.0),'Kraken',kraken,2)
for x in (-1.6,1.6):
 ico('KrakenEye',(x,-2.75,1.2),(.64,.40,.64),'Eye',kraken,2)
 ico('KrakenPupil',(x,-3.08,1.2),(.20,.11,.36),'Iron',kraken,2)
for i in range(8):
 a=i*math.tau/8;pivot=empty('ANIM_Tentacle_%02d'%i,(0,0,0),kraken)
 curve=bpy.data.curves.new('TentacleCurve','CURVE');curve.dimensions='3D';curve.resolution_u=4;curve.bevel_depth=.48;curve.bevel_resolution=1
 sp=curve.splines.new('BEZIER');sp.bezier_points.add(4)
 for j,(r,z,rad) in enumerate([(2.4,0,1),(4.1,.1,.9),(6.3,1.0,.72),(7.4,4.2,.48),(6.7,6.0,.15)]):
  b=sp.bezier_points[j];b.co=(r*math.cos(a),r*math.sin(a),z);b.radius=rad;b.handle_left_type='AUTO';b.handle_right_type='AUTO'
 ob=bpy.data.objects.new('Tentacle_%02d'%i,curve);bpy.context.collection.objects.link(ob);ob.parent=pivot;ob.data.materials.append(M['Kraken'])
 bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH');ob.select_set(False)
camera=empty('Camera_Blockout')
cube('CameraBody',(0,0,0),(.35,.16,.23),'Iron',camera,.025)
cube('CameraGrip',(.19,0,-.025),(.11,.18,.26),'Wood',camera,.02)
l=cyl('CameraLens',(0,-.14,0),.095,.17,'Brass',camera,20);l.rotation_euler.x=math.pi/2
l=cyl('CameraGlass',(0,-.23,0),.079,.02,'Blue',camera,20);l.rotation_euler.x=math.pi/2
cube('CameraViewfinder',(0,.01,.16),(.14,.08,.09),'Iron',camera,.015)
cyl('ANIM_Shutter',(.13,0,.14),.035,.035,'Red',camera,12)
for x in (-.23,.25):cube('CameraHand',(x,.045,-.08),(.1,.17,.11),'Ivory',camera,.025)

# Keep visible sources separated in Blender; export each at its own local origin.
def export(root,name):
 bpy.ops.object.select_all(action='DESELECT')
 def select_tree(o):
  o.select_set(True)
  for c in o.children:select_tree(c)
 select_tree(root);bpy.context.view_layer.objects.active=root
 bpy.context.view_layer.update()
 bpy.ops.export_scene.fbx(filepath=str(OUT/name),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',bake_space_transform=False,add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',path_mode='AUTO')
export(world,'LighthouseEnvironment.fbx')
export(boat,'Boat.fbx')
export(kraken,'Kraken.fbx')
export(camera,'Camera.fbx')
boat.location=(26,0,4);kraken.location=(-26,0,4);camera.location=(0,-18,5)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Lighthouse_Blockout.blend'))
summary={'source':'Blender 5.1','units':'meters','environment_objects':len(world.children_recursive),'mesh_objects':len([o for o in bpy.data.objects if o.type=='MESH']),'exports':[p.name for p in OUT.glob('*.fbx')],'stair_riser':.18,'stair_width':1.63,'watch_floor':top,'workstations':['lamp','radio','bed','telescope','door']}
(SOURCE/'blockout-manifest.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print('BLOCKOUT_EXPORT_COMPLETE '+json.dumps(summary))
