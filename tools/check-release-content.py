"""Validate help links, documentation links and translated picker patterns offline."""
import json
import re
import hashlib
import xml.etree.ElementTree as ET
from pathlib import Path
import importlib.util

ROOT = Path(__file__).resolve().parents[1]
assert (ROOT/'source/languages/cs.lng').is_file() and not (ROOT/'source/languages/cs-CZ.lng').exists(), 'Czech catalog must use cs.lng'
assert not (ROOT/'source/help/cs-CZ.json').exists() and not (ROOT/'source/help/site/cs-CZ').exists(), 'Czech help must use cs'
spec = importlib.util.spec_from_file_location('catalog', ROOT/'tools/language-catalog.py')
catalog = importlib.util.module_from_spec(spec)
spec.loader.exec_module(catalog)
catalog.check()
spec = importlib.util.spec_from_file_location('dependency_notices', ROOT/'tools/check-dependency-notices.py')
dependency_notices = importlib.util.module_from_spec(spec)
spec.loader.exec_module(dependency_notices)
dependency_notices.check()
spec = importlib.util.spec_from_file_location('help_builder', ROOT/'tools/build-help.py')
help_builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(help_builder)
help_builder.generate(check=True)
for locale in ('cs', 'en'):
    page = ROOT/'source/help/site'/locale/'index.html'
    tree = ET.fromstring(page.read_text(encoding='utf-8'))
    ids = {node.attrib['id'] for node in tree.iter() if 'id' in node.attrib}
    assert len(ids) == len([node for node in tree.iter() if 'id' in node.attrib]), 'Duplicate HTML element ID'
    assert len(tree.findall('.//article')) == 44
    for node in tree.iter():
        for attribute in ('href', 'src'):
            target = node.get(attribute, '')
            if target.startswith('#'):
                assert target[1:] in ids, (locale, target)
            elif target and '://' not in target:
                file = page.parent/target
                if '../images/' in target:
                    file = ROOT/'source/help/images'/Path(target).name
                assert file.is_file(), (locale, target)
    assert not re.search(r'https?://', (ROOT/'source/help/site/help.js').read_text(encoding='utf-8')), 'Help must work offline'
sets = []
illustration_sets = []
for lang in ('cs', 'en'):
    topics = json.loads((ROOT/'source/help'/f'{lang}.json').read_text(encoding='utf-8-sig'))
    ids = {t['Id'] for t in topics}
    assert len(ids) == len(topics), (lang, 'duplicate topic')
    assert 'updates' in ids
    for topic in topics:
        assert topic['Title'] and topic['Summary'] and topic['Body'], (lang,topic['Id'])
        ET.fromstring('<article>' + topic['Body'] + '</article>')
        links = re.findall(r'href=[\"\']#([^\"\']+)', topic['Body'])
        assert set(links + topic['Related']) <= ids, (lang,topic['Id'],links)
        for figure in re.findall(r'<figure src="([a-z0-9-]+)"', topic['Body']):
            assert (ROOT/'source/help/images'/f'{lang}.{figure}.png').exists(), (lang,figure)
    sets.append(ids)
    illustration_sets.append(set(re.findall(r'<figure src="([a-z0-9-]+)"', ''.join(t['Body'] for t in topics))))
assert sets[0] == sets[1], 'Help languages must cover the same topics'
assert illustration_sets[0] == illustration_sets[1] and len(illustration_sets[0]) == 28, 'Help illustrations must cover the same current workflows'
manifest = json.loads((ROOT/'source/help/images/manifest.json').read_text(encoding='utf-8'))
assert len(manifest['images']) == 56
for image in manifest['images']:
    content = (ROOT/'source/help/images'/image['file']).read_bytes()
    assert hashlib.sha256(content).hexdigest() == image['sha256'], image['file']
for path in [ROOT/'README.md', *sorted((ROOT/'docs').glob('*.md'))]:
    for link in re.findall(r'\]\(([^)]+)\)',path.read_text(encoding='utf-8-sig')):
        if '://' in link or link.startswith('#'): continue
        target = link.split('#')[0]
        assert (path.parent/target).exists(), (path.name,target)
for relative in ('source/plugins/7zip/License.txt','source/plugins/smart/LICENSE.txt',
                 'source/plugins/smart/smartmontools-7.5.tar.gz','source/plugins/media/MAGICK-NOTICE.txt',
                 'source/plugins/media/FFMPEG-LICENSE.txt','source/plugins/media/GPL-3.0.txt',
                 'source/ui/icons/LICENSE.txt','source/preview/WebView2-LICENSE.txt'):
    assert (ROOT/relative).stat().st_size > 0, relative
print(f'OK: {len(sets[0])} topics in both help languages, local documentation links and retained license/source files')
