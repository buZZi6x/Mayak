"""Fetch CC0 source maps; downloads are cached and checked against the provider's MD5."""
import json, urllib.request, hashlib
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'ArtSource/Surfaces';OUT.mkdir(exist_ok=True)
ASSETS={'Coast_Wood':'weathered_planks','Coast_Plaster':'white_plaster_rough_01','Coast_Rock':'rock_face'}
def fetch(url):
    req=urllib.request.Request(url,headers={'User-Agent':'LighthouseJam/1.0 (local asset authoring)'})
    with urllib.request.urlopen(req,timeout=60) as r:return r.read()
jobs=[]
for material,asset in ASSETS.items():
    metadata=json.loads(fetch('https://api.polyhaven.com/files/'+asset))
    for kind in ('Diffuse','nor_gl','arm'):
        f=metadata[kind]['1k']['png'];jobs.append((material,asset,kind,f))
def download(job):
    mat,asset,kind,f=job;path=OUT/(asset+'_'+kind+'.png')
    if not path.exists() or hashlib.md5(path.read_bytes()).hexdigest()!=f['md5']:
        data=fetch(f['url']);assert hashlib.md5(data).hexdigest()==f['md5'];path.write_bytes(data)
    print('VERIFIED '+path.name,flush=True)
    return {'material':mat,'asset':asset,'map':kind,'path':str(path.relative_to(ROOT)),'url':f['url'],'md5':f['md5'],'license':'CC0','source':'https://polyhaven.com/a/'+asset}
with ThreadPoolExecutor(max_workers=4) as pool:manifest=list(pool.map(download,jobs))
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
