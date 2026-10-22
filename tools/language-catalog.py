"""Maintain readable, stable language keys without generating hash IDs."""
import argparse
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
CATALOG = ROOT / 'source/languages/cs-CZ.lng'
KEY = re.compile(r'[a-z][A-Za-z0-9]*(?:\.[a-z][A-Za-z0-9]*)+')

def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate key: ' + key)
        if not isinstance(value, str):
            raise ValueError('Language values must be strings: ' + key)
        result[key] = value
    return result

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=unique_object)

def save(path, values):
    lines = ['{']
    items = [(key, values[key]) for key in ('_language', '_name') if key in values]
    items += sorted((key, value) for key, value in values.items() if key not in ('_language', '_name'))
    previous = '_'
    for index, (key, value) in enumerate(items):
        group = key.split('.')[0]
        if not key.startswith('_') and group != previous:
            lines.append('')
        lines.append('  ' + json.dumps(key) + ': ' + json.dumps(value, ensure_ascii=False) + (',' if index + 1 < len(items) else ''))
        previous = group
    path.write_text('\n'.join(lines + ['}', '']), encoding='utf-8')

def check():
    base = read(CATALOG)
    for key in base:
        if not key.startswith('_') and not KEY.fullmatch(key):
            raise ValueError('Invalid semantic key: ' + key)
        if re.fullmatch(r'text\.[0-9a-f]{16}', key):
            raise ValueError('Generated hash key is not allowed: ' + key)
    references = 0
    for source in (ROOT / 'source/src').glob('*.cs'):
        if source.name.endswith('Tests.cs'):
            continue
        for key in re.findall(r'\bL\.(?:Text|Code|Format)\("([^"]+)"', source.read_text(encoding='utf-8-sig')):
            if key not in base:
                raise ValueError(f'{source.name}: missing key {key}')
            references += 1
    for locale in CATALOG.parent.glob('*.lng'):
        values = read(locale)
        for key in values:
            if key not in base and not key.startswith('_'):
                raise ValueError(f'{locale.name}: unknown key {key}; convert older packs first')
    groups = {key.split('.')[0] for key in base if not key.startswith('_')}
    print(f'OK: {len(base) - 2} semantic keys, {len(groups)} groups, {references} source references')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command')
    commands.add_parser('check', help='Validate catalog, duplicate keys and source references')
    add = commands.add_parser('add', help='Add an explicitly named semantic key')
    add.add_argument('key'); add.add_argument('value')
    convert = commands.add_parser('convert', help='Convert an old translation while keeping its values')
    convert.add_argument('input', type=pathlib.Path); convert.add_argument('output', type=pathlib.Path)
    args = parser.parse_args()
    if args.command == 'add':
        values = read(CATALOG)
        if not KEY.fullmatch(args.key) or re.fullmatch(r'text\.[0-9a-f]{16}', args.key):
            parser.error('Use a semantic key such as copy.confirmMove')
        if args.key in values:
            parser.error('Key already exists: ' + args.key)
        values[args.key] = args.value
        save(CATALOG, values)
    elif args.command == 'convert':
        if args.output.exists():
            parser.error('Output already exists; choose a new file')
        mapping = read(ROOT / 'tools/language-key-migration.json')
        old = read(args.input)
        values = dict(old)
        for key, value in old.items():
            name = mapping.get(key, key)
            if name != key:
                if name not in old:
                    values[name] = value
                values.pop(key, None)
        save(args.output, values)
        print(f'Converted {args.input} to {args.output}; translation values preserved')
        return
    check()

if __name__ == '__main__':
    main()
