"""Apply only an explicitly stored shell displacement to the retained r1 source."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy
from mathutils import Vector

ap=argparse.ArgumentParser();ap.add_argument('--parameters',required=True);ap.add_argument('--choice',required=True);ap.add_argument('--output',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);here=Path(__file__).resolve().parent;src=here.parent/'sources/superlite17b-rear-study-r1.blend'
params=json.loads(Path(a.parameters).read_text(encoding='utf-8'));choice=next(c for c in params['candidates'] if c['name']==a.choice);expected=params['source_sha256']
assert hashlib.sha256(src.read_bytes()).hexdigest()==expected
out=Path(a.output).resolve();assert not out.exists(),'Existing candidate protected'
bpy.ops.wm.open_mainfile(filepath=str(src));o=bpy.data.objects[params['allowed_object']];old=[o.matrix_world@v.co for v in o.data.vertices];new=[v.copy() for v in old]
for i,delta in enumerate(choice['outer_displacements']):new[i]+=Vector(delta);new[i+2254]+=Vector(delta)
for i in choice['fixed_ids']:assert (new[i]-old[i]).length<1e-12
assert all((new[i]-old[i]).length<1e-12 for i in range(4508,5880))
inverse=o.matrix_world.inverted()
for i,(v,p) in enumerate(zip(o.data.vertices,new)):
    # Do not round-trip protected coordinates through world/inverse matrices.
    # Even nanometre rounding can change BVH classifications at baseline tangencies.
    if (p-old[i]).length>1e-10:v.co=inverse@p
o.data.update();o['shell_study_r2']='Shell-only visual estimate; every other part retained; source saved separately.'
out.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(out))
assert hashlib.sha256(src.read_bytes()).hexdigest()==expected
parameter_bytes=Path(a.parameters).read_bytes()
out.with_suffix('.json').write_text(json.dumps({'source_sha256':expected,'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'choice':a.choice,'modified_object':o.name,'max_displacement_model_m':choice['max_displacement_model_m'],'changed_outer_vertices':choice['changed_outer_vertices'],'parameters_file_sha256':hashlib.sha256(parameter_bytes).hexdigest(),'parameters_text_lf_sha256':hashlib.sha256(parameter_bytes.replace(b'\r\n',b'\n')).hexdigest(),'parameters_hash_policy':'file hash uses runtime working bytes; LF text hash is portable across repository line-ending normalization.','limits':'Visual shell candidate only. No physical scale/camera uniqueness or unchanged-part/geometry acceptance implied until independent saved-file checks.'},indent=2)+'\n',encoding='utf-8')
print('SHELL_ONLY_CANDIDATE_SAVED '+a.choice+' source unchanged')
