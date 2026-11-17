"""Publish an audited bilingual capture to help and the separate website checkout."""
import argparse
import hashlib
import json
import re
import shutil
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def sync(capture, website, build, captured):
    help_images = ROOT / 'source/help/images'
    old = json.loads((help_images / 'manifest.json').read_text(encoding='utf-8'))
    scenes = []
    for locale in ('cs', 'en'):
        topics = json.loads((ROOT / 'source/help' / f'{locale}.json').read_text(encoding='utf-8-sig'))
        scenes.append(set(re.findall(r'<figure src="([a-z0-9-]+)"', ''.join(topic['Body'] for topic in topics))))
    assert scenes[0] == scenes[1] and 'video-preview' in scenes[0], 'Help scene lists differ'
    expected = {f'{locale}-{scene}.png' for locale in ('cs', 'en') for scene in scenes[0]}
    actual = {p.name for p in capture.glob('*.png')}
    assert actual == expected, f'Incomplete capture: missing {expected-actual}, extra {actual-expected}'
    assert f'PASS: {len(expected)-2} application screenshots' in (capture / 'result.txt').read_text(encoding='utf-8-sig')
    assert 'PASS: 2 generated-video screenshots' in (capture / 'video-result.txt').read_text(encoding='utf-8-sig')
    # Check every scene before modifying any published image.
    for name in sorted(expected):
        audit = (capture / name.replace('.png', '.visible-text.txt')).read_text(encoding='utf-8-sig')
        assert not re.search(r'janra|Programov|synthetic-demo-data|isolated-profile|10\.0\.\d+\.', audit, re.I), name
        content = (capture / name).read_bytes()
        assert content[:8] == b'\x89PNG\r\n\x1a\n', name
        width, height = struct.unpack('>II', content[16:24])
        assert width >= 250 and height >= 100, name
    destinations = [(help_images, '.')]
    if website is not None:
        assert (website / 'gallery.mjs').is_file()
        destinations.append((website / 'assets/screenshots', '-'))
    for destination, separator in destinations:
        records = []
        for name in sorted(expected):
            content = (capture / name).read_bytes()
            width, height = struct.unpack('>II', content[16:24])
            target = name[:2] + separator + name[3:]
            (destination / target).write_bytes(content)
            records.append(dict(file=target, width=width, height=height, sha256=hashlib.sha256(content).hexdigest()))
        manifest = dict(captured=captured, application=f'File Compass 0.9.5 beta / {build}',
                        source='Actual application windows captured from the stated build using synthetic data',
                        privacy=old['privacy'], languages=['cs', 'en'], scenesPerLanguage=len(scenes[0]), images=records)
        (destination / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    for locale in ['cs', 'en']:
        shutil.copyfile(help_images / f'{locale}.audio-speed.png', help_images / f'{locale}.audio-advanced.png')
        if website is not None:
            shutil.copyfile(capture / f'{locale}-panels.png', website / f'assets/panels-{locale}.png')
    print(f'Synchronized {len(expected)} audited screenshots to help' + (' and web' if website else '') + f' ({build})')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture', type=Path, required=True)
    parser.add_argument('--website', type=Path, help='Optional separate website checkout')
    parser.add_argument('--build', required=True)
    parser.add_argument('--captured', required=True, help='ISO capture date')
    args = parser.parse_args()
    sync(args.capture.resolve(), args.website.resolve() if args.website else None, args.build, args.captured)
