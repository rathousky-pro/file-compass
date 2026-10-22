# Nové možnosti File Voyageru

## Hromadné přejmenování

V okně hromadného přejmenování lze do masky vložit údaje EXIF a hudební tagy. Nabídka předvoleb připraví názvy fotografií podle data a času nebo skladeb podle interpreta, čísla a názvu.

| Maska | Význam |
| --- | --- |
| `[EXIF.Date]` | Datum pořízení, `yyyy-MM-dd` |
| `[EXIF.Time]` | Čas pořízení, `HH-mm-ss` |
| `[EXIF.Camera]` | Výrobce a model fotoaparátu |
| `[MP3.Artist]` | Interpret |
| `[MP3.Album]` | Album |
| `[MP3.Title]` | Název skladby |
| `[MP3.Track]` | Číslo skladby, alespoň dvě číslice |
| `[MP3.Year]` | Rok |

Lze kombinovat s existujícím počítadlem `[C]`. Chybějící či nečitelné metadata ponechají soubor pod původním názvem a zobrazí důvod v náhledu. Znaky zakázané v názvech Windows se nahradí podtržítkem. Obsah ani tagy souborů se při čtení nemění.

## Kód a Markdown

- **Náhled** v horní liště a **Ctrl+Q** přepínají společný náhled: pod seznamem v jednom panelu a místo protějšího panelu ve dvojpanelových rozloženích.
- **Ctrl+Shift+Q** je také zkratkou pro stejný náhled.
- Přepínač formátovaného náhledu volí zvýraznění syntaxe / vykreslený Markdown nebo prostý text.
- V nastavení prohlížečů lze zapnout interní náhled kódu a Markdownu také pro **F3**. Výchozí F3 a F4 nadále používají dosavadní nastavení prohlížeče a editoru.

Markdown podporuje nadpisy, seznamy, tabulky a bloky kódu. HTML se nespouští, odkazy nejsou aktivní a vzdálené obrázky se nestahují. Textový náhled zpracovává nejvýše prvních 128 KiB souboru.

## Štítky

**Soubor → Upravit štítky…**, případně stejná volba v místní nabídce, umožňuje štítky přidat, nahradit nebo odebrat označeným položkám. U názvu se zobrazují jejich štítky; rychlý filtr `#Důležité` vybere odpovídající položky. **Příkazy → Soubory podle štítků…** vyhledá také označené soubory z jiných složek.

Štítky jsou v uživatelském profilu, nemění obsah souborů. Přesuny a přejmenování provedené ve File Voyageru zachovají vazbu. Přesuny mimo aplikaci se zatím automaticky nesledují. Nový soubor na původní cestě nepřebírá štítky původního souboru, pokud souborový systém poskytuje identitu souboru.

## Pracovní sady, košík a podsložky

- **Příkazy → Pracovní sady…** uloží pojmenovanou sestavu záložek obou panelů, řazení, pohledů, rozdělení a filtrů. Síťová připojení neobnovují spojení bez pokynu uživatele; hesla se do sady neukládají.
- **Příkazy → Přidat označené do košíku / Výběrový košík** shromáždí odkazy na soubory z různých složek. Delete/F8 v košíku odebere pouze odkazy. Kopírování, přejmenování a archivace pracují se skutečnými soubory.
- **Zobrazit → Obsah včetně podsložek** ukáže soubory rekurzivně v jednom panelu s jejich umístěním. Zpět vede nadřazená položka nebo **Zpět na běžný obsah složky**. Výpis vynechává odkazy na adresáře a má limit 100 000 položek; nepřístupná složka může načítání přerušit s chybou.

Souhrnné výpisy a košík nelze použít jako cílovou složku. V cílovém panelu nejprve otevřete běžný adresář. Načítání lze zrušit klávesou Esc nebo nabídkou Zobrazit.

## Everything a výkon

**Příkazy → Rychlé hledání přes Everything…** využívá index samostatné aplikace Everything, která musí být nainstalovaná a spuštěná. File Voyager dodává pouze její SDK. Dotaz může používat syntaxi Everything; zobrazí nejvýše 20 000 výsledků. Při nedostupnosti zůstává běžné hledání Alt+F7. Pomocný proces má časový limit 15 sekund. V tomto prostředí byla ověřena nedostupnost služby a časové omezení; hledání nad živým indexem vyžaduje ověření s běžícím Everything.

Start používá profilování JIT pro další spuštění. Miniatury kontrolují pouze vytvořené řádky a uvolňují obrázky mimo zobrazenou oblast. Obnovení běžné složky zachovává posun panelu. Přínos startu závisí na disku, antiviru a již vytvořeném profilu.

## Rozložení panelů a jednotný náhled

Horní lišta má pořadí **Obnovit → Panely → Skryté soubory: Ano/Ne → Náhled → Editovat → Miniatury → Komprimovat → Extrahovat → Připojení → Hledat → Oblíbené → Nastavení**. Tlačítko Panely každým kliknutím cyklicky přepíná režimy vedle sebe → pod sebou → jeden panel a ukazuje aktuální rozložení. Nemá rozbalovací nabídku. Skryté soubory přepíná skryté i systémové položky v obou panelech a ukládá volbu do nastavení; filtr názvů zachová. Editovat spouští stejnou akci jako F4. Miniatury mění zobrazení seznamu souborů a neovlivňují náhled souboru.

**Zobrazit → Rozložení panelů** nabízí dva panely vedle sebe, dva panely pod sebou a jeden panel se stromem složek. **Ctrl+H** přepíná dvojpanelové rozložení; **Ctrl+Shift+H** přepíná jeden panel a poslední dvojpanelové rozložení. Jeden panel zachová právě aktivní stranu. Druhý panel si ponechá složku, záložky i připojení a znovu se objeví po návratu do dvojpanelového režimu.

V režimu jednoho panelu je vlevo strom složek, vpravo obsah a pod obsahem detail vybrané položky. Strom používá systémová umístění a ikony Windows: Domů, Plocha, uživatelský profil, registrované cloudové složky, Tento počítač, Síť a další dostupné kořeny (například Knihovny, Koš nebo Linux). Tento počítač obsahuje místní i mapované disky, Síť nalezené servery a jejich sdílení. Domů přebírá složky rychlého přístupu. Názvy a přesměrované cesty přebírá ze systému, včetně Plochy a dokumentů uložených na OneDrivu. Větve se načítají až při rozbalení na pozadí; nedostupné umístění lze opakovaně rozbalit. Strom sleduje aktuální cestu a při obnově zachovává výběr i rozbalené větve. Jeho šířku lze změnit tažením pravého okraje. Systémové přehledy slouží k procházení; pro souborové operace otevřete konkrétní disk nebo složku. **Náhled souboru** rozbalíte nebo sbalíte kliknutím na jeho záhlaví, tlačítkem Náhled nebo Ctrl+Q. Výšku mění dělicí lišta nad detailem. Sbalené záhlaví zůstává dostupné a uvolněný prostor získá seznam souborů. Ve dvou panelech náhled nahrazuje celý protější panel; samostatný spodní detail se tam nezobrazuje.

Ve dvojpanelových rozloženích je strom skrytý a nezabírá místo ani po obnovení starší záložky nebo uloženého pohledu. Volba Strom složek je dostupná v režimu jednoho panelu.

Náhled zobrazuje obrázky, text, zvýrazněný zdrojový kód, Markdown i binární/HEX obsah. Nabídka režimu mění způsob zobrazení; tlačítko Najít otevře hledání a výběr kódování. Podrobnosti uvádějí název, velikost, datum a štítky; plná cesta je v tooltipu. Výběr složky ukáže její podrobnosti. Náhledy se načítají na pozadí až po krátkém ustálení kurzoru. Sbalený nebo skrytý detail další soubory nenačítá.

**Náhled** v liště, **Zobrazit → Náhled**, **Ctrl+Q** a **Ctrl+Shift+Q** ovládají jednu funkci. V jednom panelu se náhled zobrazí pod seznamem, v panelech vedle sebe i pod sebou místo protějšího panelu. Při změně rozložení se otevřený náhled přesune na odpovídající místo a výška spodního náhledu se zachová. Zavřít jej lze stejným tlačítkem, zkratkou nebo Esc. Po zavření se vrátí původní obsah protějšího panelu. Přímé FTP/SFTP náhledy mají dosavadní limity; u SCP a archivů detail odkazuje na F3.

**Konfigurace → Uložit pohled** uloží rozložení, dělicí poměry, viditelnou stranu a nastavení detailu/stromu. Tyto údaje obsahují také pracovní sady. Automatické ukládání při ukončení se řídí stávající volbou ukládání pohledu. Starší konfigurace zůstávají kompatibilní a otevřou dva panely vedle sebe.

## Ověření

Ověřeno: 16 kontrol metadat/štítků/náhledů, 18 kontrol košíku/pracovních sad/virtuálních panelů, 173 kontrol rozhraní, 54 kontrol navigace, 33 kontrol kopírování a 19 kontrol souborových nástrojů. Jazykový test prošel 1563 kontrolami. Náhledy byly rovněž vizuálně zkontrolovány.


## Synchronizace, velikosti, obrázky a dělení souborů (20. 10. 2026)

Obousměrný synchronizační plán s individuálními šipkami, procentuální velikosti v panelu (Alt+Shift+Enter), hromadné úpravy obrázků, rozdělení/sloučení souborů, ověřený přesun do archivu a dělené ZIP/7z archivy popisuje návod ADVANCED-TOOLS.md vedle programu.
