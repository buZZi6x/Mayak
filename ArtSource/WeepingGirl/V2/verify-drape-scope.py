"""One-off comparison with the published pre-drape Blender source."""
import bpy, json
from pathlib import Path

out=Path(__file__).resolve().parent
def read_girl(path):
    with bpy.data.libraries.load(str(path),link=False) as (available,selected):
        selected.objects=['WeepingGirl']
    return selected.objects[0]
old=read_girl(out.parent/'WeepingGirl.blend')
new=read_girl(out/'WeepingGirlV2.blend')
om,nm=old.data,new.data
okeys,nkeys=om.shape_keys.key_blocks,nm.shape_keys.key_blocks
same_topology=len(om.vertices)==len(nm.vertices) and [tuple(p.vertices) for p in om.polygons]==[tuple(p.vertices) for p in nm.polygons]
same_materials=[m.name.split('.')[0] for m in om.materials]==[m.name.split('.')[0] for m in nm.materials]
cloth={i for p in nm.polygons if nm.materials[p.material_index].name.split('.')[0]=='Girl_Cloth' for i in p.vertices}
deltas={};noncloth={};stationary={}
for key in nkeys:
    before=okeys[key.name]
    differences=[(p.co-before.data[i].co).length for i,p in enumerate(key.data)]
    deltas[key.name]=max(differences)
    noncloth[key.name]=max(d for i,d in enumerate(differences) if i not in cloth)
    if key.name not in ['Seated']+['Rise_%02d'%k for k in range(8)]:stationary[key.name]=max(differences)
result={'pass':same_topology and same_materials and list(okeys.keys())==list(nkeys.keys()) and max(noncloth.values())<1e-7 and max(stationary.values())<1e-7,'same_topology':same_topology,'same_material_order':same_materials,'same_shape_names':list(okeys.keys())==list(nkeys.keys()),'vertex_count':len(nm.vertices),'max_noncloth_delta_by_shape':noncloth,'max_delta_by_shape_m':deltas,'unchanged_basis_and_optional_keys':stationary}
(out/'drape-scope-validation.json').write_text(json.dumps(result,indent=2))
print('DRAPE_SCOPE',json.dumps(result))
assert result['pass'],'Drape update modified a protected surface'
