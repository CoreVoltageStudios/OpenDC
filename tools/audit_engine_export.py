"""Build-time audit only; no Python dependency on the player's computer.
Inspect actual Godot 4 PCK entries and bytes, not just an intended export filter.
"""
import argparse, hashlib, json, struct
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('engine', type=Path)
p.add_argument('output', type=Path)
a = p.parse_args()
b = (a.engine / 'OpenDC.pck').read_bytes()
magic, version, major, minor, patch, flags = struct.unpack_from('<6I', b)
assert magic == 0x43504447 and version == 4, 'Unreviewed PCK layout'
assert not flags & 1, 'Encrypted pack cannot be audited'
base, directory = struct.unpack_from('<QQ', b, 24)
count, = struct.unpack_from('<I', b, directory)
pos = directory + 4
entries = []
fonts = ('Barlow-Regular.ttf', 'Barlow-Medium.ttf', 'Barlow-SemiBold.ttf',
         'Barlow-Bold.ttf', 'ShareTechMono-Regular.ttf', 'RedHatDisplay-Variable.ttf',
         'BarlowSemiCondensed-Medium.ttf', 'BarlowSemiCondensed-SemiBold.ttf')
notices = ('barlow-OFL.txt', 'sharetechmono-OFL.txt', 'redhatdisplay-OFL.txt', 'barlowsemicondensed-OFL.txt')
landfall = ('opendc-wordmark.svg', 'cue-residual-hold.wav', 'cue-charge-transfer.wav',
            'cue-milestone.wav', 'cue-first-contact.wav', 'cue-awakening.wav')
for _ in range(count):
    n, = struct.unpack_from('<I', b, pos); pos += 4
    name = b[pos:pos+n].rstrip(b'\0').decode('utf-8'); pos += n
    offset, size = struct.unpack_from('<QQ', b, pos); pos += 16
    digest = b[pos:pos+16]; pos += 16
    entry_flags, = struct.unpack_from('<I', b, pos); pos += 4
    assert entry_flags == 0, ('Unreviewed resource flags', name, entry_flags)
    assert '..' not in name.split('/') and not name.startswith(('/', '\\')), name
    allowed = name in {'Main.tscn.remap', 'build.json', 'project.binary',
                      '.godot/uid_cache.bin', '.godot/global_script_class_cache.cfg',
                      'Branding/Icon/odc-icon.ico', 'Branding/Icon/png/odc-icon-512.png',
                      'Branding/Icon/png/odc-icon-512.png.import',
                      'Branding/CoreVoltage/core-voltage-intro.wav.import'}
    allowed |= name in {'Fonts/'+f+'.import' for f in fonts}
    allowed |= name in {'Fonts/'+f for f in notices}
    allowed |= name in {'Branding/Landfall/'+f+suffix for f in landfall for suffix in ('','.import')}
    allowed |= name.startswith('.godot/exported/') and name.endswith('-Main.scn')
    allowed |= name.startswith('.godot/imported/') and any(
        name.split('/')[-1].startswith(f+'-') for f in fonts + landfall + ('core-voltage-intro.wav', 'odc-icon-512.png'))
    # Godot retains a newline-only C# marker when script contents are disabled.
    allowed |= name == 'Viewer.cs' and size <= 1
    assert allowed, ('Unexpected resource in engine-only distribution', name)
    data = b[base+offset:base+offset+size]
    if name == 'Viewer.cs':
        assert not data.strip(), 'C# source unexpectedly included in resource pack'
    assert len(data) == size and hashlib.md5(data).digest() == digest, ('Pack integrity', name)
    entries.append(dict(path=name, bytes=size, sha256=hashlib.sha256(data).hexdigest()))
assert pos <= len(b) and count > 20
seen = {e['path'] for e in entries}
assert 'build.json' in seen and 'Branding/Icon/odc-icon.ico' in seen
for font in fonts:
    assert 'Fonts/'+font+'.import' in seen, ('Missing font', font)
assert 'Branding/CoreVoltage/core-voltage-intro.wav.import' in seen
runtime_dir = 'data_DarkColony.Viewer_windows_x86_64'
deps = json.loads((a.engine/runtime_dir/'DarkColony.Viewer.deps.json').read_text(encoding='utf-8-sig'))
runtime_files = {'DarkColony.Viewer.deps.json', 'DarkColony.Viewer.runtimeconfig.json'}
for target in deps['targets'].values():
    for package in target.values():
        for section in ('runtime', 'native'):
            runtime_files.update(Path(k).name for k in package.get(section, {}))
loose = {'OpenDC.exe','OpenDC.pck','odc-icon.ico','build.json','install-profile.json','NOTICES.txt'}
loose |= {runtime_dir+'/'+f for f in runtime_files}
loose |= {'Source/Media/'+f for f in ('AviConverter.cs','Cinepak.cs','DarkColony.Media.csproj','Directory.Build.props','README.txt')}
loose |= {'Notices/'+f for f in ('barlow-OFL.txt','sharetechmono-OFL.txt','redhatdisplay-OFL.txt',
    'barlowsemicondensed-OFL.txt',
    'OPENDC-LICENSE.txt','ASSET-LICENSES.md','docs/PRESENTATION-LICENSE-MAP.md','docs/DEPENDENCIES.md',
    'COPYING.LGPLv2.1','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt','GODOT-COPYRIGHT.txt',
    'GODOT-LICENSE.txt','GODOT-COMPONENTS.json','GODOT-THIRD-PARTY-LICENSES.json','GODOT-VERSION.json','MEDIA-NOTICE.txt')}
inventory = []
for f in sorted(a.engine.rglob('*')):
    assert not f.is_symlink(), f
    if f.is_file():
        rel = f.relative_to(a.engine).as_posix()
        assert rel in loose, ('Unexpected loose distribution file', rel)
        assert not set(rel.lower().split('/')) & {'private', 'generated', 'profiles', 'saves', 'content', 'evidence'}, rel
        assert f.suffix.lower() not in {'.avi','.iso','.bin','.mdf','.cue','.zip','.odcv','.tro','.spr','.pdb'}, rel
        inventory.append(dict(path=rel, bytes=f.stat().st_size, sha256=hashlib.sha256(f.read_bytes()).hexdigest()))
assert runtime_files <= {f.name for f in (a.engine/runtime_dir).iterdir() if f.is_file()}, 'Missing runtime dependency'
for name in ('OPENDC-LICENSE.txt', 'ASSET-LICENSES.md', 'docs/PRESENTATION-LICENSE-MAP.md', 'docs/DEPENDENCIES.md'):
    notice = (a.engine/'Notices'/name).read_text(encoding='utf-8-sig')
    assert 'awaiting confirmation before publication' not in notice and 'Pre-publication draft' not in notice, ('Unfinished license notice', name)
a.output.mkdir(parents=True, exist_ok=True)
(a.output/'pck-inventory.json').write_text(json.dumps(dict(format=version, godot=[major,minor,patch], entries=entries), indent=2), encoding='utf-8')
(a.output/'final-engine-inventory.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
print(json.dumps(dict(files=len(inventory), bytes=sum(f['bytes'] for f in inventory), resources=len(entries), result='PASS')))
