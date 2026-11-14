"""One parametric source -> browser VFX study and native Unity geometry. No network runtime."""
from pathlib import Path
import re, json
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Lighthouse/Assets/Scripts/Observations'; OUT.mkdir(parents=True,exist_ok=True)
# Equations use metres, seconds, u/v in [0,1], part index k. Shared verbatim with JS.
LEGACY_SPEC=[
dict(key='aurora',name='Шёлк полярного неба',points=120,duration=32,parts=3,nu=100,nv=14,color=[.18,1,.65],accent=[.65,.25,1],weather=[1,1,0,0,0],night=True,
 code='a=u*6.283; b=sin(u*9+t*.19+k);',xyz=['(u-.5)*260','52+v*67+12*b+5*sin(a+t*.13)','(k-1)*21+25*sin(u*8+t*.12)']),
dict(key='meteors',name='Звёздный дождь',points=150,duration=21,parts=9,nu=36,nv=2,color=[.45,.75,1],accent=[1,.55,.15],weather=[1,1,0,0,0],night=True,
 code='a=t*.18+k*.173; a=a-floor(a); b=a*2-1;',xyz=['58-b*110-u*19','48-b*20+u*8+k*1.8+(v-.5)*(.08+.4*(1-u))','(k-4)*5']),
dict(key='waterspout',name='Морской смерч',points=180,duration=29,parts=4,nu=80,nv=60,color=[.13,.15,.17],accent=[.38,.40,.42],weather=[0,1,0,1,3],night=False,
 code='a=u*6.283+v*13-t*1.2+k*2.1; b=1.8+pow(v,2.3)*23+k*.65+sin(a*3+v*15)*.6;',xyz=['cos(a)*b+sin(v*4+t*.18)*v*9','v*92+sin(a*4+t)*(v*v*3)','sin(a)*b']),
dict(key='rift',name='Разлом прилива',points=220,duration=24,parts=8,nu=180,nv=8,color=[.1,.72,1],accent=[.64,.19,1],weather=[0,1,2,1,2],night=False,
 code='a=u*6.283+t*(k-3.5)*.08; b=14+k*.8+sin(a*3+t+k)*1.1+(v-.5)*(1.5+k*.24);',xyz=['cos(a)*b','21+sin(a)*b','sin(u*15+t+k)*1.3+k*.3']),
dict(key='jellies',name='Небесные медузы',points=160,duration=30,parts=45,nu=36,nv=8,color=[.15,.92,1],accent=[.8,.3,1],weather=[1,1,2,0,0],night=True,
 code='a=k<5?k:floor((k-5)/8); b=k<5?u*6.283:((k-5)%8)*.7854; c=1+.12*sin(t*1.8+a); d=k<5?sin(v*1.571)*3.5*c:2.8*(1-u*.5); e=sin(a*2.4)*23; f=cos(a*2.4)*12;',xyz=['e+cos(b)*d+(k<5?0:sin(u*9+t*1.6+a)*u*1.8+(v-.5)*.1)','13+a*3+sin(t*.9+a)*1.2+(k<5?cos(v*1.571)*3.2*c:-u*11)','f+sin(b)*d+(k<5?0:cos(u*7+t+a)*u)']),
dict(key='lanterns',name='Огни потерянных моряков',points=110,duration=28,parts=30,nu=18,nv=12,color=[1,.51,.08],accent=[1,.12,.025],weather=[1,1,3,0,0],night=True,
 code='a=u*6.283; b=.36+sin(v*3.1416)*.35; c=sin(k*2.4)*33; d=cos(k*2.4)*17;',xyz=['c+cos(a)*b+sin(t*.2+k)*2','3+k*.72+sin(t*.7+k)*.5+v*1.35','d+sin(a)*b']),
dict(key='mantas',name='Призрачные скаты',points=190,duration=26,parts=6,nu=70,nv=28,color=[.18,.92,1],accent=[.25,.35,1],weather=[1,1,2,1,0],night=True,
 code='a=v*2-1; b=pow(sin(u*3.14159),.7); d=k<3?k:k-3; c=t*.22+d*2;',xyz=['sin(c)*14+(k<3?a*(1.2+b*(11-d)):sin(u*5+t)*u*1.2+a*.12*(1-u))','18+d*6+sin(c)*2+(k<3?sin(t*1.5+abs(a)*2+d)*pow(abs(a),1.6)*3.3+b*.9:sin(u*4+t)*u)','cos(c)*5+d*15+(k<3?(u-.5)*16+abs(a)*abs(a)*5:8+u*16)']),
dict(key='bloom',name='Дыхание океана',points=130,duration=25,parts=4,nu=180,nv=5,color=[.05,.8,.85],accent=[.1,.45,1],weather=[1,1,1,0,0],night=True,
 code='a=u*6.283+t*.12+k*1.57; b=6+u*22+k*3+(v-.5)*6;',xyz=['cos(a)*b','.55+sin(a*3+t)*.25','sin(a)*b'])]

# V02: specific maritime silhouettes, asymmetric rhythms and sparse accents.
SPEC=[
dict(key='aurora',name='Полярная завеса над морем',points=120,duration=38,parts=1,nu=180,nv=32,color=[.12,.63,.42],accent=[.33,.19,.52],weather=[1,1,0,0,0],night=True,
 code='a=u*6.283; b=sin(u*8+t*.075)+.25*sin(u*27-t*.12); c=70+26*sin(u*11+t*.09)+15*sin(u*31-t*.05);',xyz=['(u-.5)*310','72+v*c+12*b+6*sin(u*4+t*.09)','22*b+6*sin(u*20+t*.08)']),
dict(key='meteors',name='Болид над проливом',points=150,duration=25,parts=3,nu=64,nv=6,color=[.32,.59,.88],accent=[1,.66,.29],weather=[1,1,0,0,0],night=True,
 code='a=t-k*6.7; a=a-floor(a/22)*22; b=a/2.8; c=a<2.8?1:0;',xyz=['(95-b*215+u*58)*c','85+(17-b*39+u*11+(v-.5)*(1.3-.9*u))*c','(k*19+sin(u*5+t)*u*.7)*c']),
dict(key='waterspout',name='Смерч из грозовой тучи',points=180,duration=36,parts=3,nu=96,nv=74,color=[.13,.17,.19],accent=[.45,.49,.48],weather=[0,1,0,1,3],night=False,
 code='a=u*6.283-t*(k==1?1.3:.43)+v*6; b=k==1?8+v*29:(k==2?7:3)+pow(v,3.4)*(k==2?67:48); c=sin(v*4+t*.08)*v*18;',xyz=['cos(a)*b+c+(t-18)*.7','k==1?pow(1-v,2)*10:v*545','sin(a)*b+sin(v*7+t*.12)*v*11']),
dict(key='rift',name='Пасть водоворота',points=220,duration=32,parts=5,nu=160,nv=8,color=[.30,.51,.53],accent=[.88,.93,.89],weather=[0,1,2,1,2],night=False,
 code='a=u*6.283*1.65-t*.28+k*1.257; b=5+u*27+(v-.5)*(2+u*3);',xyz=['cos(a)*b','.2+sin(u*15-t*2+k)*.25','sin(a)*b']),
dict(key='jellies',name='Всплытие глубинных медуз',points=160,duration=34,parts=27,nu=40,nv=12,color=[.12,.47,.59],accent=[.54,.28,.47],weather=[1,1,2,0,0],night=True,
 code='a=k<3?k:floor((k-3)/8); b=k<3?u*6.283:((k-3)%8)*.7854; c=1+.14*sin(t*1.2+a*2.1); d=k<3?sin(v*1.571)*4.1*c:3.3*(1-u*.63); e=sin(a*2.4)*19+(t-17)*.52; f=cos(a*2.4)*13;',xyz=['e+cos(b)*d+(k<3?0:sin(u*8+t*1.2-u*1.6+a)*u*2+(v-.5)*.11)','5+a*1.3+sin(t*.6+a)*.65+(k<3?cos(v*1.571)*3.4*c:-u*10.5)','f+sin(b)*d+(k<3?0:cos(u*6+t*.7+a)*u*1.7)']),
dict(key='lanterns',name='Огни утонувшего конвоя',points=110,duration=32,parts=9,nu=28,nv=18,color=[.31,.83,.69],accent=[.10,.32,.37],weather=[1,1,3,0,0],night=True,
 code='a=k<3?k:k<6?k-3:k-6; b=u*6.283; c=.40+sin(v*3.1416)*.26; d=(a-1)*17+(t-16)*.65; e=(a-1)*(a-1)*6;',xyz=['d+(k<3?cos(b)*c:k<6?((u-.5)*2.2+sin(v*20+t)*.15):(u-.5)*(1+v*3)+sin(v*6+t*.8+a)*v*1.6)','k<3?2.4+sin(t*.8+a)*.45+v*1.1:k<6?.13+sin(u*15+t)*.1:3.2+v*8','e+(k<3?sin(b)*c:k<6?(v-.5)*11:sin(v*5+t*.5)*v)']),
dict(key='mantas',name='Миграция морских скатов',points=190,duration=34,parts=12,nu=78,nv=32,color=[.10,.35,.40],accent=[.43,.71,.70],weather=[1,1,2,1,0],night=True,
 code='a=v*2-1; b=sin(u*6.283); c=cos(u*6.283); d=k<2?k:k<4?k-2:k<8?floor((k-4)/2):k<10?k-8:k-10; e=k<2||k>=10?1:0; f=pow(v,.85);',xyz=['(d-.5)*29+(t-17)*.6+(e>0?b*(14-d)*f:k<4?sin(u*5+t*.8)*u*1.7+a*.65*(1-u*.85):k<8?(k%2==0?-1:1)*(1.2+u*.9)+a*.65:cos(v*6.283)*sin(u*3.14159)*2.1)','11+d*5+sin(t*.3+d)*1.2+(e>0?sin(t*.75-v*1.6+d*2)*pow(abs(b),1.5)*v*v*4+pow(1-v*v,2)*1.4+b*f*(d==0?.8:-1.2)+(k>=10?-1:1)*.55*(1-v*v):k<4?sin(u*4+t)*u:k<8?sin(u*3.1416)*.9:.4+sin(v*6.283)*sin(u*3.14159)*1.4)','(e>0?c*8*v*(1-abs(b)*.25)+b*b*v*v*4:k<4?8+u*25:k<8?-7.1-u*3.8:(u-.5)*14)+d*16']),
dict(key='bloom',name='Светящийся прилив',points=130,duration=32,parts=4,nu=160,nv=12,color=[.06,.54,.61],accent=[.26,.78,.67],weather=[1,1,1,0,0],night=True,
 code='a=(u-.5)*94; b=(v-.5)*4.5; c=sin(u*9+k*2+t*.2)*3+sin(u*23-t*.3)*.7;',xyz=['a','.16+sin(u*16+t)*.12','(k-1.5)*8+b+c-(t-16)*1.15'])]

def expr(s,cs=False):
    s=s.replace('floor(', 'Mathf.Floor(').replace('pow(', 'Mathf.Pow(').replace('sin(', 'Mathf.Sin(').replace('cos(', 'Mathf.Cos(').replace('abs(', 'Mathf.Abs(') if cs else s
    if cs:s=re.sub(r'(?<![\w.])(\d*\.\d+|\d+)(?![\w.])',lambda m:m[0]+'f',s)
    return s

cases=[]; jscases=[]
for i,s in enumerate(SPEC):
    code=expr(s['code'],True); xyz=[expr(x,True) for x in s['xyz']]
    jscode=s['code'];jsxyz=s['xyz'][:]
    if s['key']=='mantas':
        # Three-quarter courses expose the continuous silhouette and long tails from the gallery.
        code+=f'float xx={xyz[0]};float zz={xyz[2]};float cx=(d-.5f)*29+(t-17)*.6f;float cz=d*16;float turn=(d<.5f?-52:58)*Mathf.Deg2Rad;'
        xyz[0]='cx+(xx-cx)*Mathf.Cos(turn)+(zz-cz)*Mathf.Sin(turn)';xyz[2]='cz-(xx-cx)*Mathf.Sin(turn)+(zz-cz)*Mathf.Cos(turn)'
        jscode+=f'let xx={jsxyz[0]},zz={jsxyz[2]},cx=(d-.5)*29+(t-17)*.6,cz=d*16,turn=(d<.5?-52:58)*Math.PI/180;'
        jsxyz[0]='cx+(xx-cx)*cos(turn)+(zz-cz)*sin(turn)';jsxyz[2]='cz-(xx-cx)*sin(turn)+(zz-cz)*cos(turn)'
    cases.append(f'case {i}: {{ {code} return new Vector3({",".join(xyz)}); }}')
    jscases.append(f'case {i}: {{ {jscode} return [{",".join(jsxyz)}]; }}')
defs=[]
for s in SPEC:
    nums=lambda v:','.join(str(float(n))+'f' for n in v)
    defs.append('new ObservationDefinition("%s","%s",%d,%sf,%d,%d,%d,new Color(%s),new Color(%s),new float[]{%s},%s)'%(s['key'],s['name'],s['points'],s['duration'],s['parts'],s['nu'],s['nv'],nums(s['color']),nums(s['accent']),nums(s['weather']),str(s['night']).lower()))
cs='''// Generated by Tools/build_observation_kernels.py; edit that single source.
using UnityEngine;
namespace LighthouseGame {
public sealed class ObservationDefinition {
 public readonly string Key,Name; public readonly int Points,Parts,U,V; public readonly float Duration; public readonly Color Color,Accent; public readonly float[] Weather; public readonly bool Night;
 public ObservationDefinition(string key,string name,int points,float duration,int parts,int u,int v,Color color,Color accent,float[] weather,bool night) {Key=key;Name=name;Points=points;Duration=duration;Parts=parts;U=u;V=v;Color=color;Accent=accent;Weather=weather;Night=night;}
}
public static class ObservationKernels {
 public static readonly ObservationDefinition[] Catalog = {DEFS};
 public static Vector3 Evaluate(int kind,float u,float v,float k,float t) {
 float a=0,b=0,c=0,d=0,e=0,f=0;
 switch(kind) {CASES} return Vector3.zero;
 }
}}
'''.replace('DEFS',',\n'.join(defs)).replace('CASES','\n'.join(cases))
(OUT/'ObservationKernels.cs').write_text(cs,encoding='utf-8')
(ROOT/'ArtSource/observations-catalog.json').write_text(json.dumps(SPEC,ensure_ascii=False,indent=2),encoding='utf-8')
# A small, self-contained WebGL study: same positions, topology, timing and palette as Unity.
template=(ROOT/'Tools/observation-lab-template.html').read_text(encoding='utf-8')
html=template.replace('/*CATALOG*/',json.dumps(SPEC,ensure_ascii=False)).replace('/*KERNELS*/','\n'.join(jscases))
# Share the native fragment masks too: the browser study cannot silently keep an old effect.
native=(ROOT/'Lighthouse/Assets/Art/Shaders/ObservationVfx.shader').read_text(encoding='utf-8')
fragment=native[native.index('   float hash('):native.index('   ENDHLSL')]
fragment=fragment.replace('float4 Frag(V i):SV_Target','void main()').replace('float2 q=i.q;','')
for a,b in [('float2','vec2'),('float3','vec3'),('float4','vec4'),('lerp(','mix('),('frac(','fract('),('_Color.rgb','color'),('_Accent.rgb','accent'),('_Kind','kind'),('_Phase','time'),('_Daylight','0.35'),('_Visibility','700.0'),('_Opacity','1.0'),('i.part','piece'),('i.facing','facing'),('i.distance','dist'),('i.world','world'),('ddx(','dFdx('),('ddy(','dFdy('),('return vec4(c,','gl_FragColor=vec4(c,'),('PI','3.14159265')]:fragment=fragment.replace(a,b)
# GLSL ES needs floating literals in all overloaded arithmetic expressions.
fragment=re.sub(r'(?<![\w.])(\d+)(?![\w.])',lambda m:m[0]+'.0',fragment)
fragment='#extension GL_OES_standard_derivatives : enable\nprecision highp float; varying vec2 q; varying vec3 world; varying float piece,facing,dist; uniform vec3 color,accent; uniform float kind,time; float saturate(float x){return clamp(x,0.0,1.0);}\n'+fragment
html=html.replace('/*NATIVE_FRAGMENT*/',fragment)
# Keep the authored study local. Updating a conversation widget is a separate action.
(ROOT/'ArtSource/observation-vfx-lab.html').write_text(html,encoding='utf-8')
print('Generated shared kernels and laboratory:',len(SPEC),'events')
