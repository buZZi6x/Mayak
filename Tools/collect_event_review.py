"""Collect the actual integration evidence after the Editor and Windows checks."""
from pathlib import Path
import json,hashlib,datetime
root=Path(__file__).resolve().parents[1];out=root/'Artifacts/EventsUpgrade'
def read(name):return json.loads((out/name).read_text(encoding='utf-8-sig'))
def digest(path):return hashlib.sha256((root/path).read_bytes()).hexdigest()
data={'date':datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=3))).isoformat(),
      'integration':read('integration-verification.json'),'soak':read('soak-verification.json'),
      'web':read('web-verification.json'),'input':read('input-verification.json'),
      'watchRegression':read('gameplay-regression.json'),
      'sources':{p:digest(p) for p in ['ArtSource/Coastal_Fleet.blend','ArtSource/observations-catalog.json','Lighthouse/Assets/Scripts/Observations/ObservationKernels.cs']},
      'scope':'Local first-person Editor captures, functional checks and short performance sample; event prices and long-run balance remain tunable.'}
if (out/'build-and-startup.json').exists():data['windows']=read('build-and-startup.json')
(out/'quality-review.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print('Collected actual observations/fleet QA evidence')
