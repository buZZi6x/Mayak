"""Weeping girl V2: MakeHuman CC0 human anatomy, authored poses and clothing.

This is a project-local authoring script, not an installed character framework.
The licensed source data and its exact attribution live in MakeHumanCC0/.
No jump, scream-face, or outstretched attack pose is authored here.
"""
import bpy, math, json, random, sys, shutil
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree

ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'ArtSource/WeepingGirl'
DATA=SRC/'MakeHumanCC0'
OUT=SRC/'V2'
OUT.mkdir(exist_ok=True)
random.seed(20061114)

def V(x,f,z): return Vector((x,-f,z))
def lerp(a,b,t): return a*(1-t)+b*t
def smooth(t): t=max(0,min(1,t));return t*t*(3-2*t)
def curve(knots,t):
    if t<=knots[0][0]:return knots[0][1]
    for (a,av),(b,bv) in zip(knots,knots[1:]):
        if t<=b:return lerp(av,bv,smooth((t-a)/(b-a)))
    return knots[-1][1]
def read_obj(path):
    verts=[];uvs=[];groups={};group='body'
    for line in path.read_text().splitlines():
        a=line.split()
        if not a:continue
        if a[0]=='v':verts.append(Vector(tuple(map(float,a[1:4]))))
        elif a[0]=='vt':uvs.append(tuple(map(float,a[1:3])))
        elif a[0]=='g':group=a[1]
        elif a[0]=='f':
            vs=[];us=[]
            for term in a[1:]:
                parts=term.split('/');vs.append(int(parts[0])-1);us.append(int(parts[1])-1 if len(parts)>1 and parts[1] else 0)
            groups.setdefault(group,[]).append((vs,us))
    return verts,uvs,groups

raw,raw_uv,groups=read_obj(DATA/'base.obj')
for line in (DATA/'caucasian-female-young.target').read_text().splitlines():
    a=line.split()
    if a and not a[0].startswith('#'):raw[int(a[0])]+=Vector(tuple(map(float,a[1:4])))
body_ids=sorted({i for f,u in groups['body'] for i in f})
miny=min(raw[i].y for i in body_ids);maxy=max(raw[i].y for i in body_ids)
SCALE=1.635/(maxy-miny)
# MakeHuman uses Y up and +Z forward. Blender uses Z up and -Y forward.
base=[V(v.x*SCALE,v.z*SCALE,(v.y-miny)*SCALE) for v in raw]
rig_data=json.loads((DATA/'default.mhskel').read_text())
weights_data=json.loads((DATA/'default_weights.mhw').read_text())['weights']
def joint(name):
    ids=rig_data['joints'][name];return sum((base[i] for i in ids),Vector())/len(ids)
def bh(name):return joint(rig_data['bones'][name]['head'])
def bt(name):return joint(rig_data['bones'][name]['tail'])

REST={
 'Pelvis':(bh('spine05'),bt('spine05')),
 'SpineLower':(bh('spine05'),bh('spine03')),
 'SpineMid':(bh('spine03'),bh('spine01')),
 'Chest':(bh('spine01'),bh('neck01')),
 'Neck':(bh('neck01'),bh('head')),
 'Head':(bh('head'),bt('head')),
}
for s in ('L','R'):
    REST['UpperArm.'+s]=(bh('upperarm01.'+s),bt('upperarm02.'+s))
    REST['Forearm.'+s]=(bh('lowerarm01.'+s),bt('lowerarm02.'+s))
    REST['Hand.'+s]=(bh('wrist.'+s),bh('finger3-1.'+s))
    REST['Thigh.'+s]=(bh('upperleg01.'+s),bt('upperleg02.'+s))
    REST['Shin.'+s]=(bh('lowerleg01.'+s),bt('lowerleg02.'+s))
    REST['Foot.'+s]=(bh('foot.'+s),bt('foot.'+s))

def group_for(name):
    side='L' if name.endswith('.L') else 'R'
    if name.startswith('upperarm'):return 'UpperArm.'+side
    if name.startswith('lowerarm'):return 'Forearm.'+side
    if name.startswith(('finger','metacarpal','wrist')):return 'Hand.'+side
    if name.startswith('upperleg'):return 'Thigh.'+side
    if name.startswith('lowerleg'):return 'Shin.'+side
    if name.startswith(('foot','toe')):return 'Foot.'+side
    if name.startswith(('neck',)):return 'Neck'
    if name in ('spine04','spine05'):return 'SpineLower'
    if name in ('spine02','spine03'):return 'SpineMid'
    if name.startswith(('spine01','breast','clavicle','shoulder')):return 'Chest'
    if name=='root' or name.startswith('pelvis'):return 'Pelvis'
    return 'Head'
weights=[{} for i in base]
for name,values in weights_data.items():
    mapped=group_for(name)
    for i,w in values:weights[i][mapped]=weights[i].get(mapped,0)+w
for ws in weights:
    total=sum(ws.values())
    if total:
        for key in ws:ws[key]/=total
for i,p in enumerate(base):
    if p.z<.13:weights[i]={'Foot.L' if p.x>=0 else 'Foot.R':1}

def frame(a,b):
    y=(b-a).normalized();hint=Vector((1,0,0))
    if abs(y.dot(hint))>.92:hint=Vector((0,0,1))
    z=hint.cross(y).normalized();x=y.cross(z).normalized()
    m=Matrix((x,y,z)).transposed().to_4x4();m.translation=a;return m
REST_FRAMES={n:frame(*ends) for n,ends in REST.items()}
def segment_map(a,b,c,d):
    rotation=(b-a).normalized().rotation_difference((d-c).normalized()).to_matrix().to_4x4()
    return Matrix.Translation(c)@rotation@Matrix.Translation(-a)
def two_bone(a,b,l1,l2,pole):
    delta=b-a;dist=max(.00001,min(delta.length,l1+l2-.0001));axis=delta.normalized()
    proj=(l1*l1-l2*l2+dist*dist)/(2*dist);height=math.sqrt(max(0,l1*l1-proj*proj))
    bend=(pole-a)-axis*(pole-a).dot(axis)
    if bend.length<.001:bend=axis.cross(Vector((1,0,0)))
    return a+axis*proj+bend.normalized()*height
def pitch(degrees):return Matrix.Rotation(math.radians(degrees),4,'X')

HIP=bh('spine05')
def pose(t,sob=0,look=0,yaw=0):
    # Rest feet do not move. The body transfers mass forward before rising.
    rise=curve([(0,0),(.22,.015),(.42,.20),(.70,.76),(1,1)],t)
    lean=curve([(0,23),(.24,33),(.48,28),(.76,12),(1,1.5)],t)
    drop=HIP.z-.445
    hip=HIP+V(0,-.34*(1-rise),-drop*(1-rise))
    forward_rot=pitch(lean)
    chest_rot=pitch(curve([(0,22),(.25,32),(.55,23),(1,1.5)],t))
    target={}
    pl_a,pl_b=REST['Pelvis'];target['Pelvis']=(hip,hip+forward_rot@(pl_b-pl_a))
    cursor=hip
    for n in ('SpineLower','SpineMid','Chest'):
        delta=REST[n][1]-REST[n][0]
        end=cursor+chest_rot@delta
        if n=='Chest':end+=V(0,.002*sob,.003*sob)
        target[n]=(cursor,end);cursor=end
    neck_a=cursor+V(0,0,sob*.002)
    neck_rot=pitch(curve([(0,29),(.30,35),(.72,20),(1,7)],t)-look)
    neck_b=neck_a+neck_rot@(REST['Neck'][1]-REST['Neck'][0])
    target['Neck']=(neck_a,neck_b)
    head_rot=pitch(curve([(0,41),(.32,43),(.76,25),(1,7)],t)-look)
    target['Head']=(neck_b,neck_b+head_rot@(REST['Head'][1]-REST['Head'][0]))
    head_map=segment_map(*REST['Head'],*target['Head'])
    chest_map=segment_map(*REST['Chest'],*target['Chest'])
    hip_map=segment_map(*REST['Pelvis'],*target['Pelvis'])
    for side in ('L','R'):
        sign=1 if side=='L' else -1
        thigh='Thigh.'+side;shin='Shin.'+side;foot='Foot.'+side
        h=hip_map@REST[thigh][0];ankle=REST[shin][1].copy()
        k=two_bone(h,ankle,(REST[thigh][1]-REST[thigh][0]).length,(REST[shin][1]-REST[shin][0]).length,h+V(sign*.04,1,.3))
        target[thigh]=(h,k);target[shin]=(k,ankle);target[foot]=REST[foot]
        shoulder=chest_map@REST['UpperArm.'+side][0]
        # Left palm shields the face; the other shares the weight on the knee.
        face= head_map @ (REST['Head'][0]+V(sign*.055,.12,.012))
        wrist_face=face+V(sign*.015,.052,-.108)
        wrist_knee=k+V(sign*.020,.170,.110)
        wrist_rest=V(sign*.225,.020,.795)
        wrist_on_leg=lerp(wrist_face,wrist_knee,smooth(t/.34)) if side=='L' else wrist_knee
        wrist_target=lerp(wrist_on_leg,wrist_rest,smooth((t-.38)/.49))
        l1=(REST['UpperArm.'+side][1]-REST['UpperArm.'+side][0]).length
        l2=(REST['Forearm.'+side][1]-REST['Forearm.'+side][0]).length
        d=wrist_target-shoulder
        if d.length>l1+l2-.015:wrist_target=shoulder+d.normalized()*(l1+l2-.015)
        elbow=two_bone(shoulder,wrist_target,l1,l2,shoulder+V(sign*.45,.12,-.5))
        target['UpperArm.'+side]=(shoulder,elbow);target['Forearm.'+side]=(elbow,wrist_target)
        # Rotate through a forward arc. Normalized interpolation of nearly
        # opposite up/down vectors flips the hand around halfway through.
        initial_angle=-.12 if side=='L' else math.atan2(.65,-.7)
        hand_angle=lerp(initial_angle,math.pi-.03,smooth(t/.42))
        handdir=V(-sign*.03*(1-smooth(t/.42)),math.sin(hand_angle),math.cos(hand_angle)).normalized()
        handlen=(REST['Hand.'+side][1]-REST['Hand.'+side][0]).length
        target['Hand.'+side]=(wrist_target,wrist_target+handdir*handlen)
    matrices={name:segment_map(*REST[name],*ends) for name,ends in target.items()}
    for side in ('L','R'):
        hand='Hand.'+side
        wrist,end=target[hand]
        # The palms face inward while she covers her face; the dorsal side,
        # not a stop gesture, is visible to the player.
        roll=math.pi*(1-smooth(t/.50)) if side=='L' else 0
        twist=Matrix.Rotation(roll,4,(end-wrist).normalized())
        matrices[hand]=Matrix.Translation(wrist)@twist@Matrix.Translation(-wrist)@matrices[hand]
    if yaw:
        for n,amount in [('Head',1),('Neck',.5)]:
            rotate=Matrix.Rotation(math.radians(yaw*amount),4,'Z')
            matrices[n]=Matrix.Translation(neck_a)@rotate@Matrix.Translation(-neck_a)@matrices[n]
    return target,matrices

POSE_TIMES=[i/8 for i in range(9)]
POSES=[pose(t) for t in POSE_TIMES]
def skin(point,ws,matrices):
    if not ws:return point.copy()
    return sum((matrices[name]@point*w for name,w in ws.items()),Vector())

bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
materials={}
for n,c,r in [('Girl_Cloth',(.31,.34,.33,1),.98),('Girl_Hair',(.012,.013,.014,1),.81),('Girl_Skin',(.38,.40,.38,1),.91),('Girl_Dark',(.016,.018,.020,1),.95),('Girl_Eyes',(.26,.29,.28,1),.4)]:
    m=bpy.data.materials.new(n);m.diffuse_color=c;m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=c;p.inputs['Roughness'].default_value=r
    materials[n]=m
mat_index={n:i for i,n in enumerate(materials)}
VERTS=[];FACES=[];MATS=[];UVS=[];W=[];POSEVERTS=[[] for i in range(9)]
def add_mesh(points,faces,mat,ws=None,uv=None,posed=None):
    start=len(VERTS)
    VERTS.extend(points)
    if ws is None:ws=[{'Head':1} for p in points]
    W.extend(ws)
    for k in range(9):POSEVERTS[k].extend(posed[k] if posed else [skin(p,w,POSES[k][1]) for p,w in zip(points,ws)])
    for j,f in enumerate(faces):
        FACES.append(tuple(start+i for i in f));MATS.append(mat_index[mat]);UVS.append(uv[j] if uv else [(0,0)]*len(f))
def subset(group,material,filter_face=None,modify=None):
    source=groups[group];selected=[]
    for f,u in source:
        if not filter_face or filter_face(f):selected.append((f,u))
    used=sorted({i for f,u in selected for i in f});mapping={old:new for new,old in enumerate(used)}
    points=[modify(base[i],i) if modify else base[i] for i in used]
    ff=[[mapping[i] for i in f] for f,u in selected]
    uv=[[raw_uv[i] for i in u] for f,u in selected]
    add_mesh(points,ff,material,[weights[i] for i in used],uv)

# Only exposed anatomy is retained: natural face, neck, wrists, hands and feet.
def exposed(f):
    center=sum((base[i] for i in f),Vector())/len(f)
    ws={}
    for i in f:
        for n,w in weights[i].items():ws[n]=ws.get(n,0)+w/len(f)
    return center.z>1.340 or sum(w for n,w in ws.items() if n.startswith('Hand'))>.40 or center.z<.20
subset('body','Girl_Skin',exposed)

# Authored skirt panels: four large gravity folds and smaller seams, a weighted
# hem with no saw-tooth points. Intermediate panels follow the actual knee pose.
NR=45;NS=80
skirt_faces=[]
for j in range(NR-1):
    for i in range(NS):skirt_faces.append((j*NS+i,j*NS+(i+1)%NS,(j+1)*NS+(i+1)%NS,(j+1)*NS+i))
def skirt_points(k):
    t=POSE_TIMES[k];targets,mats=POSES[k]
    pelvis=targets['Pelvis'][0]
    knees=[targets['Thigh.'+s][1] for s in ('L','R')]
    knee=sum(knees,Vector())/2
    waist=mats['SpineLower']@V(0,0,.955)
    standing=curve([(0,0),(.28,.04),(.52,.35),(.8,.84),(1,1)],t)
    result=[]
    for j in range(NR):
        u=j/(NR-1)
        if u<.37:
            q=smooth(u/.37)
            center=waist.lerp(knee+V(0,.020,.085),q)
            rx=lerp(.160,.225,q);depth=lerp(.154,.135,q)
        else:
            q=(u-.37)/.63
            # The cloth turns over the knees, then drops towards the feet.
            # A forward-flared hem hid this bend from the pier camera.
            center=(knee+V(0,.020,.085)).lerp(V(0,-knee.y-.045,.095),smooth(q))
            rx=lerp(.225,.260,q);depth=lerp(.135,.160,q)
        stand=waist.lerp(V(0,.005,.09),u)
        center=center.lerp(stand,standing)
        rx=lerp(rx,lerp(.155,.255,u),standing)
        depth=lerp(depth,lerp(.154,.165,u),standing)
        for i in range(NS):
            a=i/NS*2*math.pi
            pleat=(.005+.008*u)*math.sin(10*a+.12*math.sin(u*4))+.0025*math.sin(18*a+.6)
            x=(rx+pleat)*math.sin(a)
            f=(depth+pleat*.55)*math.cos(a)
            z=.0025*math.sin(a*3+u*5)*smooth(u)
            offset=V(x,f,z)
            # Waist cross-section follows the torso exactly. By the knees
            # the heavy cloth hangs vertically, without an open waist seam.
            waist_rot=mats['SpineLower'].to_3x3()@offset
            offset=waist_rot.lerp(offset,smooth(u/.25))
            result.append(center+offset)
    return result
skirtposes=[skirt_points(k) for k in range(9)]

# A single loose nightdress, built independently of breast/abdomen anatomy.
# Sleeves are welded into the shoulder volume before skinning. Closed cuff
# caps intersect the wrist cleanly, so there are no cut-face jagged boundaries.
clothpoints=[];clothfaces=[]
def closed_loft(rings):
    start=len(clothpoints);ns=len(rings[0])
    for ring in rings:clothpoints.extend(ring)
    for j in range(len(rings)-1):
        for i in range(ns):clothfaces.append((start+j*ns+i,start+j*ns+(i+1)%ns,start+(j+1)*ns+(i+1)%ns,start+(j+1)*ns+i))
    clothfaces.append(tuple(start+i for i in reversed(range(ns))))
    clothfaces.append(tuple(start+(len(rings)-1)*ns+i for i in range(ns)))
profile=[(.09,.255,.165),(.3,.232,.162),(.6,.199,.159),(.955,.160,.154),(1.02,.177,.148),(1.12,.190,.147),(1.23,.195,.145),(1.30,.177,.122),(1.34,.145,.096),(1.375,.095,.070),(1.402,.070,.061)]
rings=[]
for j in range(85):
    z=lerp(.09,1.402,j/84)
    for (za,xa,da),(zb,xb,db) in zip(profile,profile[1:]):
        if za<=z<=zb:
            q=smooth((z-za)/(zb-za));rx=lerp(xa,xb,q);depth=lerp(da,db,q);break
    ring=[]
    for i in range(80):
        a=i/80*2*math.pi
        amp=lerp(.012,.003,smooth((z-.10)/1.25))
        fold=amp*math.sin(10*a+.12*math.sin(z*5))+.0016*math.sin(17*a+z*8)
        ring.append(V((rx+fold)*math.sin(a),(depth+fold*.55)*math.cos(a),z))
    rings.append(ring)
closed_loft(rings)
for side in ('L','R'):
    a,b=REST['UpperArm.'+side];c=REST['Forearm.'+side][1]
    sleeve=[]
    tangent=(b-a).normalized()
    axis=Vector((0,1,0));axis=(axis-tangent*axis.dot(tangent)).normalized();other=tangent.cross(axis).normalized()
    # Rounded sleeve crown continues inside the shoulder; a flat cap here
    # turns into a visible upward shoulder pad when the arm hangs down.
    for cap in range(7):
        theta=(cap+.15)/6.15*math.pi*.5
        center=a-tangent*(.062*math.cos(theta))
        radius=.071*math.sin(theta)
        sleeve.append([center+(axis*math.cos(i/40*math.tau)+other*math.sin(i/40*math.tau))*radius for i in range(40)])
    for j in range(36):
        u=j/35
        if u<.50:
            q=u/.50;center=a.lerp(b,q);tangent=(b-a).normalized();radius=lerp(.071,.061,smooth(q))
        else:
            q=(u-.50)/.50;center=b.lerp(c,q);tangent=(c-b).normalized();radius=lerp(.061,.041,smooth(q))
        axis=Vector((0,1,0));axis=(axis-tangent*axis.dot(tangent)).normalized();other=tangent.cross(axis).normalized()
        ring=[]
        for i in range(40):
            ang=i/40*2*math.pi
            ripple=.0024*math.sin(6*ang+u*2)*math.sin(math.pi*u)
            ring.append(center+(axis*math.cos(ang)+other*math.sin(ang))*(radius+ripple))
        sleeve.append(ring)
    closed_loft(sleeve)
cm=bpy.data.meshes.new('Nightdress union');cm.from_pydata(clothpoints,[],clothfaces);cm.update()
co=bpy.data.objects.new('Nightdress authoring shell',cm);bpy.context.collection.objects.link(co)
bpy.context.view_layer.objects.active=co;co.select_set(True)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
co.data.remesh_voxel_size=.009;bpy.ops.object.voxel_remesh()
sm=co.modifiers.new('Soft woven volume','SMOOTH');sm.factor=.65;sm.iterations=7;bpy.ops.object.modifier_apply(modifier=sm.name)
# Nearest anatomic weights only drive the upper garment. The skirt is sampled
# from a separately art-directed gravity drape, with one continuous boundary.
kd=KDTree(len(body_ids))
for index in body_ids:kd.insert(base[index],index)
kd.balance()
cp=[v.co.copy() for v in co.data.vertices]
cf=[tuple(p.vertices) for p in co.data.polygons]
cw=[];cposes=[[] for _ in range(9)]
for p in cp:
    nearest=kd.find_n(p,4);total=sum(1/(dist+.006)**2 for _,_,dist in nearest);ws={}
    for _,index,dist in nearest:
        factor=(1/(dist+.006)**2)/total
        for n,w in weights[index].items():
            # Cloth follows the thorax at the collar, and the forearm at the
            # cuff. Facial/finger rotations must not tug spikes out of it.
            if n in ('Head','Neck'):n='Chest'
            if n.startswith('Hand.'):n=n.replace('Hand.','Forearm.')
            ws[n]=ws.get(n,0)+w*factor
    cw.append(ws)
    u=max(0,min(1,(.955-p.z)/.865))
    rx=lerp(.160,.255,u);depth=lerp(.154,.165,u)
    angle=math.atan2(p.x/rx,-p.y/depth)%(2*math.pi)
    col=angle/(2*math.pi)*NS;row=u*(NR-1);i=int(col);j=min(NR-2,int(row));fu=col-i;fv=row-j
    for k in range(9):
        points=skirtposes[k]
        s0=points[j*NS+i].lerp(points[j*NS+(i+1)%NS],fu)
        s1=points[(j+1)*NS+i].lerp(points[(j+1)*NS+(i+1)%NS],fu)
        skirtp=s0.lerp(s1,fv)
        upper=skin(p,ws,POSES[k][1])
        blend=1-smooth((p.z-.90)/.055)
        cposes[k].append(upper.lerp(skirtp,blend))
# Correct the folded fabric at elbows/armpits after skinning, with the same
# topology and a small local relaxation in every sampled pose.
neighbors=[set() for _ in cp]
for face in cf:
    for a,b in zip(face,face[1:]+face[:1]):neighbors[a].add(b);neighbors[b].add(a)
for posepoints in cposes:
    for iteration in range(7):
        relaxed=[]
        for i,p in enumerate(posepoints):
            if cp[i].z<.20 or not neighbors[i]:relaxed.append(p);continue
            average=sum((posepoints[j] for j in neighbors[i]),Vector())/len(neighbors[i])
            relaxed.append(p.lerp(average,.42))
        posepoints[:]=relaxed
add_mesh(cp,cf,'Girl_Cloth',cw,posed=cposes)
bpy.data.objects.remove(co,do_unlink=True)

# Licensed strand cards. The file itself explicitly declares CC BY 4.0 by
# 04saken, even though its containing download pack is named hair01_cc0.
# Preserve the stricter explicit attribution instead of relabelling it CC0.
HAIR=DATA/'hair/o4saken_long01'
hairraw,hairuv,hairgroups=read_obj(HAIR/'o4saken_long01.obj')
proxy=[];scales=[1,1,1];reading=False
for line in (HAIR/'o4saken_long01.mhclo').read_text().splitlines():
    terms=line.split()
    if not terms:continue
    if terms[0] in ('x_scale','y_scale','z_scale'):
        axis={'x_scale':0,'y_scale':1,'z_scale':2}[terms[0]]
        scales[axis]=abs(raw[int(terms[1])][axis]-raw[int(terms[2])][axis])/float(terms[3])
    elif terms[0]=='verts':reading=True
    elif reading:
        if len(terms)==9 and terms[0].lstrip('-').isdigit():
            ids=list(map(int,terms[:3]));ws=list(map(float,terms[3:6]));off=list(map(float,terms[6:9]))
            p=sum((raw[i]*w for i,w in zip(ids,ws)),Vector())+Vector(tuple(off[a]*scales[a] for a in range(3)))
            proxy.append(V(p.x*SCALE,p.z*SCALE,(p.y-miny)*SCALE))
        elif len(terms)==1 and terms[0].isdigit():proxy.append(base[int(terms[0])])
        else:reading=False
assert len(proxy)==len(hairraw),(len(proxy),len(hairraw))
for p in proxy:
    if p.z<1.42:
        rx=.19;depth=.155
        length=math.sqrt((p.x/rx)**2+(p.y/depth)**2)
        if .01<length<1.10:
            amount=1-smooth((p.z-1.30)/.12)
            dilation=lerp(1,1.10/length,amount)
            p.x*=dilation;p.y*=dilation
hfaces=[];huvs=[]
for sourcefaces in hairgroups.values():
    for f,u in sourcefaces:hfaces.append(f);huvs.append([hairuv[i] for i in u])
hairweights=[]
for p in proxy:
    head=smooth((p.z-1.31)/.17)
    neck=(1-head)*smooth((p.z-1.19)/.16)
    chest=1-head-neck
    hairweights.append({n:w for n,w in [('Head',head),('Neck',neck),('Chest',chest)] if w>.0001})
add_mesh(proxy,hfaces,'Girl_Hair',hairweights,huvs)
shutil.copyfile(HAIR/'o4saken_long01.png',OUT/'Girl_Hair_BaseMap.png')
hairimage=bpy.data.images.load(str(OUT/'Girl_Hair_BaseMap.png'),check_existing=True)
hairimage.pack()
hairmaterial=materials['Girl_Hair'];nodes=hairmaterial.node_tree.nodes;links=hairmaterial.node_tree.links
tex=nodes.new('ShaderNodeTexImage');tex.image=hairimage
bsdf=nodes.get('Principled BSDF')
links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
cut=nodes.new('ShaderNodeMath');cut.operation='GREATER_THAN';cut.inputs[1].default_value=.38
links.new(tex.outputs['Alpha'],cut.inputs[0]);links.new(cut.outputs[0],bsdf.inputs['Alpha'])
bsdf.inputs['Roughness'].default_value=.88
hairmaterial.use_backface_culling=False

# Quiet eyes remain anatomically inside the natural eyelids. No glowing dots.
for group in ('helper-l-eye','helper-r-eye'):
    if group in groups:
        subset(group,'Girl_Eyes')
for side in ('L','R'):
    center=bh('eye.'+side)
    iris=[];faces=[]
    iris.append(center+V(0,.0162,0))
    for j in range(1,4):
        radius=.0058*j/3
        for i in range(32):
            a=i/32*math.tau
            iris.append(center+V(radius*math.cos(a),math.sqrt(.0162**2-radius**2),radius*math.sin(a)))
    for i in range(32):faces.append((0,1+i,1+(i+1)%32))
    for j in range(2):
        for i in range(32):faces.append((1+j*32+i,1+(j+1)*32+i,1+(j+1)*32+(i+1)%32,1+j*32+(i+1)%32))
    add_mesh(iris,faces,'Girl_Dark',[{'Head':1} for _ in iris])

mesh=bpy.data.meshes.new('WeepingGirl V2 human mesh')
mesh.from_pydata(VERTS,[],FACES);mesh.update()
girl=bpy.data.objects.new('WeepingGirl',mesh);bpy.context.collection.objects.link(girl)
for mat in materials.values():mesh.materials.append(mat)
for p,m in zip(mesh.polygons,MATS):p.material_index=m;p.use_smooth=True
uvlayer=mesh.uv_layers.new(name='Source and cloth UV')
for poly,uvs in zip(mesh.polygons,UVS):
    for loop,uv in zip(poly.loop_indices,uvs):uvlayer.data[loop].uv=uv

# Basis is the actual quiet standing pose, not an A-pose. Every Rise key is a
# full sampled skeletal pose relative to this Basis and shares identical topology.
standing=POSEVERTS[8]
for i,p in enumerate(standing):mesh.vertices[i].co=p
girl.shape_key_add(name='Basis')
for k in range(9):
    key=girl.shape_key_add(name='Rise_%02d'%k)
    for i,p in enumerate(POSEVERTS[k]):key.data[i].co=p
seated=girl.shape_key_add(name='Seated')
for i,p in enumerate(POSEVERTS[0]):seated.data[i].co=p
for name,look,yaw in [('LookUp',27,0),('LookDown',-17,0),('LookLeft',0,-55),('LookRight',0,55)]:
    key=girl.shape_key_add(name=name)
    mats=pose(1,look=look,yaw=yaw)[1]
    for i,(v,w) in enumerate(zip(VERTS,W)):
        key.data[i].co=standing[i]
        if any(n in ('Head','Neck') for n in w):key.data[i].co=skin(v,w,mats)
sobkey=girl.shape_key_add(name='Sob')
sobmats=pose(0,sob=1)[1]
for i,(v,w) in enumerate(zip(VERTS,W)):
    sobkey.data[i].co=standing[i]
    if any(n in ('Head','Neck','Chest') or n.startswith(('UpperArm','Forearm','Hand')) for n in w):
        sobkey.data[i].co=standing[i]+skin(v,w,sobmats)-skin(v,w,POSES[0][1])

# Keep an explicit editable bone rig with the true source weights in the .blend.
# The runtime export is the baked mesh only, so its blendshape contract is small.
armdata=bpy.data.armatures.new('Girl authoring skeleton')
arm=bpy.data.objects.new('AuthoringRig · excluded from export',armdata);bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,(a,b) in REST.items():
    bone=armdata.edit_bones.new(name);bone.head=a;bone.tail=b
bpy.ops.object.mode_set(mode='OBJECT');arm.hide_render=True;arm.hide_set(True)
for name in REST:
    vg=girl.vertex_groups.new(name=name)
    for i,w in enumerate(W):
        if name in w:vg.add([i],w[name],'REPLACE')
for k,(target,mats) in enumerate(POSES):
    for name in REST:
        pb=arm.pose.bones[name];pb.matrix=mats[name]@armdata.bones[name].matrix_local
        pb.keyframe_insert(data_path='location',frame=1+k*12)
        pb.keyframe_insert(data_path='rotation_quaternion',frame=1+k*12)
arm['Purpose']='Editable source skeleton. Runtime geometry is baked into Rise_00..08.'

root=bpy.data.objects.new('WeepingGirlRoot',None);bpy.context.collection.objects.link(root);girl.parent=root
face_rest=(bh('eye.L')+bh('eye.R'))*.5+V(0,.016,0)
seated_face=POSES[0][1]['Head']@face_rest
standing_face=POSES[8][1]['Head']@face_rest
seat_contact=V(0,-.34,.32)
for name,position in [('FaceForward',(0,-1,1)),('FootOrigin',(0,0,0)),('StandingCrown',(0,0,1.65)),('SeatedFace',seated_face),('StandingFace',standing_face),('SeatContact',seat_contact)]:
    obj=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(obj);obj.parent=root;obj.location=position;obj.empty_display_size=.04

# Neutral review lighting must reveal anatomy and intersections, without hiding
# them behind darkness, fog, bloom, screen-space grain or dramatic exposure.
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('Neutral review');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.11,.12,.13,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5;bpy.context.scene.world=world
for n,loc,color,energy,size in [('Key',(-3,-4,4),(1,.96,.91),310,4),('Fill',(3,-2,2),(.75,.83,1),140,3),('Rim',(1,3,3),(.8,.87,1),250,3)]:
    d=bpy.data.lights.new(n,'AREA');d.energy=energy;d.color=color;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(n,d);bpy.context.collection.objects.link(o);o.location=loc;aim(o,(0,0,.8))
camdata=bpy.data.cameras.new('Review camera');cam=bpy.data.objects.new('Review camera',camdata);bpy.context.collection.objects.link(cam)
scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=1200;scene.render.resolution_y=1400;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
cam.location=(2,-4,1.6);camdata.lens=75;aim(cam,(0,0,.85))
for o in bpy.context.selected_objects:o.select_set(False)
girl.select_set(True);bpy.context.view_layer.objects.active=girl
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')

def set_pose(k):
    for key in girl.data.shape_keys.key_blocks:key.value=0
    girl.data.shape_keys.key_blocks['Rise_%02d'%k].value=1
set_pose(8)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'WeepingGirlV2.blend'))
bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
for obj in root.children_recursive:obj.select_set(True)
bpy.context.view_layer.objects.active=girl
bpy.ops.export_scene.fbx(filepath=str(OUT/'WeepingGirlV2.fbx'),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,apply_unit_scale=True,bake_space_transform=True,bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type='FACE')
if '--seated-review' in sys.argv:
    set_pose(0);cam.location=(1.6,-3.3,1.40);camdata.lens=70;aim(cam,(0,0,.85))
    scene.render.filepath=str(OUT/'seated-drape-review.png');bpy.ops.render.render(write_still=True)
elif '--no-render' not in sys.argv:
    for k in (0,2,4,6,8):
        set_pose(k);cam.location=(1.6,-3.3,1.40);camdata.lens=70;aim(cam,(0,0,.85))
        scene.render.filepath=str(OUT/('rise_%02d.png'%k));bpy.ops.render.render(write_still=True)
    set_pose(8);cam.location=(.3,-.85,1.58);camdata.lens=82;aim(cam,(0,-.03,1.52));scene.render.filepath=str(OUT/'face.png');bpy.ops.render.render(write_still=True)
set_pose(8)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'WeepingGirlV2.blend'))
def unity(p):return [p.x,p.z,-p.y]
stats={'vertices':len(VERTS),'faces':len(FACES),'standing_bounds_blender_xyz':[[min(p[a] for p in standing),max(p[a] for p in standing)] for a in range(3)],'source_scale':SCALE,'shape_keys':[k.name for k in girl.data.shape_keys.key_blocks], 'poses':[], 'unity_markers':{'seated_face':unity(seated_face),'standing_face':unity(standing_face),'seat_contact_approximate':unity(seat_contact),'standing_feet_plane_y':0},'material_order':list(materials),'hair':{'file':'Girl_Hair_BaseMap.png','alpha_cutoff':.38,'double_sided':True,'license':'CC BY 4.0, 04saken; distributed in MakeHuman hair01 pack'},'animation_contract':{'duration_seconds':3.2,'method':'Clear all Rise weights. p = clamp01(progress)*8. Set floor(p) to (1-frac(p))*100, next key to frac(p)*100. Rise_08 matches Basis. Root stays still.','idle':'Seated or Rise_00 = 100. Sob is a small additive upper-body inhale; keep it 0 during the rise.','look':'LookUp +27 degrees, LookDown -17 degrees, standing relative keys. Only fade in after Rise_08.','no_attack':True}}
stats['animation_contract']['horizontal_look']='LookLeft -55 degrees and LookRight +55 degrees. Keep the actor root still; rotate only head/neck by one of these keys. Fade in after Rise_08, do not combine opposite keys.'
stats['animation_method']='Catmull-Rom cubic interpolation of four adjacent full authored Rise poses, matching WeepingGirlEncounter.Pose.'
stats['animation_contract']['method']='Clear all shape weights. f=clamp01(progress)*8; i=min(floor(f),7); t=f-i. Accumulate 100 times weights (-0.5t+t^2-0.5t^3, 1-2.5t^2+1.5t^3, 0.5t+2t^2-1.5t^3, -0.5t^2+0.5t^3) on indices (i-1,i,i+1,i+2), clamped to 0..8. Negative cubic weights are intentional. Rise_08 matches Basis; root stays still.'
for k,(targets,mats) in enumerate(POSES):
    stats['poses'].append({'name':'Rise_%02d'%k,'hip':unity(targets['Pelvis'][0]),'ankle_L':unity(targets['Shin.L'][1]),'ankle_R':unity(targets['Shin.R'][1]),'head':unity(targets['Head'][0]),'face':unity(mats['Head']@face_rest),'crown':max(p.z for p in POSEVERTS[k])})
(OUT/'asset-contract.json').write_text(json.dumps(stats,indent=2),encoding='utf8')
print('V2_DONE',json.dumps(stats))
