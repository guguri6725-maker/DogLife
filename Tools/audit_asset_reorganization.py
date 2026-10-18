"""Read-only audit of Unity GUIDs, resource keys, and paths after asset moves."""
from pathlib import Path
import argparse, collections, json, re, subprocess

GUID=re.compile(rb'^guid: ([0-9a-f]{32})',re.M)
REFERENCE=re.compile(rb'guid: ([0-9a-f]{32})')

def guid(data):
    found=GUID.search(data)
    return found[1].decode() if found else None

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--baseline',default='de7421e^');parser.add_argument('--output',default='Builds/AssetMigrationAudit/static-audit.json');args=parser.parse_args()
    root=Path.cwd();current=collections.defaultdict(list);orphan=[];missing_meta=[]
    for meta in (root/'Assets').rglob('*.meta'):
        asset=Path(str(meta)[:-5]);key=guid(meta.read_bytes())
        if key:current[key].append(asset.relative_to(root).as_posix())
        if not asset.exists():orphan.append(meta.relative_to(root).as_posix())
    for asset in (root/'Assets').rglob('*'):
        if asset.is_file() and not asset.name.endswith('.meta') and not asset.name.startswith('.') and not Path(str(asset)+'.meta').exists():missing_meta.append(asset.relative_to(root).as_posix())
    entries=[]
    for row in subprocess.check_output(['git','ls-tree','-r','-z',args.baseline,'Assets']).split(b'\0'):
        if not row:continue
        info,path=row.split(b'\t',1)
        if path.endswith(b'.meta'):entries.append((info.split()[2],path.decode()[:-5]))
    payload=b'\n'.join(oid for oid,path in entries)+b'\n'
    batch=subprocess.run(['git','cat-file','--batch'],input=payload,stdout=subprocess.PIPE,check=True).stdout
    baseline={};offset=0
    for oid,path in entries:
        end=batch.index(b'\n',offset);header=batch[offset:end].split();size=int(header[2]);data=batch[end+1:end+1+size];offset=end+size+2
        key=guid(data)
        if key:baseline[key]=path
    moves={old:current[key][0] for key,old in baseline.items() if key in current and len(current[key])==1 and old!=current[key][0] and (root/current[key][0]).is_file()}
    known=set(current)
    for base in ['Packages','Library/PackageCache']:
        for meta in (root/base).rglob('*.meta'):
            key=guid(meta.read_bytes())
            if key:known.add(key)
    unresolved=collections.defaultdict(list);scanned=0
    for path in (root/'Assets').rglob('*'):
        if not path.is_file() or path.suffix.lower() not in {'.meta','.unity','.prefab','.asset','.mat','.anim','.controller','.overridecontroller','.playable','.mask','.rendertexture','.guiskin','.terrainlayer','.lighting'}:continue
        data=path.read_bytes()
        if not(path.name.endswith('.meta') or data.startswith(b'%YAML')):continue
        scanned+=1
        for key in set(REFERENCE.findall(data)):
            key=key.decode()
            if key not in known and not key.startswith('0000000000000000'):unresolved[key].append(path.relative_to(root).as_posix())
    old_refs=[];missing_literals=[];resources=[]
    for base in ['Assets/Scripts','Tools','ArtSource','ArtSources']:
        for path in (root/base).rglob('*'):
            if path.suffix not in {'.cs','.py'}:continue
            try:text=path.read_text(encoding='utf-8-sig')
            except UnicodeError:continue
            for number,line in enumerate(text.splitlines(),1):
                for match in re.finditer(r'Assets/[^"\'\r\n]+',line):
                    old=match[0]
                    if old in moves and not (root/old).exists():old_refs.append(dict(file=path.relative_to(root).as_posix(),line=number,old=old,new=moves[old]))
                if path.suffix!='.cs':continue
                for match in re.finditer(r'"(Assets/[^"\r\n]+)"',line):
                    value=match[1]
                    if not(root/value).exists():missing_literals.append(dict(file=path.relative_to(root).as_posix(),line=number,path=value,expression=line.strip()[:300]))
                for match in re.finditer(r'Resources\.Load(?:All|Async)?(?:<[^>]+>)?\s*\(\s*"([^"\r\n]+)"\s*\)',line):resources.append(dict(file=path.relative_to(root).as_posix(),line=number,key=match[1]))
    resource_keys=set()
    for path in (root/'Assets').rglob('*'):
        if not path.is_file() or path.name.endswith('.meta') or 'Resources' not in path.parts:continue
        index=len(path.parts)-1-list(reversed(path.parts)).index('Resources');resource_keys.add(Path(*path.parts[index+1:]).with_suffix('').as_posix())
    missing_resources=[item for item in resources if item['key'] not in resource_keys and not any(key.startswith(item['key']+'/') for key in resource_keys)]
    result=dict(summary=dict(asset_guids=len(current),moved_files=len(moves),preserved_moved_guids=len(moves),duplicate_guids=sum(len(v)>1 for v in current.values()),orphan_meta=len(orphan),assets_without_meta=len(missing_meta),serialized_files_scanned=scanned,unresolved_guids=len(unresolved),stale_file_paths=len(old_refs),missing_resource_literals=len(missing_resources)),moves=moves,duplicate_guids={k:v for k,v in current.items() if len(v)>1},orphan_meta=orphan,assets_without_meta=missing_meta,unresolved_guids=unresolved,stale_file_paths=old_refs,missing_resource_literals=missing_resources,missing_literal_candidates=missing_literals)
    output=root/args.output;output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(result['summary']));print('Unresolved GUID references:',json.dumps(unresolved,ensure_ascii=False));print('Missing Resources:',json.dumps(missing_resources,ensure_ascii=False))

if __name__=='__main__':main()
