"""Validate help links, documentation links and translated picker patterns offline."""
import json
import re
from pathlib import Path
import importlib.util

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('catalog', ROOT/'tools/language-catalog.py')
catalog = importlib.util.module_from_spec(spec)
spec.loader.exec_module(catalog)
catalog.check()
sets = []
for lang in ('cs-CZ', 'en'):
    topics = json.loads((ROOT/'source/help'/f'{lang}.json').read_text(encoding='utf-8-sig'))
    ids = {t['Id'] for t in topics}
    assert len(ids) == len(topics), (lang, 'duplicate topic')
    assert 'updates' in ids
    for topic in topics:
        assert topic['Title'] and topic['Summary'] and topic['Body'], (lang,topic['Id'])
        links = re.findall(r'href=[\"\']#([^\"\']+)', topic['Body'])
        assert set(links + topic['Related']) <= ids, (lang,topic['Id'],links)
        for figure in re.findall(r'<figure src="([a-z0-9-]+)"', topic['Body']):
            assert (ROOT/'source/help/images'/f'{lang}.{figure}.png').exists(), (lang,figure)
    sets.append(ids)
assert sets[0] == sets[1], 'Help languages must cover the same topics'
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
