# Překlady File Voyager

Soubor `cs-CZ.lng` je český základ v UTF-8. Používá plochý JSON objekt s čitelnými anglickými klíči, seřazenými abecedně a oddělenými do tematických bloků:

```json
{
  "_language": "en",
  "_name": "English",

  "action.copy": "Copy",
  "action.cut": "Cut",
  "action.paste": "Paste",

  "archive.memoryFailed": "Not enough memory for compression.",
  "copy.preserveAttributes": "Preserve attributes"
}
```

Ukázka je částečný překlad, nikoli celý anglický jazykový balíček. Chybějící položky se zobrazí česky.

## Přehled skupin
- `action.*` – společná tlačítka a příkazy, např. `action.copy`, `action.paste`, `action.cancel`.
- `menu.*`, `toolbar.*`, `functionKey.*` – hlavní nabídky, horní lišta a funkční klávesy.
- `panel.*`, `navigation.*`, `tabs.*`, `column.*`, `drive.*` – panely, cesty, záložky a disky.
- `copy.*`, `transfer.*`, `conflict.*`, `queue.*`, `resume.*`, `history.*` – operace se soubory a jejich průběh.
- `archive.*` – komprimace, rozbalování, hesla a limity archivů.
- `network.*`, `ftp.*`, `ssh.*`, `scp.*`, `smb.*`, `nfs.*`, `sshfs.*`, `proxy.*` – síťové protokoly; `sshfsSetup.*` je instalace podpory SSHFS.
- `search.*`, `rename.*`, `compare.*`, `sync.*`, `attributes.*` – nástroje pro soubory.
- `viewer.*`, `preview.*`, `media.*`, `remotePreview.*`, `remoteEditor.*` – prohlížení a editace.
- `settings.*`, `state.*`, `common.*`, `error.*`, `security.*`, `integrity.*` – nastavení, stavy a společné texty.

## Jak vytvořit překlad
1. Zkopírujte `cs-CZ.lng` například do `en.lng` ve stejné složce.
2. Změňte `_language` na `en` a `_name` na `English`.
3. Překládejte hodnoty vpravo. Klíče vlevo se nemění.
4. Do `languages/language.txt` zapište `en` a restartujte aplikaci. Přednost má případně nastavená proměnná prostředí `COMMANDERCZ_LANGUAGE`.

Lze použít i částečný soubor pouze s přeloženými klíči. Český základ je vždy nutnou součástí aplikace. Prázdná hodnota znamená prázdný text; pro návrat k češtině klíč v překladu vynechte.

Zachovejte úvodní a koncové mezery u fragmentů, nové řádky a případné zástupné symboly `{0}`, `{1}`. Zápisy `\n`, `\r`, `\t`, `\"` a `\\` mají význam JSON. Nepřidávejte komentáře ani čárku za poslední položkou. Soubor má limit 4 MiB.

Hodnoty `internal.sshExitStatusScript`, `proxy.basicAuthorizationHeader`, `clipboard.panelDataFormat` a `ssh.passwordPromptMatch` jsou technické a nepřekládají se; aplikace pro ně používá český základ nezávisle na zvoleném překladu. Je možné je z dalšího jazykového souboru vynechat. Český základ kanonických stavů `L.Code` zachovává hodnoty dosavadní historie a front. Překládat jejich zobrazení v dalších jazycích je možné.

## Starší překlady
Aplikace rozpoznává také původní generované klíče `text.xxxxxxxxxxxxxxxx` a převede je při načtení. Pokud soubor obsahuje starý i nový klíč, nový má přednost. Samotný český katalog už staré klíče neobsahuje.

Ve zdrojovém projektu lze vytvořit čitelnou kopii starého překladu:

```
python tools/language-catalog.py convert stary-en.lng novy-en.lng
```

Původní soubor se nemění. Převodní tabulka je v `tools/language-key-migration.json`; kompatibilita aplikace je součástí sestavení a nevyžaduje další soubor vedle překladu.

## Pro vývojáře
Nový text dostává předem zvolený významový klíč:

```
python tools/language-catalog.py add copy.confirmMove "Přesunout vybrané soubory?"
python tools/language-catalog.py check
```

Kontrola odhalí duplicitní klíče, chybějící odkazy v produkčním kódu a návrat k hashovým názvům. Původní `extract-language.py` už nevytváří hashové klíče a přesměruje na tento nástroj.

`L.Text` načítá zobrazený text, `L.Format` doplňuje argumenty, `L.Code` vrací kanonickou hodnotu a `L.Display` překládá zobrazované stavové hodnoty. Technické příkazy, identifikátory a cesty nesmějí záviset na překladu. Texty Windows a cizích knihoven řídí jejich původce.

Kontroly aplikace: `FileVoyager.exe --language-test <nova-pracovni-slozka>` ověřují celý katalog, UTF-8, částečné překlady, staré klíče, návrat k češtině, neměnné stavy a technické údaje i odmítnutí neplatného JSON.