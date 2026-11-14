"""V02: ocean-going ships with authored fracture groups, plus a distinct abyssal squid.
Metres; Blender +Y is bow, +Z is up. Source pivots survive the Unity import.
"""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector, noise
R=Path(__file__).resolve().parents[1]; O=R/'Lighthouse/Assets/Art/Marine'; O.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC'
palette={'HullNavy':(.025,.064,.081,1),'HullRed':(.22,.065,.035,1),'HullTeal':(.028,.11,.10,1),'Deck':(.24,.13,.061,1),'Sail':(.74,.69,.55,1),'Brass':(.42,.28,.10,1),'Iron':(.025,.03,.032,1),'Ivory':(.63,.57,.43,1),'Glass':(.045,.11,.13,1),'Light':(1,.56,.17,1),'Fracture':(.43,.27,.12,1),'DeepSkin':(.048,.10,.115,1),'Ridge':(.025,.046,.057,1),'Flesh':(.21,.13,.12,1),'Eye':(.67,.31,.037,1)}
M={}
for n,c in palette.items():
 m=bpy.data.materials.new('Marine_'+n);m.diffuse_color=c;m.use_nodes=True
 p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=c;p.inputs['Roughness'].default_value=.52
 M[n]=m
def empty(n,par=None):
 o=bpy.data.objects.new(n,None);bpy.context.collection.objects.link(o);o.parent=par;return o
def mesh(n,vs,fs,mat,par,smooth=False):
 me=bpy.data.meshes.new(n);me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new(n,me);bpy.context.collection.objects.link(o);o.parent=par;o.data.materials.append(M[mat])
 uv=me.uv_layers.new(name='Metres');
 for poly in me.polygons:
  poly.use_smooth=smooth
  for li in poly.loop_indices:
   v=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(v.y*.18,v.z*.18+v.x*.1)
 return o
def box(n,p,s,mat,par):
 x,y,z=p;X,Y,Z=[v*.5 for v in s];vs=[(x+sx*X,y+sy*Y,z+sz*Z) for sx,sy,sz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
 return mesh(n,vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,par)
def rod(n,a,b,r,mat,par,r2=None,N=10):
 a,b=Vector(a),Vector(b);d=(b-a).normalized();x=d.cross(Vector((0,1,0)))
 if x.length<.01:x=d.cross(Vector((1,0,0)))
 x.normalize();y=d.cross(x);vs=[]
 for p,rad in [(a,r),(b,r if r2 is None else r2)]:
  for i in range(N):q=i*math.tau/N;vs.append(p+(x*math.cos(q)+y*math.sin(q))*rad)
 fs=[tuple(reversed(range(N))),tuple(range(N,2*N))]+[(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
 return mesh(n,vs,fs,mat,par,True)
def ball(n,p,s,mat,par):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=28,ring_count=16,location=p);o=bpy.context.object;o.name=n;o.scale=s;o.parent=par;o.data.materials.append(M[mat])
 for f in o.data.polygons:f.use_smooth=True
 return o
def setpivot(ob,p):
 # Vertices were authored in ship coordinates. Translate the descendants once.
 for c in ob.children:c.location-=Vector(p)
 ob.location=p
def export(root,name):
 bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
 for c in root.children_recursive:c.select_set(True)
 bpy.context.view_layer.objects.active=root;bpy.context.view_layer.update()
 bpy.ops.export_scene.fbx(filepath=str(O/(name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE')
roots=[];manifest=[]
for idx,(name,L,W,H) in enumerate([('MeridianFrigate',48,11.8,34),('PilgrimBarque',57,12.8,39),('NorthernSteamer',62,11.5,26)]):
 root=empty(name);roots.append(root);groups=[empty('BREAK_Hull_Aft',root),empty('BREAK_Hull_Mid',root),empty('BREAK_Hull_Bow',root)];N=72;ring=13
 def width(q):return W*.5*max(.018,math.sin(math.pi*(.10+q*.90)))**.55*(1-.19*q)
 def sheer(q):return 4.8+1.7*abs(q*2-1)**3
 def group(q):return groups[min(2,int(q*3))]
 # Thick closed hull sections: matching sawtooth fracture boundaries and interior ribs.
 for part,g in enumerate(groups):
  vs=[];fs=[]
  for j in range(25):
   q=(part*24+j)/N;y=(q-.5)*L;w=width(q);top=sheer(q)
   section=[(-w,top),(-w*1.01,2.7),(-w*.95,.1),(-w*.75,-1.8),(-w*.43,-3.1),(-w*.14,-3.65),(0,-3.75),(w*.14,-3.65),(w*.43,-3.1),(w*.75,-1.8),(w*.95,.1),(w*1.01,2.7),(w,top)]
   for k,(x,z) in enumerate(section):
    jag=(math.sin(k*13.4)*.35+math.cos(k*6.2)*.20) if j in (0,24) and (part>0 or j==24) and (part<2 or j==0) else 0
    vs.append((x,y+jag,z))
  for j in range(24):
   for k in range(ring-1):a=j*ring+k;fs.append((a,a+ring,a+ring+1,a+1))
  hull=mesh('Carvel planked hull',vs,fs,['HullNavy','HullTeal','HullRed'][idx],g,True)
  # Closed timber end faces remain visible after the hull splits.
  for j in (0,24):mesh('Exposed transverse fracture',vs[j*ring:(j+1)*ring],[tuple(range(ring))],'Fracture',g)
  for j in range(24):
   q=(part*24+j+.5)/N;y=(q-.5)*L;w=width(q);z=sheer(q)
   box('Deck strake',(0,y,z-.13),(w*2,L/N-.018,.24),'Deck',g)
   if j%3==0:
    for s in (-1,1):rod('Bulwark stanchion',(s*w,y,z),(s*w,y,z+1.05),.055,'Iron',g)
   for s in (-1,1):
    q0=(part*24+j)/N;q1=(part*24+j+1)/N
    for dz,r,mat in [(0,.11,'Ivory'),(1.05,.075,'Deck'),(-1.7,.09,'Ivory'),(-2.15,.065,'Brass')]:
     rod('Sheer moulding',(s*width(q0),(q0-.5)*L,sheer(q0)+dz),(s*width(q1),(q1-.5)*L,sheer(q1)+dz),r,mat,g,N=6)
   if j%4==0:
    for s in (-1,1):
     box('Gunport frame' if idx<2 else 'Porthole',(s*(w+.025),y,2.35),(.14,.93,.9),'Ivory',g)
     box('Dark gunport',(s*(w+.11),y,2.35),(.08,.67,.63),'Iron',g)
     if idx<2:rod('Bronze cannon',(s*(w-.25),y,2.2),(s*(w+.65),y,2.2),.16,'Iron',g)
  for q in [part/3+.07,part/3+.20]:
   y=(q-.5)*L;w=width(q)
   for s in (-1,1):rod('Interior curved frame',(s*w*.68,y,-1.4),(s*w*.95,y,4.4),.19,'Fracture',g)
 # Raised forecastle, poop and proper transom galleries.
 for y,z,length in [(-L*.33,6.5,L*.28),(L*.37,6.1,L*.18)]:
  g=groups[0 if y<0 else 2];w=width(y/L+.5)*1.7
  box('Raised deck',(0,y,z),(w,length,.35),'Deck',g)
  box('Cabin sides',(0,y,(z+4.7)/2),(w*.94,length*.94,z-4.7),'HullNavy',g)
  for s in (-1,1):
   for k in range(7):
    yy=y+(k/6-.5)*length*.8
    box('Cabin window brass frame',(s*w*.473,yy,z-.85),(.13,.7,.8),'Brass',g)
    box('Cabin amber window',(s*w*.481,yy,z-.85),(.07,.5,.6),'Glass',g)
  for s in (-1,1):rod('Raised deck rail',(s*w*.5,y-length*.47,z+.85),(s*w*.5,y+length*.47,z+.85),.07,'Ivory',g)
 for row in range(2):
  for col in range(7):
   x=(col-3)*.85
   box('Stern gallery window',(x,-L*.485,3.1+row*1.35),(.6,.15,.9),'Brass',groups[0]);box('Stern glass',(x,-L*.49,3.1+row*1.35),(.46,.09,.7),'Glass',groups[0])
 for s in (-1,1):
  for y in [-L*.38,L*.34]:
   rod('Capstan bollard',(s*W*.3,y,6.5),(s*W*.3,y,7.1),.16,'Iron',group(y/L+.5));rod('Mooring cleat',(s*W*.3-.3,y,7),(s*W*.3+.3,y,7),.12,'Iron',group(y/L+.5))
  for y in [-L*.36,0,L*.32]:ball('Navigation lantern',(s*W*.46,y,6.0),(.22,.22,.42),'Light',group(y/L+.5))
 rod('Bowsprit',(0,L*.30,6.1),(0,L*.66,9.2),.31,'Deck',groups[2],.13)
 for s in (-1,1):rod('Bow stay',(s*W*.32,L*.32,4.8),(0,L*.66,9.2),.035,'Iron',groups[2],N=6)
 # Separate deck fragments, all present before the attack and then released.
 for j in range(14):
  q=.2+(j%7)*.095;y=(q-.5)*L;s=-1 if j<7 else 1;x=s*width(q)*.62
  part=empty('BREAK_Plank_%02d'%j,root);box('Timber grating',(x,y,5.05),(1.0,3.1,.24),'Deck',part)
  rod('Broken rail',(x,y-1.5,5.3),(x,y+1.5,5.3),.08,'Ivory',part);setpivot(part,(x,y,5.1))
 for j,y in enumerate([-L*.24,0,L*.24] if idx<2 else [-L*.28,L*.28]):
  mh=H*(.86 if j==0 else 1 if j==1 else .92); mast=empty('BREAK_Mast_%d'%j,root)
  # Jagged base at the deck; lower stump is part of the hull.
  rod('Lower mast stump',(0,y,4.8),(0,y,7.5),.38,'Deck',group(y/L+.5),.34)
  rod('Main tapered mast',(0,y,7.3),(0,y,mh),.35,'Deck',mast,.085)
  for dz in [7.3,14,mh*.76]:rod('Mast binding',(0,y,dz),(0,y,dz+.25),.39 if dz<15 else .24,'Iron',mast)
  rod('Crows nest',(0,y,mh*.62),(0,y,mh*.62+.3),1.25,'Deck',mast,N=20)
  for a in range(10):
   ang=a*math.tau/10;x=math.cos(ang)*1.2;yy=y+math.sin(ang)*1.2
   rod('Nest baluster',(x,yy,mh*.62+.3),(x,yy,mh*.62+1.05),.04,'Iron',mast,N=6)
  if idx<2:
   for si,(z,ww,hh) in enumerate([(9,W*1.75,8),(18,W*1.48,6.7),(25,W*1.06,5.5)]):
    if z+hh>mh:continue
    rig=empty('ANIM_Sail_%d_%d'%(j,si),mast);vs=[];fs=[];nu,nv=24,14;ang=math.radians(26 if idx==0 else -24)
    for v in range(nv+1):
     vv=v/nv
     for u in range(nu+1):
      uu=u/nu;x=(uu-.5)*ww*(1-.24*vv);yy=math.sin(uu*math.pi)*math.sin(vv*math.pi)*2.0
      xx=x*math.cos(ang)-yy*math.sin(ang);yyy=y+x*math.sin(ang)+yy*math.cos(ang)
      zz=z+vv*hh+.6*(abs(uu*2-1)**2)*(1-vv)
      vs.append((xx,yyy,zz))
    for v in range(nv):
     for u in range(nu):a=v*(nu+1)+u;fs.append((a,a+1,a+nu+2,a+nu+1))
    intact=empty('STATE_Intact_canvas',rig);broken=empty('STATE_Broken_canvas',rig)
    mesh('Stitched billowing canvas',vs,fs,'Sail',intact,True)
    torn=[];torn_faces=[]
    for vi,p in enumerate(vs):
     vv=(vi//(nu+1))/nv;uu=(vi%(nu+1))/nu
     sag=(1-vv)*(.65+1.6*math.sin(uu*math.pi)**2)
     torn.append((p[0],p[1]+sag*.8,p[2]-sag))
    for v in range(nv):
     for u in range(nu):
      uu=(u+.5)/nu;vv=(v+.5)/nv
      hole=((uu-.29)**2/.018+(vv-.42)**2/.055<1) or (uu>.64 and vv<.25+.10*math.sin(u*4.6)) or (vv<.10+.04*math.sin(u*3.2))
      if not hole:a=v*(nu+1)+u;torn_faces.append((a,a+1,a+nu+2,a+nu+1))
    mesh('Torn slack canvas',torn,torn_faces,'Sail',broken,True)
    for u in range(0,nu+1,4):
     for v in range(nv):rod('Canvas seam',vs[v*(nu+1)+u],vs[(v+1)*(nu+1)+u],.014,'Ivory',intact,N=4)
    for v in [0,nv]:
     for u in range(nu):rod('Canvas bolt rope',vs[v*(nu+1)+u],vs[v*(nu+1)+u+1],.035,'Deck',intact,N=6)
    end=ww*.54;left=(-end*math.cos(ang),y-end*math.sin(ang),z+hh);right=(end*math.cos(ang),y+end*math.sin(ang),z+hh)
    rod('Yard',left,right,.16,'Deck',intact,.11)
    joint=(ww*.12*math.cos(ang),y+ww*.12*math.sin(ang),z+hh)
    rod('Broken yard long',left,joint,.16,'Deck',broken,.14)
    rod('Broken yard hanging',joint,(right[0],right[1]+1.4,right[2]-2.5),.14,'Deck',broken,.09)
    for chip in range(5):rod('Bright timber splinter',(joint[0]+chip*.06,joint[1],joint[2]),(joint[0]+.3+chip*.11,joint[1]+.25*math.sin(chip),joint[2]+.5-chip*.13),.055,'Fracture',broken,.012,N=5)
    setpivot(rig,(0,y,z+hh))
   for s in (-1,1):
    for dy in [-2.1,0,2.1]:rod('Shroud',(s*W*.44,y+dy,5.1),(0,y,mh*.64),.035,'Iron',mast,N=6)
    for step in range(20):
     f=step/20;xx=s*W*.44*(1-f);zz=5.1+(mh*.64-5.1)*f
     rod('Ratline',(xx,y-2.1*(1-f),zz),(xx,y+2.1*(1-f),zz),.025,'Deck',mast,N=4)
  else:
   for s in (-1,1):rod('Cargo derrick',(0,y,8),(s*6,y-8,14),.20,'Deck',mast,.12)
  flag=empty('ANIM_Ensign_%d'%j,mast);vs=[(0,y,mh),(3.8,y-.3,mh-.3),(3.1,y+.4,mh-1.5),(0,y,mh-1.2)];mesh('Swallowtail ensign',vs,[(0,1,2,3)],'HullRed',flag);setpivot(flag,(0,y,mh))
  setpivot(mast,(0,y,7.3))
 if idx<2:
  # Curved forestaysails ahead of the foremast, with separated outlines.
  for j in range(2):
   points=[Vector((0,L*(.65 if j==0 else .44),9.2)),Vector((0,L*(.38 if j==0 else .24),20 if j==0 else 28)),Vector((0,L*(.45 if j==0 else .28),10.5))]
   vs=[];fs=[];lookup={};N=12
   for a in range(N+1):
    for b in range(N+1-a):
     u=a/N;v=b/N;w=1-u-v;p=points[0]*u+points[1]*v+points[2]*w;p.x+=u*v*w*34;lookup[(a,b)]=len(vs);vs.append(p)
   for a in range(N):
    for b in range(N-a):
     fs.append((lookup[(a,b)],lookup[(a+1,b)],lookup[(a,b+1)]))
     if a+b<N-1:fs.append((lookup[(a+1,b)],lookup[(a+1,b+1)],lookup[(a,b+1)]))
   jib=empty('ANIM_Sail_jib_%d'%j,groups[2]);mesh('Curved separate forestaysail',vs,fs,'Sail',jib,True);setpivot(jib,points[1]);rod('Forestay',points[0],points[1],.045,'Iron',groups[2])
 else:
  box('Bridge',(0,-L*.12,8.2),(8.4,6.3,4),'Ivory',groups[1]);box('Bridge roof',(0,-L*.12,10.3),(9,7,.3),'Deck',groups[1])
  for j in range(6):box('Bridge windows',((j-2.5)*1.2,-L*.12+3.18,9),(1,.12,1.15),'Glass',groups[1])
  for y in [-.5,5]:
   rod('Funnel',(0,y,5),(0,y,16),1.15,'HullRed',groups[1]);rod('Funnel top',(0,y,14.6),(0,y,16.5),1.23,'Iron',groups[1]);marker=empty('MARK_Smoke',groups[1]);marker.location=(0,y,16.5)
  for y in [-L*.34,L*.28]:box('Cargo hatch',(0,y,6.8),(6,8,.55),'Deck',group(y/L+.5))
 for i,g in enumerate(groups):setpivot(g,(0,(i-1)*L/3,1.8))
 export(root,name);manifest.append({'id':name,'length':L,'beam':W,'mast':H,'fractureGroups':len([o for o in root.children_recursive if o.name.startswith('BREAK_')])});root.location.x=idx*85

kr=empty('AbyssalKraken');roots.append(kr);vs=[];fs=[];N=64;Z=44
for j in range(Z+1):
 q=j/Z;z=q*24;rad=math.sin(math.pi*(.12+q*.88))**.7*(1-q*.22)
 for i in range(N):
  a=i/N*math.tau;rib=1+.055*math.cos(a*12+q*4);x=math.cos(a)*6.1*rad*rib;y=math.sin(a)*7.6*rad-4*q
  vs.append((x,y,z))
for j in range(Z):
 for i in range(N):a=j*N+i;b=j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
mesh('Tapered ridged mantle',vs,fs,'DeepSkin',kr,True)
for s in (-1,1):
 # Swept lateral fins, heavy shoulder ridges and recessed eyes.
 fin=[];faces=[];U,V=12,30
 for j in range(V+1):
  v=j/V
  for i in range(U+1):
   u=i/U;spread=math.sin(v*math.pi)**.75
   fin.append((s*(3.2+u*8.0*spread),-2-v*5-u*u*2.5,10+v*12+math.sin(u*math.pi)*1.2+math.sin(v*24)*u*.25))
 for j in range(V):
  for i in range(U):a=j*(U+1)+i;faces.append((a,a+1,a+U+2,a+U+1))
 mesh('Organic swept mantle fin',fin,faces,'DeepSkin',kr,True)
 ball('Recessed eye socket',(s*4.7,4.1,4.8),(1.7,1.2,1.2),'Ridge',kr)
 ball('Amber lateral eye',(s*5.0,4.8,4.8),(1.0,.65,.63),'Eye',kr)
 ball('Oblique pupil',(s*5.05,5.34,4.8),(.22,.09,.49),'Iron',kr)
 for j in range(9):
  z=3+j*1.65;rad=math.sin(math.pi*(.12+z/24*.88))**.7*(1-z/24*.22)
  rod('Armoured scar',(s*5.4*rad,0,z),(s*3.9*rad,-4,z+1.6),.22,'Ridge',kr,.08)
ball('Brachial crown',(0,2.2,1.5),(5.8,5.8,2.5),'DeepSkin',kr)
mesh('Dark hooked beak',[(-1,7,1),(1,7,1),(0,9.8,-1.5),(0,7,3),(-.9,6,-1),(1,6,-1)],[(0,1,3),(0,2,1),(0,4,2),(1,2,5),(4,5,2)],'Iron',kr)
export(kr,'AbyssalKraken');kr.location.x=-85
# Editable skin: pigment islands and fine grain, no copied imagery.
im=bpy.data.images.new('Abyssal dermis',width=512,height=512);px=[]
for y in range(512):
 for x in range(512):
  p=Vector((x/512*10,y/512*14,4));n=noise.noise(p)*.5+.5;grain=noise.noise(p*14)*.5+.5;ridge=abs(math.sin(x*.08+noise.noise(p)*3))**18
  px.extend((.16+n*.25+ridge*.08,.24+n*.24,.25+n*.22,.55+grain*.4))
im.pixels=px;im.filepath_raw=str(O/'AbyssDermis.png');im.file_format='PNG';im.save();im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(R/'ArtSource/Ocean_Fleet_V02.blend'))
(R/'ArtSource/marine-manifest.json').write_text(json.dumps({'vessels':manifest,'blender':bpy.app.version_string,'kraken':'24m tapered mantle; procedural arms authored in Unity'},indent=2),encoding='utf-8')
print('OCEAN_FLEET_V02_COMPLETE')
