"""Generate offline, browser-readable help shared with the native application."""
import argparse
import html
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'source/help'
SITE = SOURCE / 'site'


def render(locale):
    topics = json.loads((SOURCE / f'{locale}.json').read_text(encoding='utf-8-sig'))
    cs = locale == 'cs'
    esc = lambda value: html.escape(value, quote=True)
    title = 'Nápověda File Compass' if cs else 'File Compass Help'
    navigation = []
    for group in dict.fromkeys(t['Group'] for t in topics):
        links = ''.join(f'<a href="#{esc(t["Id"])}" data-topic-link="{esc(t["Id"])}">{esc(t["Title"])}</a>' for t in topics if t['Group'] == group)
        navigation.append(f'<details open="open"><summary>{esc(group)}</summary>{links}</details>')
    articles = []
    for topic in topics:
        def figure(match):
            scene, caption = match.groups()
            image = f'../images/{locale}.{scene}.png'
            return f'<figure data-figure="{scene}"><a href="{image}" data-enlarge="true"><img src="{image}" loading="lazy" alt="{esc(re.sub("<[^>]*>", " ", caption).strip())}" /></a><figcaption>{caption}</figcaption></figure>'
        body = re.sub(r'<figure src="([a-z0-9-]+)">(.*?)</figure>', figure, topic['Body'], flags=re.S)
        related = ''.join(f'<a href="#{esc(id)}">{esc(next(t["Title"] for t in topics if t["Id"] == id))}</a>' for id in topic['Related'])
        attrs = ' '.join(f'data-{key}="{esc(topic[field])}"' for key, field in [('topic', 'Id'), ('title', 'Title'), ('group', 'Group'), ('summary', 'Summary'), ('source-language', 'SourceLanguage')])
        articles.append(f'<article id="{esc(topic["Id"])}" {attrs}><header><div class="eyebrow">{esc(topic["Group"])}</div><h1>{esc(topic["Title"])}</h1><p>{esc(topic["Summary"])}</p></header><div class="topic-body">{body}</div><nav class="related" aria-label="Related">{related}</nav></article>')
    return f'''<!DOCTYPE html>
<html lang="{locale}"><head><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" /><title>{title}</title><link rel="stylesheet" href="../help.css" /><script src="../help.js" defer="defer"></script></head>
<body><a class="skip" href="#content">{'Přejít na obsah' if cs else 'Skip to content'}</a><header class="top"><a href="#home">File Compass · {'Nápověda' if cs else 'Help'}</a><nav><a href="../cs/index.html" lang="cs" data-language="true">Čeština</a><a href="../en/index.html" lang="en" data-language="true">English</a></nav></header>
<div class="layout"><aside><label for="help-search">{'Hledat v nápovědě' if cs else 'Search help'}</label><input id="help-search" type="search" autocomplete="off" placeholder="{'Hledané téma…' if cs else 'Search topics…'}" /><p id="result-count" aria-live="polite"></p><nav aria-label="{'Obsah' if cs else 'Contents'}">{''.join(navigation)}</nav></aside><main id="content">{''.join(articles)}<p id="no-results" hidden="hidden">{'Žádné odpovídající téma.' if cs else 'No matching topic.'}</p></main></div>
<dialog id="zoom"><form method="dialog"><button>{'Zavřít' if cs else 'Close'} ×</button></form><img alt="" /></dialog></body></html>
'''


def generate(check=False):
    outputs = {SITE / locale / 'index.html': render(locale) for locale in ('cs', 'en')}
    outputs[SITE / 'index.html'] = '<!DOCTYPE html>\n<html lang="en"><head><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" /><title>File Compass Help</title><link rel="stylesheet" href="help.css" /></head><body><main class="language-choice"><h1>File Compass</h1><p>Offline help / Nápověda offline</p><p><a href="cs/index.html">Česká nápověda</a></p><p><a href="en/index.html">English help</a></p></main></body></html>\n'
    for path, text in outputs.items():
        if check:
            assert path.exists() and path.read_text(encoding='utf-8') == text, f'Regenerate help: {path.relative_to(ROOT)}'
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding='utf-8', newline='\n')
    print('OK: browser/native HTML help is current' if check else 'Generated Czech/English HTML help')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    generate(parser.parse_args().check)
