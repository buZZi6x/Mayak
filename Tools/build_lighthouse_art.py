"""Author the coastal art pass in Blender; preserve gameplay pivots and collision shells."""
import bpy, math, random, json
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils import noise

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Lighthouse/Assets/Art/Coastal'
for d in ('Models','Textures'): (OUT/d).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Lighthouse_Blockout.blend'))
random.seed(71113)
world=bpy.data.objects['Lighthouse_Blockout']
camera=bpy.data.objects['Camera_Blockout'];camera.location=(0,0,0)
M={m.name:m for m in bpy.data.materials}
S=1024
yy,xx=np.mgrid[0:S,0:S].astype(np.float32)/S
rng=np.random.default_rng(71113)
def vn(nx,ny=None):
    ny=ny or nx;grid=rng.random((ny,nx)).astype(np.float32)
    x=xx*nx;y=yy*ny;ix=x.astype(int);iy=y.astype(int);u=x-ix;v=y-iy;u=u*u*(3-2*u);v=v*v*(3-2*v)
    return (grid[iy%ny,ix%nx]*(1-u)+grid[iy%ny,(ix+1)%nx]*u)*(1-v)+(grid[(iy+1)%ny,ix%nx]*(1-u)+grid[(iy+1)%ny,(ix+1)%nx]*u)*v
def save_map(name,data):
    im=bpy.data.images.new(name,width=S,height=S,alpha=True)
    im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(np.asarray(data,dtype=np.float32).ravel())
    im.filepath_raw=str(OUT/'Textures'/f'{name}.png');im.file_format='PNG';im.save();return im
recipes={
 'Coast_Rock':((.24,.285,.30),'rock',.03,.24),
 'Coast_Masonry':((.52,.50,.44),'stone',0,.22),
 'Coast_Plaster':((.76,.73,.64),'plaster',0,.20),
 'Coast_Wood':((.31,.19,.095),'wood',0,.27),
 'Coast_Copper':((.12,.25,.24),'copper',.68,.39),
 'Coast_Red':((.37,.065,.035),'paint',.12,.36),
 'Coast_Iron':((.065,.082,.088),'metal',.72,.41),
 'Coast_Brass':((.62,.39,.12),'metal',.82,.47),
 'Coast_Teal':((.045,.26,.24),'paint',.30,.38),
 'Coast_Cloth':((.065,.17,.28),'cloth',0,.12),
 'Coast_Linen':((.66,.62,.50),'cloth',0,.12),
 'Coast_Paper':((.64,.59,.43),'paper',0,.12),
}
for name,(base,kind,metal,smooth) in recipes.items():
    print('TEXTURE '+name,flush=True)
    broad=vn(4);medium=vn(24);fine=vn(128);grain=rng.random((S,S)).astype(np.float32)
    h=.48*broad+.30*medium+.15*fine+.07*grain
    rgb=np.ones((S,S,3),np.float32)*np.array(base)
    variation=.72+h*.52
    if kind=='wood':
        streak=vn(4,128);rings=.5+.5*np.sin(xx*math.tau*18+vn(6)*5+np.sin(yy*math.tau)*2)
        h=.45*streak+.4*rings+.15*fine;variation=.6+h*.8
        for cx,cy in ((.24,.3),(.76,.78)):
            rr=np.sqrt(((xx-cx)*1.5)**2+((yy-cy)*.28)**2)
            knot=np.exp(-rr*rr*170);variation-=knot*(.25+.18*np.sin(rr*230))
    elif kind=='rock':
        strata=.5+.5*np.sin(yy*math.tau*14+broad*8+medium*.5)
        h=.56*h+.44*strata;variation=.68+h*.6
        cracks=np.clip((.15-abs(medium-.5))*8,0,.8)*np.clip((broad-.6)*5,0,1)
        variation-=cracks*.4;h-=cracks*.2
    elif kind=='copper':
        patina=np.clip((broad+.28*medium-.43)*2.8,0,1)
        rgb=rgb*patina[:,:,None]+np.array((.33,.16,.065))*(1-patina[:,:,None]);variation=.7+h*.55
    elif kind=='paint':
        chips=np.clip((fine+medium*.3-.94)*6,0,1)
        rgb=rgb*(1-chips[:,:,None])+np.array((.28,.23,.16))*chips[:,:,None]
    elif kind=='cloth':
        weave=(np.sin(xx*math.tau*220)*np.sin(yy*math.tau*220)+1)*.5
        h=.45*weave+.45*medium+.1*broad;variation=.85+h*.25
    rgba=np.ones((S,S,4),np.float32);rgba[:,:,:3]=np.clip(rgb*variation[:,:,None],0,1)
    albedo=save_map(name+'_Base',rgba)
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*3.5;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*3.5
    if kind in ('metal','paint','paper'):dx*=.3;dy*=.3
    length=np.sqrt(dx*dx+dy*dy+1);normal=np.ones_like(rgba);normal[:,:,0]=(-dx/length+1)*.5;normal[:,:,1]=(-dy/length+1)*.5;normal[:,:,2]=(1/length+1)*.5
    norm=save_map(name+'_Normal',normal)
    mask=np.ones_like(rgba);mask[:,:,0]=metal;mask[:,:,1]=.88+h*.12;mask[:,:,3]=np.clip(smooth+(h-.5)*.18,.04,.85)
    save_map(name+'_Mask',mask)
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*base,1);M[name]=m
    nodes=m.node_tree.nodes;links=m.node_tree.links;nodes.clear()
    p=nodes.new('ShaderNodeBsdfPrincipled');out=nodes.new('ShaderNodeOutputMaterial');links.new(p.outputs['BSDF'],out.inputs['Surface'])
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=1-smooth
    a=nodes.new('ShaderNodeTexImage');a.image=albedo;a.image.colorspace_settings.name='sRGB';links.new(a.outputs['Color'],p.inputs['Base Color'])
    n=nodes.new('ShaderNodeTexImage');n.image=norm;nm=nodes.new('ShaderNodeNormalMap');links.new(n.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],p.inputs['Normal'])

def put(o,mat,parent):
    o.data.materials.clear();o.data.materials.append(M[mat]);o.parent=parent;return o
def bevel(o,width=.025,segments=3):
    mod=o.modifiers.new('Soft machined edges','BEVEL');mod.width=width;mod.segments=segments
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in o.data.polygons:p.use_smooth=True
    mod=o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name);return o
def cube(name,loc,size,mat='Coast_Wood',parent=world,b=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);put(o,mat,parent)
    if b:bevel(o,min(b,min(size)*.35))
    return o
def cyl(name,loc,r,d,mat='Coast_Iron',parent=world,n=40,b=.012):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d,location=loc);o=bpy.context.object;o.name=name;put(o,mat,parent)
    if b:bevel(o,min(b,d*.2,r*.2),2)
    return o
def mesh(name,verts,faces,mat='Coast_Rock',parent=world):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return put(o,mat,parent)
def bar(name,a,b,r=.025,mat='Coast_Iron',parent=world):
    a,b=Vector(a),Vector(b);o=cyl(name,(a+b)/2,r,(b-a).length,mat,parent,n=16,b=.005);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def torus(name,loc,r,thick,mat='Coast_Brass',parent=world):
    bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=8,location=loc,major_radius=r,minor_radius=thick)
    o=bpy.context.object;o.name=name;put(o,mat,parent)
    for p in o.data.polygons:p.use_smooth=True
    return o
def text(name,body,loc,size=.1,mat='Coast_Linen',parent=world,rotation=(math.pi/2,0,0)):
    c=bpy.data.curves.new(name,'FONT');c.body=body;c.size=size;c.align_x='CENTER';c.extrude=.0008;c.bevel_depth=.0004;c.space_line=1.15
    if font:c.font=font
    o=bpy.data.objects.new('VIS_'+name,c);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=rotation;o.parent=parent;c.materials.append(M[mat])
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');return o
font=bpy.data.fonts.load('C:/Windows/Fonts/arial.ttf')
def remove(o):
    for c in list(o.children):remove(c)
    bpy.data.objects.remove(o,do_unlink=True)
def hidden_shell(o):o.name=o.name.replace('SOLID_','COLL_');o.hide_render=True
def wedge(name,ri,ro,a,b,z,h,mat,parent=world):
    vs=[(r*math.cos(t),r*math.sin(t),zz) for zz in (z-h,z) for r,t in ((ri,a),(ro,a),(ro,b),(ri,b))]
    return mesh(name,vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,parent)

# Preserve the tested collision envelope; rebuild visible stone and walking surfaces.
for o in list(world.children_recursive):
    if o.type!='MESH':continue
    old=o.data.materials[0].name if o.data.materials else ''
    mapping={'Stone':'Coast_Rock','PaleStone':'Coast_Masonry','Ivory':'Coast_Plaster','Red':'Coast_Red','Roof':'Coast_Copper','Iron':'Coast_Iron','Wood':'Coast_Wood','Brass':'Coast_Brass','Blue':'Coast_Cloth','Teal':'Coast_Teal','White':'Coast_Linen'}
    if old in mapping:put(o,mapping[old],o.parent)
    if o.name.startswith(('SOLID_TowerWall_','SOLID_RedBand_')) or o.name in ('SOLID_Terrace','SOLID_GroundFloor','SOLID_WatchFloor','SOLID_Balcony','SOLID_Roof'):
        hidden_shell(o)
    elif o.name.startswith('SOLID_Stair_') or o.name in ('DeskTop','RadioCase','BedFrame','Mattress','Pillow','DoorLeaf','DoorInset'):
        bevel(o,.025,3)

# Layered coastal outcrop with overhangs, fractures and independent wave-cut boulders.
remove(bpy.data.objects['SOLID_IslandRock'])
for o in list(world.children):
    if o.name.startswith('SOLID_ShoreRock_'):remove(o)
vs=[];fs=[];n=96
levels=[(-4.5,14.8),(-2.7,16.1),(-1.2,16.7),(.1,16.2),(1.1,15.1),(2.1,14.8),(3.15,13.2),(4.12,12.1)]
for j,(z,r) in enumerate(levels):
    for i in range(n):
        a=i*math.tau/n;delta=math.sin(a*3+.8)*.7+math.cos(a*7)*.42+math.sin(a*15+j*.7)*.28+random.uniform(-.22,.22)
        zz=z+(math.sin(a*5+j*.6)*.19 if j<7 else random.uniform(-.025,.025))
        vs.append(((r+delta)*math.cos(a),(r+delta)*math.sin(a),zz))
for j in range(len(levels)-1):
    for i in range(n):
        a=j*n+i;b=j*n+(i+1)%n
        fs.extend([(a,b,b+n),(a,b+n,a+n)])
rock=mesh('SOLID_IslandRock',vs,fs)
for i in range(14):
    a=i*math.tau/14;r=random.uniform(14,18);bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=1)
    o=bpy.context.object;o.name='VIS_Wavecut_Boulder_%02d'%i
    for v in o.data.vertices:
        co=v.co.copy();v.co*=1+noise.fractal(co*3,1.0,2,3)*.16;v.co.z=round(v.co.z*9)/9*.75+v.co.z*.25
    o.location=(math.cos(a)*r,math.sin(a)*r,random.uniform(-1,.3));o.scale=(random.uniform(1.8,3.2),random.uniform(1.3,2.7),random.uniform(2.1,3.8));o.rotation_euler=(.1,random.uniform(-.2,.2),a)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);put(o,'Coast_Rock',world)

# Paved platform and radial coping, with seams and irregular edge rhythm.
for ring in range(8):
    ri=3.88+ring*1.015;ro=ri+1.0;count=round(ro*math.tau/1.18)
    for i in range(count):
        a=(i+(ring%2)*.5)*math.tau/count;b=a+math.tau/count-.006
        o=wedge('VIS_Terrace_Paving',ri+.014,ro,a,b,4.165,.08,'Coast_Masonry');bevel(o,.014,2)
for i in range(60):
    a=i*math.tau/60;b=(i+1)*math.tau/60-.007
    o=wedge('VIS_Cliff_Coping',11.75,12.30,a,b,4.22,.28,'Coast_Masonry');bevel(o,.025,2)

# Tapered, round plaster tower with a real door aperture and exposed masonry footing.
def wall_strip(name,z0,z1,r0,r1,mat,door=False):
    verts=[];faces=[];n=128
    for i in range(n):
        a=i*math.tau/n;b=(i+1)*math.tau/n
        mid=(a+b)/2
        if door and math.sin(mid)<0 and abs(math.cos(mid))<.28:continue
        k=len(verts);verts.extend([(r0*math.cos(a),r0*math.sin(a),z0),(r0*math.cos(b),r0*math.sin(b),z0),(r1*math.cos(b),r1*math.sin(b),z1),(r1*math.cos(a),r1*math.sin(a),z1)])
        faces.append((k,k+1,k+2,k+3))
    o=mesh(name,verts,faces,mat)
    for p in o.data.polygons:p.use_smooth=True
    return o
wall_strip('VIS_Tower_Plaster_Lower',4.46,7.12,3.81,3.70,'Coast_Plaster',True)
wall_strip('VIS_Tower_Plaster_Upper',7.12,9.85,3.70,3.59,'Coast_Plaster')
for lo,hi,door in ((4.46,7.12,True),(7.12,9.85,False)):
    inner=wall_strip('VIS_Interior_Plaster',lo,hi,3.43,3.43,'Coast_Plaster',door)
    import bmesh
    bm=bmesh.new();bm.from_mesh(inner.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(inner.data);bm.free()
wall_strip('VIS_Tower_Red_Stripe',7.93,8.52,3.686,3.663,'Coast_Red')
for z in (4.52,4.75,9.65,9.82):torus('VIS_Architectural_Cornice',(0,0,z),3.8 if z<5 else 3.65,.09,'Coast_Masonry')
for row in range(3):
    for i in range(32):
        a=(i+(row%2)*.5)*math.tau/32;b=a+math.tau/32-.008
        if math.sin((a+b)/2)<0 and abs(math.cos((a+b)/2))<.3:continue
        o=wedge('VIS_Footing_Stone',3.70,3.9,a,b,4.7+row*.23,.21,'Coast_Masonry');bevel(o,.025,2)
for x in (-1.15,1.15):
    for j in range(7):cube('VIS_Door_Quoin',(x,-3.77,4.65+j*.34),(.31,.43,.32),'Coast_Masonry',b=.035)
for i in range(15):
    a=i*math.pi/15;b=(i+1)*math.pi/15-.02
    coords=[(r*math.cos(t),y,6.93+r*math.sin(t)) for y in (-4.02,-3.63) for r,t in ((1.0,a),(1.35,a),(1.35,b),(1.0,b))]
    bevel(mesh('VIS_Entry_Arch',coords,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'Coast_Masonry'),.018,2)
cube('VIS_Entry_Plaque',(0,-3.87,7.65),(1.02,.06,.27),'Coast_Iron')
text('Station_Number','МАЯК  /  07',(0,-3.911,7.59),.13,'Coast_Brass')

# Individual wood boards preserve the original broad walking route upstairs.
def boarded_disk(z,inner,outer):
    step=.25
    for j in range(math.ceil(outer*2/step)):
        y=-outer+(j+.5)*step
        for seg in (-1,1):
            edge=math.sqrt(max(0,outer*outer-y*y));hole=math.sqrt(max(0,inner*inner-y*y)) if abs(y)<inner else 0
            length=edge-hole-.015
            if length>.06:cube('VIS_Floor_Plank',((hole+edge)*.5*seg,y,z-.025),(length,step-.008,.05),'Coast_Wood',b=.008)
boarded_disk(4.465,0,3.62);boarded_disk(9.868,3.20,5.55)
for i in range(64):
    a=i*math.tau/64;b=(i+1)*math.tau/64-.006
    bevel(wedge('VIS_Balcony_Stone',5.5,6.45,a,b,9.81,.24,'Coast_Masonry'),.018,2)
for o in list(world.children):
    if o.name.startswith('SOLID_Stair_'):
        idx=int(o.name.rsplit('_',1)[1]);a=(idx+1)*math.tau/30;z=4.46+(idx+1)*.18+.008
        bar('VIS_Tread_Nosing',(1.48*math.cos(a),1.48*math.sin(a),z),(3.06*math.cos(a),3.06*math.sin(a),z),.018,'Coast_Brass')

# Standing-seam copper roof and detailed weather vane.
bpy.ops.mesh.primitive_cone_add(vertices=128,radius1=6.04,radius2=.68,depth=2.2,location=(0,0,13.86))
o=bpy.context.object;o.name='VIS_Copper_Roof';put(o,'Coast_Copper',world)
for p in o.data.polygons:p.use_smooth=True
for i in range(48):
    a=i*math.tau/48
    bar('VIS_Roof_Standing_Seam',(6.04*math.cos(a),6.04*math.sin(a),12.77),(.68*math.cos(a),.68*math.sin(a),14.98),.025,'Coast_Copper')
for z,r in ((12.70,5.85),(12.81,6.04),(14.98,.67)):torus('VIS_Copper_Drip_Edge',(0,0,z),r,.065,'Coast_Copper')
cyl('VIS_Roof_Vent',(0,0,15.1),.46,.34,'Coast_Iron')
for z in (14.99,15.1,15.21):torus('VIS_Vent_Louvre',(0,0,z),.49,.028,'Coast_Brass')
bar('VIS_Vane_Arrow',(-.7,0,15.92),(.8,0,15.92),.022,'Coast_Brass')
mesh('VIS_Vane_Fletching',[(-.75,0,15.7),(-.75,0,16.14),(-.25,0,15.92)],[(0,1,2)],'Coast_Brass')
mesh('VIS_Vane_Tip',[(.65,0,15.75),(.9,0,15.92),(.65,0,16.09)],[(0,1,2)],'Coast_Brass')

# Door boards, straps, bolt heads, and a bronze handle keep the existing hinge pivot.
door=bpy.data.objects['STATION_Door']
for i in range(7):cube('VIS_Door_Board',(.15+i*.273,-.089,1.24),(.263,.038,2.36),'Coast_Red',door,.012)
for z in (.38,1.93):
    cube('VIS_Door_Strap',(.91,-.126,z),(1.75,.038,.10),'Coast_Iron',door,.015)
    for x in (.18,.55,1.25,1.68):
        o=cyl('VIS_Door_Rivet',(x,-.157,z),.023,.025,'Coast_Brass',door,12);o.rotation_euler.x=math.pi/2
torus('VIS_Door_Knocker',(1.55,-.17,1.3),.10,.016,'Coast_Brass',door).rotation_euler.x=math.pi/2

# Keeper's lamp: Fresnel rings, gauges, fuel pipes and pump linkage.
lamp=bpy.data.objects['STATION_Lamp'];lens=bpy.data.objects['ANIM_Lens']
for z in np.linspace(-.52,.52,19):torus('VIS_Fresnel_Glass_Ring',(0,0,float(z)),.60+.07*(1-abs(z)/.6),.017,'Glass',lens)
for z in (.12,.35,.90,1.12):torus('VIS_Lamp_Moulding',(0,0,z),.68 if z<.5 else .80,.035,'Coast_Brass',lamp)
for i in range(16):
    a=i*math.tau/16;cyl('VIS_Tank_Bolt',(.73*math.cos(a),.73*math.sin(a),1.13),.026,.035,'Coast_Iron',lamp,12)
bar('VIS_Fuel_Pipe',(.55,.25,.3),(.55,.25,1.4),.045,'Coast_Brass',lamp)
gauge=cyl('VIS_Pressure_Gauge',(-.4,-.64,1.05),.16,.08,'Coast_Brass',lamp);gauge.rotation_euler.x=math.pi/2
face=cyl('VIS_Gauge_Face',(-.4,-.691,1.05),.137,.012,'Coast_Linen',lamp);face.rotation_euler.x=math.pi/2
for i in range(11):
    a=math.radians(-35+i*25)
    bar('VIS_Gauge_Tick',(-.4+math.cos(a)*.10,-.702,1.05+math.sin(a)*.10),(-.4+math.cos(a)*.123,-.702,1.05+math.sin(a)*.123),.004,'Coast_Iron',lamp)
bar('VIS_Gauge_Needle',(-.4,-.71,1.05),(-.47,-.71,1.13),.006,'Coast_Red',lamp)
text('Lamp_Name','ОПТИКА 07',(0,-.68,.47),.085,'Coast_Brass',lamp)

# Radio becomes a readable instrument, rather than a luminous cube.
radio=bpy.data.objects['STATION_Radio']
display=bpy.data.objects['RadioDisplay'];put(display,'Coast_Iron',radio)
for i in range(16):cube('VIS_Radio_Vent',(-.46+i*.06,-.285,1.46),(.022,.02,.10),'Coast_Iron',radio,.003)
cube('VIS_Radio_Dial_Face',(0,-.295,1.29),(.59,.018,.20),'Coast_Linen',radio,.006)
for i in range(25):cube('VIS_Frequency_Tick',(-.27+i*.022,-.309,1.31),(.004,.008,.045 if i%5 else .075),'Coast_Iron',radio,.001)
cube('VIS_Tuning_Needle',(.11,-.318,1.29),(.008,.01,.15),'Coast_Red',radio,.001)
text('Radio_Frequency','88  96  104  112',(0,-.316,1.245),.032,'Coast_Iron',radio)
text('Radio_Name','СВЯЗЬ СО ШТАБОМ',(0,-.29,1.60),.067,'Coast_Brass',radio)
for sign in (-1,1):
    dial=bpy.data.objects['ANIM_RadioDial_'+str(sign*.45)]
    # Detail is local to the original rotating dial, so it follows the animation.
    for i in range(20):
        a=i*math.tau/20;cyl('VIS_Dial_Knurl',(.101*math.cos(a),.101*math.sin(a),.035),.009,.025,'Coast_Iron',dial,n=8,b=0)
    cube('VIS_Dial_Marker',(0,.065,.038),(.012,.045,.009),'Coast_Linen',dial,.003)
for x in (-.53,.53):
    for z in (1.03,1.5):
        bolt=cyl('VIS_Radio_Screw',(x,-.293,z),.015,.012,'Coast_Brass',radio,12);bolt.rotation_euler.x=math.pi/2
paper=bpy.data.objects['ANIM_ReportPaper'];put(paper,'Coast_Paper',radio)
for i in range(7):cube('VIS_Report_Line',(.55,-.42+.02*i,.015),(.30,.002,.001),'Coast_Iron',paper,b=0).location=(0,-.14+i*.04,.017)
text('Report_Header','ЖУРНАЛ ВАХТЫ',(0,-.10,.019),.038,'Coast_Iron',paper,rotation=(0,0,0))
bar('VIS_Radio_Cable',(.5,.24,1.1),(.59,.35,.45),.017,'Coast_Iron',radio)
cyl('VIS_Mug',(-.61,-.24,1.04),.075,.17,'Coast_Linen',radio)
torus('VIS_Mug_Handle',(-.71,-.24,1.05),.047,.012,'Coast_Linen',radio).rotation_euler.x=math.pi/2

# Soft linen and a folded, draped navy blanket with its original animated transform.
bed=bpy.data.objects['STATION_Bed'];blanket=bpy.data.objects['ANIM_Blanket']
verts=[];faces=[];nx=33;ny=25
for i in range(nx):
    for j in range(ny):
        x=(i/(nx-1)-.5)*1.5;y=(j/(ny-1)-.5)*1.14
        z=.055+math.sin(x*23+y*6)*.011+math.sin(y*35)*.006-max(0,abs(y)-.44)*1.9
        verts.append((x,y,z))
for i in range(nx-1):
    for j in range(ny-1):a=i*ny+j;faces.append((a,a+ny,a+ny+1,a+1))
old=blanket.data;me=bpy.data.meshes.new('Draped blanket');me.from_pydata(verts,[],faces);me.update();blanket.data=me;put(blanket,'Coast_Cloth',bed)
for p in me.polygons:p.use_smooth=True
mod=blanket.modifiers.new('Cloth thickness','SOLIDIFY');mod.thickness=.012;bpy.context.view_layer.objects.active=blanket;bpy.ops.object.modifier_apply(modifier=mod.name)
for x in (-1.12,1.12):
    for y in (-.51,.51):cyl('VIS_Bed_Corner_Post',(x,y,.5),.045,1,'Coast_Iron',bed)
    for z in (.65,.95):bar('VIS_Bed_Headrail',(x,-.51,z),(x,.51,z),.03,'Coast_Iron',bed)
for y in np.linspace(-.38,.38,7):bar('VIS_Bed_Spindle',(-1.12,float(y),.65),(-1.12,float(y),.95),.012,'Coast_Iron',bed)

# Optical telescope fittings follow its yaw pivot.
swivel=bpy.data.objects['ANIM_Telescope']
for y,r in ((-.37,.13),(-.24,.17),(.55,.175),(.70,.185)):
    torus('VIS_Telescope_Collar',(0,y,0),r,.018,'Coast_Brass',swivel).rotation_euler.x=math.pi/2
for i in range(24):
    a=i*math.tau/24;bar('VIS_Focus_Knurl',(.17*math.cos(a),-.26,.17*math.sin(a)),(.17*math.cos(a),-.17,.17*math.sin(a)),.005,'Coast_Iron',swivel)

# Interior structure and a few purposeful props, kept out of the tested route.
for i in range(12):
    a=i*math.tau/12
    bar('VIS_Roof_Rafter',(.9*math.cos(a),.9*math.sin(a),14.4),(5.33*math.cos(a),5.33*math.sin(a),12.60),.075,'Coast_Wood')
    for z in (10.15,12.45):torus('VIS_Window_Post_Collar',(5.35*math.cos(a),5.35*math.sin(a),z),.095,.02,'Coast_Brass')
for j in range(3):
    cube('VIS_Supply_Chest',(-4.5,-1.4+j*.55,10.13),(.62,.48,.5),'Coast_Wood')
    for x in (-4.7,-4.3):cube('VIS_Chest_Strap',(x,-1.4+j*.55,10.39),(.055,.5,.025),'Coast_Iron')
cube('VIS_Chart_Frame',(4.57,1.2,10.9),(.07,1.65,1.05),'Coast_Wood')
cube('VIS_Chart_Paper',(4.52,1.2,10.9),(.012,1.5,.91),'Coast_Paper')
for i in range(6):bar('VIS_Chart_Grid',(4.51,.48+i*.28,10.49),(4.51,.48+i*.28,11.3),.002,'Coast_Iron')
for i in range(4):bar('VIS_Chart_Grid',(4.51,.48,10.5+i*.25),(4.51,1.93,10.5+i*.25),.002,'Coast_Iron')
for r in (.15,.28,.40):
    o=torus('VIS_Chart_Compass',(4.50,1.2,10.9),r,.002,'Coast_Red');o.rotation_euler.y=math.pi/2

# Upgrade the foreground camera as part of the keeper's equipment.
for o in list(camera.children):
    if o.type=='MESH':
        old=o.data.materials[0].name if o.data.materials else ''
        if old in ('Iron','Wood','Brass','Red','Ivory'):put(o,{'Iron':'Coast_Iron','Wood':'Coast_Wood','Brass':'Coast_Brass','Red':'Coast_Red','Ivory':'Coast_Linen'}[old],camera)
        bevel(o,.006,3)
for y in (-.12,-.16,-.20):torus('VIS_Camera_Lens_Ring',(0,y,0),.099,.006,'Coast_Brass',camera).rotation_euler.x=math.pi/2
for x in (-.13,.12):
    for z in (-.08,.08):
        o=cyl('VIS_Camera_Screw',(x,.087,z),.008,.008,'Coast_Brass',camera,12);o.rotation_euler.x=math.pi/2
text('Camera_Badge','07',(-.10,.086,.005),.035,'Coast_Brass',camera,rotation=(-math.pi/2,0,0))

# UVs at a consistent physical scale, including custom meshes and rounded edges.
bpy.context.view_layer.update()
for root in (world,camera):
    for o in root.children_recursive:
        if o.type!='MESH' or o.name.startswith('COLL_'):continue
        if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
        uv=o.data.uv_layers.active.data
        for poly in o.data.polygons:
            n=poly.normal;axis=max(range(3),key=lambda i:abs(n[i]));axes=[i for i in range(3) if i!=axis]
            for li in poly.loop_indices:
                v=o.data.vertices[o.data.loops[li].vertex_index].co
                # Grain follows long local faces; the overall texel density is 512 px/m.
                uv[li].uv=(v[axes[0]]*.5,v[axes[1]]*.5)

# Merge static decorative pieces per parent/material; keep all named gameplay pivots.
for parent in [world]+[o for o in world.children_recursive if o.type=='EMPTY']+[camera]:
    buckets={}
    for o in list(parent.children):
        if o.type=='MESH' and o.name.startswith('VIS_') and o.name!='VIS_LightBeam':buckets.setdefault(o.data.materials[0].name,[]).append(o)
    for mat,items in buckets.items():
        if len(items)<2:continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in items:o.select_set(True)
        bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();items[0].name='VIS_Detail_'+mat

def export(root,name):
    bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
    for o in root.children_recursive:o.select_set(True)
    bpy.context.view_layer.objects.active=root;bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/name),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',path_mode='AUTO')
export(world,'LighthouseEnvironment.fbx');export(camera,'Camera.fbx')
camera.location=(0,-18,5)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Lighthouse_Coastal.blend'))
summary={'blender':bpy.app.version_string,'source':'ArtSource/Lighthouse_Coastal.blend','materials':list(recipes),'textures':len(recipes)*3,'texture_resolution':S,'environment_meshes':len([o for o in world.children_recursive if o.type=='MESH']),'triangles':sum(len(o.data.polygons)*2 for o in world.children_recursive if o.type=='MESH'),'route':'Original 30 risers and 37 markers retained','exports':['LighthouseEnvironment.fbx','Camera.fbx']}
(ROOT/'ArtSource/coastal-art-manifest.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print('COASTAL_ART_COMPLETE '+json.dumps(summary),flush=True)
