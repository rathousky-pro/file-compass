"""Audit pinned runtime binaries and their original notices. --refresh records a reviewed baseline."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'source'
MANIFEST = SOURCE / 'dependencies.json'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inventory():
    groups = {
        '7zip': ('LGPL-2.1-or-later with unRAR restriction and BSD components', ['plugins/7zip/License.txt']),
        'cloud': ('MIT', ['plugins/cloud/LICENSE.txt']),
        'everything': ('MIT', ['plugins/everything/LICENSE.txt']),
        'network': ('curl, BSD, MIT, ISC and dependency-specific terms', ['plugins/network/licenses']),
        'smart': ('GPL-2.0', ['plugins/smart/LICENSE.txt', 'plugins/smart/smartmontools-7.5.tar.gz']),
        'ffmpeg': ('LGPL-3.0-or-later; corresponding sources still required for public redistribution', ['plugins/media/FFMPEG-LICENSE.txt', 'plugins/media/GPL-3.0.txt', 'plugins/media/SOURCE.json']),
        'magick': ('Apache-2.0 wrapper plus native dependency licenses', ['plugins/media/MAGICK-NOTICE.txt', 'plugins/media/SOURCE.json']),
        'openmp': ('Microsoft Visual C++ redistribution terms; publisher entitlement required', ['plugins/media/SOURCE.json', 'plugins/media/README.txt']),
        'webview': ('Microsoft WebView2 SDK terms; runtime is external', ['preview/WebView2-LICENSE.txt', 'preview/WebView2-NOTICE.txt']),
        'wpfui': ('MIT and third-party terms', ['ui/licenses/WPF-UI-LICENSE.txt', 'ui/licenses/WPF-UI-THIRD-PARTY.txt']),
        'microsoft': ('MIT', ['ui/licenses/MICROSOFT-LICENSE.txt']),
        'discutils': ('MIT and third-party terms', ['ui/licenses/DiscUtils']),
        'pdfpig': ('Apache-2.0', ['ui/licenses/PdfPig/LICENSE.txt']),
        'avalonedit': ('MIT', ['ui/licenses/AVALONEDIT-LICENSE.txt']),
        'markdig': ('BSD-2-Clause', ['ui/licenses/MARKDIG-LICENSE.txt']),
        'taglib': ('LGPL-2.1', ['ui/licenses/TAGLIBSHARP-LICENSE.txt']),
        'sharpsevenzip': ('LGPL-3.0-or-later', ['ui/licenses/SharpSevenZip/LICENSE.txt']),
        'yaml': ('MIT, including libyaml-derived code', ['ui/licenses/YamlDotNet/LICENSE.txt', 'ui/licenses/YamlDotNet/LICENSE-libyaml.txt']),
    }
    exact = {'ICSharpCode.AvalonEdit.dll':'avalonedit', 'Markdig.dll':'markdig', 'TagLibSharp.dll':'taglib', 'SharpSevenZip.dll':'sharpsevenzip', 'YamlDotNet.dll':'yaml'}
    entries = []
    for folder in ('ui', 'plugins', 'preview'):
        for path in sorted((SOURCE / folder).rglob('*')):
            if path.suffix.lower() not in ('.dll', '.exe', '.sfx'):
                continue
            relative = path.relative_to(SOURCE).as_posix()
            name = path.name
            if folder == 'plugins':
                group = path.relative_to(SOURCE / folder).parts[0]
                if group == 'media':
                    group = 'openmp' if name == 'vcomp140.dll' else 'magick' if name.startswith('Magick.') else 'ffmpeg'
            elif name.startswith('Microsoft.Web.WebView2') or folder == 'preview': group = 'webview'
            elif name.startswith('Magick.'): group = 'magick'
            elif name.startswith(('DiscUtils.', 'LTRData.', 'System.Collections.Immutable', 'System.Text.Encoding.CodePages', 'System.Threading.Tasks.Extensions')): group = 'discutils'
            elif name.startswith('UglyToad.'): group = 'pdfpig'
            elif name.startswith('Wpf.Ui'): group = 'wpfui'
            elif name.startswith(('System.', 'Microsoft.Bcl.')): group = 'microsoft'
            else: group = exact.get(name)
            assert group in groups, f'Unclassified runtime binary: {relative}'
            license, notices = groups[group]
            for notice in notices: assert (SOURCE / notice).exists(), f'Missing notice: {notice}'
            entries.append(dict(file=relative, sha256=digest(path), component=group, license=license, notices=notices))
    # Preserve every original legal text and package provenance record, not just a sample.
    legal = set()
    for _, notices in groups.values():
        for notice in notices:
            path = SOURCE / notice
            if path.is_file(): legal.add(path)
    for folder in ('ui/licenses', 'plugins', 'preview', 'ui/icons'):
        for path in (SOURCE / folder).rglob('*'):
            if not path.is_file(): continue
            name = path.name.lower()
            if any(term in name for term in ('license', 'copying', 'notice', 'source.json', 'manifest.json')) or path.suffix == '.nuspec' or name == 'build-manifest.txt': legal.add(path)
    return dict(schema=1, reviewed='2026-11-01', scope='Pinned source distribution; optional vendor updates have independent versions and terms.', binaries=entries, originalNotices=[dict(file=p.relative_to(SOURCE).as_posix(), sha256=digest(p)) for p in sorted(legal)])


def check(refresh=False, installation=None):
    current = inventory()
    if refresh: MANIFEST.write_text(json.dumps(current, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    else: assert json.loads(MANIFEST.read_text(encoding='utf-8')) == current, 'Dependency inventory changed: review notices and run tools/check-dependency-notices.py --refresh'
    if installation:
        install = Path(installation).resolve()
        def packaged(relative):
            if relative.startswith('ui/licenses/'): return install / relative.replace('ui/', 'libraries/', 1)
            if relative.startswith('ui/icons/'):
                names = {'LICENSE.txt':'FLUENT-ICONS-LICENSE.txt', 'SOURCE.json':'FLUENT-ICONS-SOURCE.json', 'TOOLBAR-SOURCE.json':'FLUENT-TOOLBAR-SOURCE.json', 'COLOR-SOURCE.json':'FLUENT-COLOR-SOURCE.json'}
                return install / 'libraries/licenses' / names[Path(relative).name] if Path(relative).name in names else None
            if relative.startswith('ui/'): return install / relative.replace('ui/', 'libraries/', 1)
            if relative == 'preview/manifest.json': return None  # Included inside preview.dll.
            return install / relative
        for entry in current['binaries'] + current['originalNotices']:
            path = packaged(entry['file'])
            if path is not None: assert path.is_file() and digest(path) == entry['sha256'], f'Installed dependency/notice mismatch: {path}'
        print('OK: packaged binaries, original licenses and SMART sources match the reviewed inventory')
    print(f'OK: {len(current["binaries"])} runtime binaries classified; {len(current["originalNotices"])} original notices/provenance files verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--refresh', action='store_true')
    parser.add_argument('--installation', type=Path)
    args = parser.parse_args()
    check(args.refresh, args.installation)
