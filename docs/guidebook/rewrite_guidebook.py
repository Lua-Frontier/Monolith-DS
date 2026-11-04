"""Edit the Markdown manuscript first; --apply imports that manuscript into the game.

Custom guidebook widgets remain as HTML-like directives in Markdown. Their attributes
are part of the manuscript, so links, pictures and console buttons survive import.
The manuscript is the editable source; only --apply writes game resources.
"""
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
DOC = Path(__file__).parent
MANUSCRIPT = DOC / 'GUIDEBOOK.md'
PREFIX = 'Resources/ServerInfo/_LuaM/Guidebook/'

def xml(body):
    body = re.sub(r'<!--.*?-->', '', body, flags=re.S)
    body = re.sub(r'\[([^\]\n]+)\]\(guide://([^)\n]+)\)', r'[textlink="\1" link="\2"]', body)
    body = re.sub(r'\*\*(.+?)\*\*', r'[bold]\1[/bold]', body)
    body = re.sub(r'(?<!\*)\*([^*\n]+)\*(?!\*)', r'[italic]\1[/italic]', body)
    # The game only has hyphen lists; Markdown numbered lists need explicit rows.
    body = re.sub(r'(?m)^(\d+)\. (.+)$', r'- [bold]\1.[/bold] \2', body)
    # The game parser needs a paragraph boundary before a heading and after widgets.
    body = re.sub(r'(?m)(?<!\n)\n(#{1,4} )', r'\n\n\1', body)
    body = '\n'.join(line.rstrip() for line in body.splitlines())
    return '<Document>\n' + body.strip() + '\n</Document>\n'

def read_manuscript():
    text = MANUSCRIPT.read_text(encoding='utf-8')
    pairs = re.findall(r'<!-- article: (.*?) -->\n(.*?)\n<!-- /article -->', text, re.S)
    if len(pairs) != len(dict(pairs)):
        raise ValueError('Duplicate article paths in GUIDEBOOK.md')
    return dict(pairs)

def guide_entries():
    """Read guide metadata without requiring third-party Python YAML packages."""
    entries = {}
    for file in (ROOT / 'Resources/Prototypes').rglob('*.yml'):
        for block in re.split(r'(?m)^- type: ', file.read_text(encoding='utf-8-sig')):
            if not block.startswith('guideEntry\n'):
                continue
            fields = {}
            lines = block.splitlines()
            children = []
            reading_children = False
            for line in lines[1:]:
                match = re.match(r'^  ([\w-]+):\s*(.*)', line)
                if match:
                    key, value = match.groups()
                    value = value.split('#', 1)[0].strip().strip('"\'')
                    fields[key] = value
                    reading_children = key == 'children'
                    if reading_children and value.startswith('['):
                        children = [v.strip().strip('"\'') for v in value[1:-1].split(',') if v.strip()]
                elif reading_children:
                    child = re.match(r'^\s+-\s+([^\s#]+)', line)
                    if child:
                        children.append(child[1].strip('"\''))
            if 'id' in fields:
                fields['children'] = children
                fields['source'] = str(file.relative_to(ROOT))
                if fields['id'] in entries:
                    raise ValueError('Duplicate GuideEntry: ' + fields['id'])
                entries[fields['id']] = fields
    return entries


def check(articles):
    identifiers = set()
    for path in (ROOT/'Resources/Prototypes').rglob('*.yml'):
        identifiers.update(re.findall(r'(?m)^  id: ["\']?([^\s"\'#]+)', path.read_text(encoding='utf-8-sig')))
    entries = guide_entries()
    errors = []
    own_prefix = '/ServerInfo/_LuaM/Guidebook/'
    visible_files = set()
    visible_entries = set()

    def walk(identifier, folder, ancestors):
        if identifier in ancestors:
            errors.append('Guide tree cycle: ' + ' -> '.join(ancestors + [identifier]))
            return
        if identifier not in entries:
            errors.append('Unknown child GuideEntry: ' + identifier)
            return
        if identifier in visible_entries:
            errors.append('Duplicate guide tree entry: ' + ' -> '.join(ancestors + [identifier]))
            return
        visible_entries.add(identifier)
        entry = entries[identifier]
        path = entry.get('text', '')
        if path.startswith(own_prefix):
            visible_files.add('Resources' + path)
            actual = path.removeprefix(own_prefix).split('/')[0]
            if actual != folder:
                errors.append(f'{identifier}: folder {actual} differs from tree section {folder}')
        for child in entry['children']:
            walk(child, folder, ancestors + [identifier])

    for identifier, entry in entries.items():
        if entry.get('bookRoot') != 'true':
            continue
        path = entry.get('text', '')
        folder = path.removeprefix(own_prefix).split('/')[0] if path.startswith(own_prefix) else ''
        walk(identifier, folder, [])
    for identifier, entry in entries.items():
        path = entry.get('text', '')
        if path.startswith(own_prefix) and 'Resources' + path not in articles:
            errors.append(f'{identifier}: maintained XML missing from manuscript: {path}')

    for path, body in articles.items():
        content = xml(body)
        try:
            ET.fromstring(content)
        except ET.ParseError as error:
            errors.append(f'{path}: {error}')
        for kind, identifier in re.findall(r'\b(link|Entity|Reagent)="([^"]+)"', content):
            if identifier not in (entries if kind == 'link' else identifiers):
                errors.append(f'{path}: unknown {kind}={identifier}')
            elif kind == 'link' and not (ROOT / ('Resources' + entries[identifier].get('text', ''))).is_file():
                errors.append(f'{path}: link {identifier} points to a missing XML')
        for map_path in re.findall(r'<GuideMap Map="([^"]+)"', content):
            if not (ROOT/('Resources'+map_path)).is_file():
                errors.append(f'{path}: missing map {map_path}')
        target = ROOT / path
        if not target.is_file():
            errors.append(f'{path}: XML missing; run --apply')
        elif target.read_text(encoding='utf-8-sig') != content:
            errors.append(f'{path}: XML differs; run --apply')
        if path.startswith(PREFIX) and path not in visible_files and '/Contextual/' not in path:
            errors.append(f'{path}: no visible place in guidebook; merge it or declare contextual help')
    for file in (ROOT / PREFIX).rglob('*.xml'):
        path = file.relative_to(ROOT).as_posix()
        if path not in articles:
            errors.append(f'{path}: orphan XML outside manuscript')
    print(f'{len(articles)} articles, {len(errors)} errors')
    for error in errors:
        print(error)
    return bool(errors)

if __name__ != '__main__':
    pass
elif '--apply' in sys.argv:
    articles = read_manuscript()
    for path, body in articles.items():
        dest = ROOT / path
        if not dest.resolve().is_relative_to((ROOT/'Resources/ServerInfo').resolve()):
            raise ValueError(path)
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text(xml(body), encoding='utf-8')
    print(f'Imported {len(articles)} articles from {MANUSCRIPT.name}')
elif '--check' in sys.argv:
    raise SystemExit(check(read_manuscript()))
else:
    print('Edit GUIDEBOOK.md, then use --apply and --check.')
