"""Original one-off girl sculpt: Blender -Y front, Z up, metres.
One mesh, five materials, Basis / Seated / Attack shape keys.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'ArtSource/WeepingGirl'
OUTPUT=ROOT/'Lighthouse/Assets/Art/WeepingGirl/Models'
OUTPUT.mkdir(parents=True,exist_ok=True)
SOURCE.mkdir(parents=True,exist_ok=True)
random.seed(261114)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
MATERIALS={}
for name,color,roughness in [
 ('Girl_Cloth',(.49,.55,.565,1),.93),('Girl_Hair',(.009,.015,.017,1),.79),
 ('Girl_Skin',(.44,.49,.48,1),.88),('Girl_Dark',(.0035,.0055,.006,1),1),
 ('Girl_Eyes',(.38,.49,.48,1),.31)]:
 m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=color;bs.inputs['Roughness'].default_value=roughness
 MATERIALS[name]=m
MAT_INDEX={n:i for i,n in enumerate(MATERIALS)}
vertices=[[],[],[]];faces=[];face_mats=[]
def V(x,f,z):return Vector((x,-f,z))
def add_vertex(positions):
 index=len(vertices[0])
 for pose in range(3):vertices[pose].append(tuple(positions[pose]))
 return index
def add_face(vs,material):faces.append(tuple(vs));face_mats.append(MAT_INDEX[material])
def grid_mesh(rings,sides,position,material,caps=False,wrap=True):
 start=len(vertices[0])
 for j in range(rings):
  for i in range(sides):
   a=2*math.pi*i/sides if wrap else i/(sides-1)
   add_vertex([position(p,j/(rings-1),a) for p in range(3)])
 for j in range(rings-1):
  for i in range(sides if wrap else sides-1):
   n=(i+1)%sides;a=start+j*sides+i;b=start+j*sides+n
   add_face((a,b,b+sides,a+sides),material)
 if caps:
  add_face(tuple(start+i for i in reversed(range(sides))),material)
  add_face(tuple(start+(rings-1)*sides+i for i in range(sides)),material)
def gaussian(x,z,cx,cz,sx,sz):return math.exp(-((x-cx)/sx)**2-((z-cz)/sz)**2)
def mix(a,b,t):return a*(1-t)+b*t
def catmull(points,t):
 n=len(points)-1;u=max(0,min(.999999,t))*n;k=int(u)
 a,b,c,d=[points[max(0,min(n,i))] for i in (k-1,k,k+1,k+2)];u-=k
 return .5*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u)
head_centres=[V(0,.024,1.219),V(0,.252,.915),V(0,.283,1.179)]
head_tilts=[math.radians(6),math.radians(44),math.radians(-4)]
def head_point(pose,x,f,z):
 a=head_tilts[pose]
 return head_centres[pose]+V(x,f*math.cos(a)+z*math.sin(a),-f*math.sin(a)+z*math.cos(a))
def face_forward(x,z,pose):
 radial=max(.015,1-(x/.091)**2-(z/.132)**2);f=.083*math.sqrt(radial)
 f-=.018*gaussian(x,z,.037,.024,.027,.018)+.018*gaussian(x,z,-.037,.024,.027,.018)
 f+=.012*gaussian(x,z,.051,-.014,.026,.022)+.012*gaussian(x,z,-.051,-.014,.026,.022)
 f+=.030*gaussian(x,z,0,-.005,.012,.016)+.016*gaussian(x,z,0,.022,.010,.038)
 f+=.008*gaussian(x,z,.036,.050,.031,.008)+.008*gaussian(x,z,-.036,.050,.031,.008)
 f+=.006*gaussian(x,z,0,-.102,.030,.015)
 f-=.018*gaussian(x,z,0,-.062,.015 if pose!=2 else .030,.026 if pose!=2 else .043)
 return f
def head_surface(pose,t,a):
 phi=.012+t*(math.pi-.024);z=.132*math.cos(phi)
 jaw=.82+.18*min(1,(z+.105)/.09)
 x=.091*math.sin(phi)*math.sin(a)*max(.76,jaw);f=.083*math.sin(phi)*math.cos(a)
 if math.cos(a)>0:f=mix(f,face_forward(x,z,pose),min(1,math.cos(a)/.27))
 return head_point(pose,x,f,z)
grid_mesh(74,108,head_surface,'Girl_Skin',caps=True)
# Recessed eyes, lid rims, irregular tear tracks and tiny pale reflections.
for side in (-1,1):
 def socket(p,t,a,side=side):
  r=.03+.97*t;x=side*.037+math.cos(a)*.022*r;z=.025+math.sin(a)*.0115*r-.003*math.cos(a)*side
  return head_point(p,x,face_forward(x,z,p)+.0007+.0015*(1-r),z)
 grid_mesh(9,42,socket,'Girl_Dark')
 def lid(p,t,a,side=side):
  theta=t*math.pi;x=side*.037+math.cos(theta)*.024;z=.026+math.sin(theta)*.013
  return head_point(p,x,face_forward(x,z,p)+.002+math.cos(a)*.0017,z+math.sin(a)*.0017)
 grid_mesh(20,7,lid,'Girl_Skin',caps=True)
 for trail in range(3):
  def tears(p,t,a,side=side,trail=trail):
   x=side*(.026+trail*.010)+math.sin(t*4+trail)*.0015+(a-.5)*(.0023*(1-t)+.0003);z=.016-t*(.039+trail*.009)
   return head_point(p,x,face_forward(x,z,p)+.0009,z)
  grid_mesh(16,3,tears,'Girl_Dark',wrap=False)
 def eye_glint(p,t,a,side=side):
  phi=t*math.pi;x=side*.037+math.sin(phi)*math.cos(a)*.0033;z=.026+math.cos(phi)*.0027
  return head_point(p,x,face_forward(side*.037,.026,p)+.007+math.sin(phi)*math.sin(a)*.0020,z)
 grid_mesh(8,12,eye_glint,'Girl_Eyes',caps=True)
def mouth(p,t,a):
 r=.015+.985*t;x=math.cos(a)*(.0105 if p!=2 else .026)*r;z=-.062+math.sin(a)*(.021 if p!=2 else .043)*r
 return head_point(p,x,face_forward(x,z,p)+.0011+.001*(1-r),z)
grid_mesh(14,52,mouth,'Girl_Dark')
def lip(p,t,a):
 theta=t*math.pi*2;x=math.cos(theta)*(.0115 if p!=2 else .0275);z=-.062+math.sin(theta)*(.022 if p!=2 else .0445)
 return head_point(p,x+math.cos(theta)*math.sin(a)*.0019,face_forward(x,z,p)+.0018+math.cos(a)*.0019,z+math.sin(theta)*math.sin(a)*.0019)
grid_mesh(64,8,lip,'Girl_Skin')
for side in (-1,1):
 def nostril(p,t,a,side=side):
  x=side*.009+math.cos(a)*.004*t;z=-.015+math.sin(a)*.0019*t
  return head_point(p,x,face_forward(x,z,p)+.0005,z)
 grid_mesh(5,18,nostril,'Girl_Dark')
# Scalloped hair cap and individually curved locks with tapered wet ends.
def cap(p,t,a):
 phi=.008+t*(2.37-max(0,math.cos(a))**1.8*1.52)
 x=.096*math.sin(phi)*math.sin(a);f=.088*math.sin(phi)*math.cos(a);z=.137*math.cos(phi)
 groove=1+.011*math.sin(a*33+phi*.8)+.008*math.sin(a*59-phi)
 return head_point(p,x*groove,f*groove,z)
grid_mesh(35,96,cap,'Girl_Hair',caps=True)
for lock in range(58):
 a=(lock/58)*math.pi*2;long=math.cos(a)<.61
 length=random.uniform(.29,.45) if long else random.uniform(.047,.067)
 width=random.uniform(.007,.016) if long else random.uniform(.005,.010);seed=random.uniform(0,6)
 def strand(p,t,theta,a=a,length=length,width=width,seed=seed,long=long):
  root_phi=.14 if long else .08;curve=root_phi+t*1.37
  x=.096*math.sin(curve)*math.sin(a)+math.sin(t*4+seed)*.009*t
  f=.091*math.sin(curve)*math.cos(a)+math.sin(t*3+seed*.7)*.013*t+t*t*.025
  z=.139*math.cos(root_phi)-t*length
  if p==2:x+=math.sin(a)*t*t*.028;f-=t*t*.034
  r=width*(1-t)**.55*min(1,.05+t*5)+.0003
  return head_point(p,x+math.cos(theta)*r,f+math.sin(theta)*r*.36,z)
 grid_mesh(20,8,strand,'Girl_Hair',caps=True)
HEAD_VERTEX_END=len(vertices[0])
# Gown: narrow shoulders, waist, fabric pleats and irregular weathered hem.
cloth_profiles=[
 [(1.102,.015,.061,.046),(1.065,.009,.154,.071),(1.026,.010,.186,.083),(.951,.009,.161,.082),(.841,.008,.122,.068),(.728,.002,.142,.090),(.615,.008,.164,.104),(.491,.014,.190,.120),(.358,.020,.214,.142),(.205,.030,.245,.159)],
 [(.838,.179,.061,.046),(.794,.143,.154,.071),(.757,.129,.186,.083),(.690,.126,.160,.083),(.583,.110,.129,.070),(.472,.125,.161,.103),(.535,.236,.204,.149),(.548,.337,.219,.157),(.328,.405,.228,.136),(.135,.444,.242,.125)],
 [(1.085,.160,.061,.046),(1.049,.115,.154,.071),(1.001,.058,.186,.083),(.936,.022,.161,.082),(.824,-.040,.122,.068),(.710,-.083,.142,.09),(.594,-.111,.164,.104),(.473,-.112,.190,.120),(.344,-.092,.214,.142),(.201,-.066,.245,.159)]]
cloth_arrays=[[Vector(p) for p in profile] for profile in cloth_profiles]
def cloth(p,t,a):
 z,f,rx,rf=catmull(cloth_arrays[p],t)
 pleat=(.0038+.010*t)*math.sin(a*18+.30*math.sin(t*9))+.0022*math.sin(a*37-t*10)+.0015*math.cos(a*9+t*19)
 fold=.0028*math.sin(t*55+a*5)*math.sin(math.pi*t)
 x=(rx+pleat+fold)*math.sin(a);f+=(rf+pleat*.65+fold)*math.cos(a)
 hem=max(0,(t-.84)/.16);z+=hem*hem*(.012*math.sin(a*7)+.009*math.sin(a*13+1.3)+.005*math.sin(a*29))
 return V(x,f,z)
grid_mesh(106,112,cloth,'Girl_Cloth')
neck_pairs=[(V(0,.016,1.085),V(0,.023,1.168)),(V(0,.173,.817),V(0,.213,.875)),(V(0,.145,1.065),V(0,.240,1.126))]
def tube_from_pairs(pairs,radius,material,rings=18,sides=16,depth=1):
 def point(p,t,a):
  start,end=pairs[p];direction=(end-start).normalized();axis=direction.cross(Vector((1,0,0))).normalized();right=axis.cross(direction).normalized();r=radius(t)
  return start.lerp(end,t)+right*math.cos(a)*r+axis*math.sin(a)*r*depth
 grid_mesh(rings,sides,point,material,caps=True)
tube_from_pairs(neck_pairs,lambda t:.034-.002*math.sin(t*math.pi),'Girl_Skin',18,28,.87)
for side in (-1,1):
 shoulder=[V(side*.127,.012,1.025),V(side*.127,.130,.755),V(side*.127,.064,1.005)]
 elbow=[V(side*.212,.027,.812),V(side*.222,.397,.645),V(side*.267,.302,1.047)]
 wrist=[V(side*.199,.062,.618),V(side*.066,.358,.825),V(side*.328,.529,1.075)]
 cuff=[elbow[p].lerp(wrist[p],.93) for p in range(3)]
 arm_curves=[[shoulder[p],shoulder[p].lerp(elbow[p],.55),elbow[p],elbow[p].lerp(cuff[p],.55),cuff[p]] for p in range(3)]
 def sleeve(p,t,a):
  center=catmull(arm_curves[p],t)
  tangent=(catmull(arm_curves[p],min(.99999,t+.003))-catmull(arm_curves[p],max(0,t-.003))).normalized()
  axis=tangent.cross(Vector((1,0,0))).normalized();right=axis.cross(tangent).normalized()
  radius=mix(.057,.041,t)+.0022*math.sin(t*42+a*.7)+.002*math.sin(a*9+t*8)
  return center+right*math.cos(a)*radius+axis*math.sin(a)*radius*.93
 grid_mesh(55,30,sleeve,'Girl_Cloth',caps=True)
 tube_from_pairs([(elbow[p].lerp(wrist[p],.85),wrist[p]) for p in range(3)],lambda t:.0225,'Girl_Skin',8,18,.80)
 hand_forward=[V(0,.08,-1).normalized(),V(-side*.25,-.1,1).normalized(),V(side*.12,1,.09).normalized()]
 hand_right=[Vector((side,0,0))]*3
 hand_normal=[hand_forward[p].cross(hand_right[p]).normalized() for p in range(3)]
 hand_right=[hand_normal[p].cross(hand_forward[p]).normalized() for p in range(3)]
 def handpos(p,x,y,z):return wrist[p]+hand_right[p]*x+hand_forward[p]*y+hand_normal[p]*z
 def palm(p,t,a):
  return handpos(p,math.cos(a)*(.019+.009*math.sin(t*math.pi*.8)),t*.069,math.sin(a)*(.0115+.004*math.sin(t*math.pi)))
 grid_mesh(22,28,palm,'Girl_Skin',caps=True)
 for finger in range(4):
  offset=(finger-1.5)*.014;length=[.060,.068,.063,.047][finger]
  def digit(p,t,a,finger=finger,offset=offset,length=length):
   spread=(finger-1.5)*(.006 if p!=2 else .024)*t
   curl=.015*math.sin(t*math.pi*.75) if p!=2 else -.009*math.sin(t*math.pi)
   if p==1:curl=.010*math.sin(t*math.pi)
   r=(.0062-.0028*t)*(1+.10*math.exp(-((t-.35)/.08)**2)+.09*math.exp(-((t-.67)/.07)**2))
   return handpos(p,offset+spread+math.cos(a)*r,.061+t*length,curl+math.sin(a)*r*.91)
  grid_mesh(20,12,digit,'Girl_Skin',caps=True)
 def thumb(p,t,a):
  r=.008-.0037*t
  return handpos(p,-.021-t*(.035 if p==2 else .022)+math.cos(a)*r,.026+t*.035,.007+math.sin(t*2)*.006+math.sin(a)*r)
 grid_mesh(18,14,thumb,'Girl_Skin',caps=True)
 knees=[V(side*.092,.008,.363),V(side*.145,.350,.545),V(side*.110,-.038,.357)]
 ankles=[V(side*.092,.010,.070),V(side*.151,.397,.073),V(side*.110,-.018,.071)]
 tube_from_pairs(list(zip(knees,ankles)),lambda t:.029-.011*t+.004*math.sin(t*math.pi),'Girl_Skin',26,24,.95)
 def foot(p,t,a):
  rx=.027*math.sin(math.pi*(.09+.82*t))+.005;z=-.033+math.sin(math.pi*t)*.006
  return ankles[p]+V(math.cos(a)*rx,-.025+t*.15,z+math.sin(a)*(.037-.020*t))
 grid_mesh(26,28,foot,'Girl_Skin',caps=True)
mesh=bpy.data.meshes.new('WeepingGirl sculpted surfaces');mesh.from_pydata(vertices[0],[],faces);mesh.update()
girl=bpy.data.objects.new('WeepingGirl',mesh);bpy.context.collection.objects.link(girl)
for m in MATERIALS.values():mesh.materials.append(m)
for poly,idx in zip(mesh.polygons,face_mats):poly.material_index=idx;poly.use_smooth=True
bpy.context.view_layer.objects.active=girl;girl.select_set(True)
for pose,name in enumerate(('Basis','Seated','Attack')):
 key=girl.shape_key_add(name=name,from_mix=False)
 if pose:
  for i,p in enumerate(vertices[pose]):key.data[i].co=p
  key.slider_min=0;key.slider_max=1
lookup=girl.shape_key_add(name='LookUp',from_mix=False)
neck_pivot=V(0,.016,1.119)
lookup_rotation=Matrix.Rotation(math.radians(-32),4,'X')
for i,p in enumerate(vertices[0]):
 lookup.data[i].co=neck_pivot+lookup_rotation@(Vector(p)-neck_pivot) if i<HEAD_VERTEX_END else p
lookup.slider_min=0;lookup.slider_max=1
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
root=bpy.data.objects.new('WeepingGirlRoot',None);bpy.context.collection.objects.link(root);girl.parent=root
for name,loc in [('FaceForward',(0,-1,1)),('FootOrigin',(0,0,0)),('Crown',(0,0,1.36))]:
 ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob);ob.parent=root;ob.location=loc;ob.empty_display_size=.05
girl['front_axis']='Blender -Y; intended FBX Unity +Z'
girl['pose_contract']='Basis standing; Seated/Attack 0..100; LookUp 0..100 pitches head +32deg only. Zero LookUp during Seated/Attack.'
girl['authoring']='Original procedural sculpt; no third-party characters or textures.'
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('Coastal darkness');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.022,.030,.040,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;bpy.context.scene.world=world
for name,pos,color,power,size in [('Cold overcast',(-2,-3,4),(.65,.79,1),220,3),('Rock bounce',(2,-2,1.4),(.80,.86,.84),75,2),('Moon rim',(1,2,3),(.39,.62,.73),350,2)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
 ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob);ob.location=pos;aim(ob,(0,0,.7))
camera_data=bpy.data.cameras.new('Asset Review Camera');camera=bpy.data.objects.new('Asset Review Camera',camera_data);bpy.context.collection.objects.link(camera)
camera.location=(.85,-2.55,1.1);camera_data.lens=70;aim(camera,(0,0,.76))
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
for ob in root.children_recursive:ob.select_set(True)
bpy.context.view_layer.objects.active=girl
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'WeepingGirl.fbx'),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,apply_unit_scale=True,bake_space_transform=True,bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type='FACE',path_mode='AUTO')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'WeepingGirl.blend'))
for name,pose,campos,target,lens in [('standing',0,(.85,-2.55,1.1),(0,0,.76),70),('seated',1,(1.20,-2.50,1.0),(0,-.19,.59),75),('attack',2,(.02,-1.05,1.24),(0,-.24,1.10),55),('face',0,(.10,-.67,1.255),(0,-.04,1.23),80),('lookup',3,(.30,-1.2,1.65),(0,-.04,1.22),80)]:
 girl.data.shape_keys.key_blocks['Seated'].value=1 if pose==1 else 0;girl.data.shape_keys.key_blocks['Attack'].value=1 if pose==2 else 0
 lookup.value=1 if pose==3 else 0
 camera.location=campos;camera_data.lens=lens;aim(camera,target);scene.render.filepath=str(SOURCE/(name+'.png'));bpy.ops.render.render(write_still=True)
girl.data.shape_keys.key_blocks['Seated'].value=0;girl.data.shape_keys.key_blocks['Attack'].value=0;lookup.value=0
camera.location=(.85,-2.55,1.1);camera_data.lens=70;aim(camera,(0,0,.76));bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'WeepingGirl.blend'))
summary={'vertices':len(vertices[0]),'faces':len(faces),'shape_keys':['Basis','Seated','Attack','LookUp'],'lookup_degrees':32,'materials':list(MATERIALS),'source_front':'-Y','source_up':'Z','bounds':{str(p):{'min':[min(v[i] for v in vertices[p]) for i in range(3)],'max':[max(v[i] for v in vertices[p]) for i in range(3)]} for p in range(3)}}
(SOURCE/'asset-contract.json').write_text(json.dumps(summary,indent=2),encoding='utf8')
print('WEEPING_GIRL_EXPORTED',json.dumps(summary))
