"""Three original, rigged coastal vessels; Blender source and local FBX exports."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[1];O=R/'Lighthouse/Assets/Art/Fleet';O.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC'
palette={'HullNavy':(.035,.10,.14,1),'HullRed':(.38,.055,.025,1),'HullTeal':(.04,.24,.23,1),'Deck':(.34,.19,.08,1),'Sail':(.82,.72,.48,1),'Brass':(.65,.4,.1,1),'Iron':(.045,.055,.06,1),'Ivory':(.73,.69,.54,1),'Glass':(.08,.27,.32,1),'Light':(1,.65,.18,1)}
M={}
for n,c in palette.items():
 m=bpy.data.materials.new('Fleet_'+n);m.diffuse_color=c;m.use_nodes=True;p=next(x for x in m.node_tree.nodes if x.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=c;p.inputs['Roughness'].default_value=.55;M[n]=m
def mesh(n,vs,fs,mat,par):
 me=bpy.data.meshes.new(n);me.from_pydata(vs,[],fs);me.update();ob=bpy.data.objects.new(n,me);bpy.context.collection.objects.link(ob);ob.parent=par;ob.data.materials.append(M[mat]);return ob
def empty(n,par=None):
 o=bpy.data.objects.new(n,None);bpy.context.collection.objects.link(o);o.parent=par;return o
def box(n,p,s,mat,par,bevel=.035):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=n;o.dimensions=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.parent=par;o.data.materials.append(M[mat]);m=o.modifiers.new('Rounded edges','BEVEL');m.width=bevel;m.segments=2;o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o
def bar(n,a,b,r,mat,par):
 a,b=Vector(a),Vector(b);bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=(b-a).length,location=(a+b)/2);o=bpy.context.object;o.name=n;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();o.parent=par;o.data.materials.append(M[mat]);return o
def sphere(n,p,s,mat,par):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,location=p);o=bpy.context.object;o.name=n;o.scale=s;o.parent=par;o.data.materials.append(M[mat]);return o
def hull(par,L,W,mat):
 vs=[];fs=[];N=36
 for i in range(N+1):
  q=i/N;y=(q-.5)*L;w=W*.5*pow(max(.015,math.sin(q*math.pi)),.55)*(1-.32*q);rise=.65*pow(abs(q*2-1),3)
  for x,z in [(-w,1.5+rise),(-w*.94,.4),(-w*.5,-.8),(0,-1.15),(w*.5,-.8),(w*.94,.4),(w,1.5+rise)]:vs.append((x,y,z))
 for i in range(N):
  for j in range(6):a=i*7+j;fs.append((a,a+7,a+8,a+1))
 fs.extend([(0,1,2,3,4,5,6),tuple(N*7+j for j in reversed(range(7)))])
 ob=mesh('Clinker hull',vs,fs,mat,par)
 for p in ob.data.polygons:p.use_smooth=True
 # Planked deck with curved edge and bright rubbing strakes.
 for i in range(N):
  a=i*7;b=(i+1)*7
  mesh('Deck plank %02d'%i,[vs[a],vs[a+6],vs[b+6],vs[b]],[(0,1,2,3)],'Deck',par)
  for side in [0,6]:
   p=Vector(vs[a+side]);q=Vector(vs[b+side]);bar('Ivory sheer rail',p,q,.065,'Ivory',par)
   if i%3==0:
    bar('Rail stanchion',p,p+Vector((0,0,.65)),.035,'Iron',par)
   bar('Upper rail',p+Vector((0,0,.65)),q+Vector((0,0,.65)),.035,'Iron',par)
 for z in [.4,.85]:
  for side in [-1,1]:
   for i in range(N):
    a=Vector(vs[i*7+(0 if side<0 else 6)]);b=Vector(vs[(i+1)*7+(0 if side<0 else 6)]);a.z-=z;b.z-=z;bar('Hull strake',a,b,.026,'Brass',par)
def sail(par,y,z,w,h):
 rig=empty('ANIM_Sail',par);vs=[];fs=[];N=16
 for i in range(N+1):
  u=i/N
  for j in range(N+1):
   v=j/N;vs.append(((u-.5)*w*(1-.28*v),y+math.sin(u*math.pi)*math.sin(v*math.pi)*1.1,z+v*h))
 for i in range(N):
  for j in range(N):a=i*(N+1)+j;fs.append((a,a+N+1,a+N+2,a+1))
 o=mesh('Canvas billow',vs,fs,'Sail',rig)
 for p in o.data.polygons:p.use_smooth=True
 # Visible stitched seams, foot and leech ropes.
 for i in range(0,N+1,4):
  for j in range(N):bar('Canvas seam',vs[i*(N+1)+j],vs[i*(N+1)+j+1],.012,'Ivory',rig)
 for j in [0,N]:
  for i in range(N):bar('Sail edge',vs[i*(N+1)+j],vs[(i+1)*(N+1)+j],.025,'Deck',rig)
 bar('Yard',(-w*.55,y,z+h),(w*.55,y,z+h),.075,'Deck',par)
 return rig
def cabin(par,y,w=2.6,h=1.7):
 box('Deckhouse',(0,y,1.5+h*.5),(w,3,h),'Ivory',par,.10);box('Cabin roof',(0,y,1.5+h),(w+.35,3.35,.18),'HullNavy',par)
 for side in [-1,1]:
  for dy in [-.9,0,.9]:box('Wheelhouse window',(side*(w/2+.01),y+dy,2.3),(.035,.55,.65),'Glass',par,.02)
 for x in [-.7,.7]:box('Front window',(x,y+1.51,2.45),(.8,.035,.65),'Glass',par,.03)
def fittings(par,L,W):
 bar('Bowsprit',(0,L*.35,2),(0,L*.63,2.8),.12,'Deck',par)
 for y in [-L*.32,L*.25]:
  for x in [-W*.22,W*.22]:
   bar('Bollard',(x,y,1.5),(x,y,1.85),.09,'Iron',par);bar('Cleat',(x-.2,y,1.8),(x+.2,y,1.8),.065,'Iron',par)
 for j in range(4):
  y=-L*.15+j*.6;bar('Cargo barrel',(0,y,1.6),(0,y,2.25),.33,'Deck',par)
 for x in [-W*.32,W*.32]:sphere('Brass navigation lantern',(x,-L*.32,2.6),(.17,.17,.27),'Light',par)
 bar('Anchor stock',(-W*.48,L*.26,1),(-W*.48,L*.26,0),.07,'Iron',par)
 for s in [-1,1]:bar('Anchor fluke',(-W*.48,L*.26,0),(-W*.48+s*.42,L*.26,.3),.075,'Iron',par)
names=['Cutter','Brigantine','SteamTrawler']; roots=[]
for idx,name in enumerate(names):
 r=empty(name);roots.append(r);L=[17,23,19][idx];W=[4.6,5.6,5.3][idx];hull(r,L,W,['HullTeal','HullNavy','HullRed'][idx]);fittings(r,L,W)
 if idx<2:
  cabin(r,-L*.28,w=2.2,h=1.25)
  for m,y in enumerate([1.2] if idx==0 else [-3,4.5]):
   H=13 if idx==0 else 16-m*1.2;bar('Main mast',(0,y,1.4),(0,y,H),.14,'Deck',r)
   sail(r,y,3.3,W*1.7,5.4);sail(r,y,9,W*1.25,H-9.5)
   for x in [-W*.43,W*.43]:
    for yy in [y-2.1,y+2.1]:bar('Standing rigging',(x,yy,1.7),(0,y,H-.7),.023,'Iron',r)
   flag=empty('ANIM_Ensign',r);mesh('Pennant',[(0,y,H),(2.4,y,H-.15),(0,y,H-.8)],[(0,1,2)],'HullRed',flag)
  # Triangular jib with curvature.
  mesh('Jib',[(0,L*.59,2.8),(.15,1.2,12),(.8,L*.15,3)],[(0,1,2)],'Sail',r)
 else:
  cabin(r,-3,w=3.2,h=2.5);bar('Funnel',(0,-.4,1.6),(0,-.4,6),.58,'HullRed',r);bar('Black funnel cap',(0,-.4,5.5),(0,-.4,6.15),.65,'Iron',r)
  p=empty('MARK_Smoke',r);p.location=(0,-.4,6.2)
  bar('Fore mast',(0,4.3,1.4),(0,4.3,10),.12,'Deck',r);bar('Fishing boom',(0,4.3,7),(0,-2,4),.10,'Deck',r)
  for x in [-1.9,1.9]:bar('Crane support',(x,4,1.6),(0,4.3,7),.07,'Iron',r)
  for x in [-1.7,1.7]:sphere('Life ring',(x,-3.8,2.7),(.09,.45,.45),'HullRed',r)
 # Export at origin. Source layout is staged only after exports.
 bpy.ops.object.select_all(action='DESELECT');r.select_set(True)
 for child in r.children_recursive:child.select_set(True)
 bpy.context.view_layer.objects.active=r;bpy.context.view_layer.update()
 bpy.ops.export_scene.fbx(filepath=str(O/(name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE')
for i,r in enumerate(roots):r.location.x=(i-1)*30
bpy.ops.wm.save_as_mainfile(filepath=str(R/'ArtSource/Coastal_Fleet.blend'))
(R/'ArtSource/fleet-manifest.json').write_text(json.dumps({'ships':names,'materials':palette,'blender':bpy.app.version_string,'meshCount':len([o for o in bpy.data.objects if o.type=='MESH'])},indent=2),encoding='utf-8')
print('FLEET_EXPORT_COMPLETE')
