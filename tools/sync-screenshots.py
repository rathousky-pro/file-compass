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
    expected = {item['file'].replace('.', '-', 1) for item in old['images']}
    actual = {p.name for p in capture.glob('*.png')}
    assert actual == expected, f'Incomplete capture: missing {expected-actual}, extra {actual-expected}'
    assert 'PASS: 54 application screenshots' in (capture / 'result.txt').read_text(encoding='utf-8-sig')
    assert 'PASS: 2 generated-video screenshots' in (capture / 'video-result.txt').read_text(encoding='utf-8-sig')
    # Check every scene before modifying any published image.
    for name in sorted(expected):
        audit = (capture / name.replace('.png', '.visible-text.txt')).read_text(encoding='utf-8-sig')
        assert not re.search(r'janra|Programov|synthetic-demo-data|isolated-profile|10\.0\.\d+\.', audit, re.I), name
        content = (capture / name).read_bytes()
        assert content[:8] == b'\x89PNG\r\n\x1a\n', name
        width, height = struct.unpack('>II', content[16:24])
        assert width >= 250 and height >= 100, name
    website_images = website / 'assets/screenshots'
    assert (website / 'gallery.mjs').is_file()
    for destination, separator in [(help_images, '.'), (website_images, '-')]:
        records = []
        for name in sorted(expected):
            content = (capture / name).read_bytes()
            width, height = struct.unpack('>II', content[16:24])
            target = name[:2] + separator + name[3:]
            (destination / target).write_bytes(content)
            records.append(dict(file=target, width=width, height=height, sha256=hashlib.sha256(content).hexdigest()))
        manifest = dict(captured=captured, application=f'File Compass 0.9.5 beta / {build}',
                        source='Actual Fluent application windows; identical captures shared by web and offline help',
                        privacy=old['privacy'], languages=['cs', 'en'], scenesPerLanguage=28, images=records)
        (destination / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    for locale in ['cs', 'en']:
        shutil.copyfile(help_images / f'{locale}.audio-speed.png', help_images / f'{locale}.audio-advanced.png')
        shutil.copyfile(capture / f'{locale}-panels.png', website / f'assets/panels-{locale}.png')
    print(f'Synchronized {len(expected)} audited screenshots to web and help ({build})')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture', type=Path, required=True)
    parser.add_argument('--website', type=Path, required=True)
    parser.add_argument('--build', required=True)
    parser.add_argument('--captured', required=True, help='ISO capture date')
    args = parser.parse_args()
    sync(args.capture.resolve(), args.website.resolve(), args.build, args.captured)
