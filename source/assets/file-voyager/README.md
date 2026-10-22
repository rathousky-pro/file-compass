# File Voyager — knihovny ikon

V nasazené aplikaci jsou čtyři knihovní soubory:

- `Actions.Fluent.fvicons` — Windows 11, Fluent barevné.
- `Actions.Flat.fvicons` — Moderní ploché.
- `Actions.Outline.fvicons` — Jednoduché obrysové.
- `FileVoyager.Icons.dll` — sedm ikon aplikace: Kompas, Dva panely, Trasa, Tmavý kompas, Disketa – Trasa (návrh 3), Disketa – Orbita (návrh 5), Disketa – Klasik (návrh 6). Každá má deset velikostí 16–256 px.

V **Konfigurace → Ikony** se vybírá sada akcí a samostatně ikona aplikace s náhledem. **Uložit** změní ikony otevřených oken a uloží volbu pro další spuštění. **Storno** ji nezmění. Volba aplikace nemění ikonu souboru EXE ani již vytvořených zástupců.

Tlačítko **Vytvořit zástupce s vybranou ikonou…** uloží zástupce `.lnk` s vybranou ikonou ze společné knihovny. Pro hlavní panel Windows odepněte starou položku a připněte nového zástupce; Windows může u staré připnuté položky nadále zobrazovat její původní ikonu. Knihovnu ani program poté nepřesouvejte. Samotný soubor EXE má pevnou ikonu vloženou při sestavení; jeho ikonu nelze přepnout ve Vlastnostech souboru jako u zástupce.

Pokud chcete změnit i zástupce ve Windows, vyberte ve **Vlastnosti → Zástupce → Změnit ikonu → Procházet** soubor `FileVoyager.Icons.dll`; Windows zobrazí všech sedm variant. Knihovnu ponechte na stálém místě.

FVICONS jsou datové ZIP balíčky s pojmenovanými PNG; aplikace z nich nic nespouští ani nerozbaluje na disk. DLL s ikonami se mapuje pouze pro čtení prostředků, bez spuštění kódu. Grafika aplikace je vlastní; licence převzatých ikon akcí je v `ui/licenses/FLUENT-ICONS-LICENSE.txt`.

Samostatné SVG/PNG/ICO jsou pouze výrobní zdroje v projektu; do distribuované aplikace se nekopírují. `source/BuildIconLibraries.cs` vytváří balíčky a zdroje pro DLL při sestavení.
