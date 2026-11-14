"""Original Blender source for the sea creature, silver fish and distant coastline."""
import bpy, math, random
from pathlib import Path
from mathutils import noise, Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Lighthouse/Assets/Art/Encounters';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
M={}
for name,col in {'AbyssSkin':(.105,.24,.23,1),'Sucker':(.49,.30,.25,1),'EyeGold':(.85,.43,.05,1),'Pupil':(.008,.013,.012,1),'Basalt':(.19,.24,.25,1),'Moss':(.19,.24,.15,1),'Snow':(.61,.69,.70,1),'SilverFish':(.23,.59,.65,1),'WreckLinen':(.78,.69,.51,1),'WreckWood':(.4,.27,.12,1)}.items():
 m=bpy.data.materials.new(name);m.diffuse_color=col;M[name]=m
def empty(name):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);return o
def uvball(name,p,scale,mat,parent):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=40,ring_count=24,location=p);o=bpy.context.object;o.name=name;o.scale=scale;o.parent=parent;o.data.materials.append(M[mat])
 for f in o.data.polygons:f.use_smooth=True
 return o
def mesh(name,vs,fs,mat,parent):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);o.parent=parent;o.data.materials.append(M[mat]);return o
def export(root,name):
 bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
 for c in root.children_recursive:c.select_set(True)
 bpy.context.view_layer.objects.active=root;bpy.context.view_layer.update()
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE')
kr=empty('Abyssal Kraken mantle')
uvball('Mantle', (0,0,0),(3.3,4.2,2.7),'AbyssSkin',kr)
uvball('Crown', (0,-1.1,1.7),(2.4,2.7,1.8),'AbyssSkin',kr)
for side in [-1,1]:
 uvball('Eye socket',(side*2.5,2,1.05),(.85,.95,.66),'Sucker',kr)
 uvball('Amber eye',(side*2.61,2.31,1.15),(.63,.65,.49),'EyeGold',kr)
 uvball('Vertical pupil',(side*2.63,2.85,1.18),(.16,.12,.4),'Pupil',kr)
 for i in range(6):
  y=-3+i*1.08
  uvball('Mantle ridge',(side*(1.5+math.sin(i*.6)*.5),y,2.2),(.22,.58,.35),'AbyssSkin',kr)
for i in range(18):
 a=i*2.399;uvball('Skin nodule',(math.cos(a)*2.8,math.sin(a)*3.5,1.25+abs(math.sin(a*2))*.5),(.15,.25,.13),'AbyssSkin',kr)
export(kr,'KrakenMantle');kr.location.x=-35
fish=empty('Silver fish')
uvball('Body',(0,0,0),(.23,.85,.28),'SilverFish',fish)
mesh('Tail',[(-.02,-.65,0),(-.02,-1.15,.42),(-.02,-1,.05),(-.02,-1.15,-.42)],[(0,1,2),(0,2,3)],'SilverFish',fish)
mesh('Dorsal',[(-.02,-.4,.18),(-.02,.25,.23),(-.02,-.1,.65)],[(0,1,2)],'SilverFish',fish)
for s in [-1,1]:uvball('Eye',(s*.2,.47,.1),(.055,.08,.065),'Pupil',fish)
export(fish,'SilverFish');fish.location.x=-50
wr=empty('Torn sail on broken yard');vs=[];fs=[];N=16
for j in range(N+1):
 for i in range(N+1):
  u=i/N;v=j/N;x=(u-.5)*6;y=(v-.5)*3.5
  z=.25+math.sin(u*math.pi*3)*.22+math.sin(v*math.pi)*.25
  vs.append((x,y,z))
for j in range(N):
 for i in range(N):
  if (i>12 and j>9+i%3) or (i<3 and j<i+3):continue
  a=j*(N+1)+i;fs.append((a,a+1,a+N+2,a+N+1))
ob=mesh('Torn canvas folded on water',vs,fs,'WreckLinen',wr)
for p in ob.data.polygons:p.use_smooth=True
uvball('Splintered yard', (0,-.2,.35),(3.7,.18,.18),'WreckWood',wr)
uvball('Broken spar', (0,0,.38),(.14,2.5,.14),'WreckWood',wr)
export(wr,'WreckSail');wr.location.x=-65
for kind in range(3):
 r=empty(['BasaltIsland','BrokenRidge','NeedleCape'][kind]);N=160;vs=[];fs=[]
 for j in range(N+1):
  for i in range(N+1):
   x=(i/N-.5)*2;y=(j/N-.5)*2
   p=Vector((x*5+kind*17,y*5,kind+2));n=noise.noise(p)
   radial=max(0,1-(x*x+y*y)+n*.14)
   peaks=[(-.34,-.1,.75,.65,1),(.17,.22,.40,.57,1.25),(.49,-.22,.42,.35,.78)] if kind!=1 else [(-.55,.02,.48,.5,.75),(-.09,.15,.46,.48,1.1),(.36,-.07,.4,.43,1.4),(.6,.3,.34,.36,.85)]
   mountain=max(max(0,1-math.sqrt(((x-px)/wx)**2+((y-py)/wy)**2))*h for px,py,wx,wy,h in peaks)
   detail=noise.noise(p*3)*.16+noise.noise(p*8)*.065+noise.noise(p*19)*.025
   gullies=abs(math.sin(x*39+y*14+n*4))*.045*mountain
   z=radial**.5*(mountain*.9+detail-gullies+.10)*[180,250,205][kind]-15
   if kind==2:z+=max(0,1-math.sqrt((x+.02)**2+(y-.05)**2)*6)*85
   vs.append((x*[280,420,210][kind],y*[210,280,230][kind],z))
 for j in range(N):
  for i in range(N):
   a=j*(N+1)+i;fs.extend([(a,a+1,a+N+2),(a,a+N+2,a+N+1)])
 ob=mesh(r.name+' eroded terrain',vs,fs,'Basalt',r);ob.data.materials.append(M['Moss']);ob.data.materials.append(M['Snow'])
 uv=ob.data.uv_layers.new(name='Rock metres')
 for poly in ob.data.polygons:
  poly.use_smooth=True
  center=sum((Vector(vs[v]) for v in poly.vertices),Vector())/len(poly.vertices)
  poly.material_index=2 if center.z>245 else (1 if poly.normal.z>.75 and 8<center.z<65 else 0)
  for loop in poly.loop_indices:
   p=vs[ob.data.loops[loop].vertex_index];uv.data[loop].uv=(p[0]/28,p[1]/28)
 export(r,r.name);r.location.x=600+kind*900
# Original mottled skin texture, useful on the mantle and continuous animated arms.
im=bpy.data.images.new('AbyssSkin mottled',width=512,height=512);pixels=[]
for y in range(512):
 for x in range(512):
  p=Vector((x/512*12,y/512*12,2));n=noise.noise(p)*.5+.5;fine=noise.noise(p*7)*.5+.5
  pixels.extend((.25+n*.30,.43+n*.33,.39+n*.30,.65+fine*.3))
im.pixels=pixels;im.filepath_raw=str(OUT/'AbyssSkin.png');im.file_format='PNG';im.save();im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Encounter_Coast.blend'))
print('ENCOUNTER_ART_COMPLETE')
