# Sjednocení rozhraní WPF UI

CommanderCZ používá WPF UI 4.3.0, přibalené v bin/ui s jeho závislostmi a licencemi.
Zůstává na .NET Framework 4.8. Sestavení zajišťuje source/build.ps1.

Společné prostředky a velikosti jsou v src/FluentTheme.cs. Tlačítka panelů a horní
lišty vytváří CompactTheme; tlačítka dialogů CopyUi. Výchozí šablony knihovny mají
kompaktní výšky a menu používá její upravenou šablonu ui/CompactMenu.xaml.
Hlavní okno zachovává práci se dvěma panely, označování a funkční klávesy.
Dialogy síťových profilů, kopírování, archivů, hledání a nastavení sdílejí prostředky.

Přejmenování rozlišuje původní názvy a skutečné změny. Bez změn nebo při neplatné
masce je Spustit zakázané. Vrácení změny zůstává samostatnou vedlejší akcí.
Konfigurace → Vzhled a písmo otevírá nové přehledné nastavení včetně tří sad ikon.

Ověření:

- --ui-test: skutečné načtení oken, čitelnost menu a hlavních akcí, výška polí,
  viditelný kompresní poměr, přejmenování beze změn / se změnami a obrázky oken.
  Hlavní okno se renderuje také při šířce 900; přejmenování při měřítku renderu 150 %.
  Jde o kontrolu renderu, nikoliv o změnu DPI monitoru.
- --network-test: profily, šifrované ukládání hesel a obecné i rozšířené stránky
  všech protokolů, mapování disku a síťové kopírování.
- --copy-test: přepisovací pravidla, kontrola obsahu, rušení a chybový dialog.
- --archive-test: tvorba, čtení, změny a ověřování archivů i operace na pozadí.
- --file-tools-test: hledání, přejmenování, vrácení změn a velikosti složek.
- --theme-test a --drive-test: tři sady ikon, systémové ikony a zvýraznění při najetí.
- --self-test a --startup-test: základní operace a skutečné spuštění s obnovou disků.

Náhledy a výsledky jsou v verification/fluent-ui. Původní program je zálohovaný
v backup/before-wpf-ui-20261013-133435. Síťové profily a archivní konfigurace
v uživatelském profilu se při nasazení nepřepisují.

Závěrečné výsledky 13. 10. 2026: UI 27, síťové profily 40, kopírování 31,
archivy 68, souborové nástroje 19, sady ikon 6, disky 9 a základní operace 22
úspěšných kontrol. Navíc prošlo skutečné spuštění programu. Celkem 223 kontrol.

Úprava podle připomínek 13. 10. 2026: zaškrtávátka mají políčko 16 px a menší
vnitřní okraje; kontroluje se, že se celé vejde do ovládacího prvku i u zakázaných
voleb. Seznam připojení má samostatný bílý rámeček. Nabídka disků ukazuje systémové
ikony, písmena, názvy svazků a UNC adresy; odpojené disky mají označený stav.
Metadata aktualizuje stávající kontrola disků na pozadí, otevření nabídky neprovádí
nové dotazy na vzdálené svazky. Výběr nadále používá skutečnou kořenovou cestu.
Náhledy při renderu 100 a 150 procent jsou v verification/ui-polish/ui.

Oprava otevírání nabídky disků: vnitřní ToggleButton má vlastní styl a vyplňuje
celou plochu ComboBoxu. Převzatý implicitní styl jej dříve zmenšoval, takže na něj
kliknutí přes ikonu nebo šipku nedosáhlo. Regresní test kontroluje zásah tlačítka
ve třech bodech (ikona, písmeno, šipka) a jeho OnClick pro otevření i zavření
skutečného popupu v obou panelech. Před opravou test selhal; po opravě prošlo
60 kontrol UI. Výsledky jsou v verification/drive-dropdown-fix.

Jednotná konfigurace a prohlížeče: Konfigurace → Nastavení otevírá šest částí
v jediném okně. Konfigurace → Uložit pohled uchovává samostatné složky panelů
v settings.xml v uživatelském profilu; explicitní počáteční adresář testů má
přednost. Testuje se zápis, opakovaný atomický zápis, obnovení dvou různých složek
a návrat do výchozí složky, pokud uložená chybí. Testovací konfigurace se ukládají
pouze do pracovního adresáře, uživatelská nastavení se při ověření nemění.
F3 přesměrovává obrázky, text a média podle nastavení; F4 používá nastavený editor.
Prohlížeč obrázků načítá na pozadí, uvolní soubor a nabízí přizpůsobení / 100 %.
Ověřeny argumenty pro názvy s mezerami a češtinou, chybějící program, přidružení
Windows a přepínání velikosti obrázku. Externí přehrávače se při testech nespouštějí.
Výsledky v verification/config-viewers: UI 84, souborové nástroje 19,
síťová konfigurace 40 úspěšných kontrol.

Etapa 1–6: asynchronní načítání 7 000 položek, rychlé Zpět/Vpřed, zneplatnění
staré odpovědi, dostupnost ovládání, skutečný přesun mezi C: a E:, zámek,
kolize, přepis, filtr, zrušení při ověřování a zachování zdroje. Společná historie
obsahuje úlohy a soubory, chyby a samostatně zrušitelné čekající úlohy; retry
nemění dokončené soubory. Skutečné okno obnovuje poměr panelů, řazení a šířky
sloupců, které přežijí další změnu velikosti. XML uchovává geometrii a historii
bez přihlašovacích údajů. Nový test --improvements-test má 39 kontrol.
Výsledky: verification/phase1-6.

Body 7–14: nový --navigation-test má 39 funkčních kontrol. Záložky zachovávají
kurzor, označení a výchozí zamčenou složku; serializace ukládá aktivní záložku
a skupiny/dvojice oblíbených. Ověřeno filtrování a masky, FileDrop/Preferred
DropEffect, skutečné kopírování a přesun sdíleným enginem, tri-state atributy,
čas a rušení, reakce sledovače na vytvoření/smazání se zachováním výběru a filtru.
Skutečné FTP/SFTP testy doplňují čtyři scénáře sdílených síťových záložek,
archivní testy tři scénáře návratu do archivu. Kompaktní panel s novými záložkami
je v verification/phase7-14/release-navigation/panels.png. Sada vzhledu: 84 kontrol.

13. 10. 2026 – sloupce a atributy: verification/columns-attributes/final-ui
obsahuje 115 kontrol, včetně skutečných událostí tažení všech deseti úchytů
sloupců, zachování ručních šířek při změně okna a načtení atributů a časů.
Náhledy attributes.png a attributes-150.png byly vizuálně zkontrolovány.
Funkční sada final-navigation: 53 kontrol; zahrnuje volitelné podsložky,
jediné zpracování překrývajících se výběrů a správnou změnu času složek.
Sestava prošla také testem skutečného spuštění.

Sdílené sloupce: verification/shared-columns/ui obsahuje 128 kontrol.
Tažení každého sloupce v levém i pravém panelu mění oba panely. Zachování
šířek je ověřeno po změně velikosti okna, serializaci pohledu do XML a
vytvoření nového okna s načteným nastavením i odlišnými starými šířkami
záložek. Ověřeno pravé zarovnání dat. Funkční sada navigation: 53 kontrol.

14. 10. 2026 – kontrastní ovládací prvky a sekce: verification/gray-sections.
Společné pole, heslo a roletka jsou bílé; zaškrtávátko má bílý čtverec,
modrou značku a samostatný šedý stav beze změny. Skupiny mají šedé pozadí
a ohraničení. Nastavení, hledání, síťové profily a archivy jsou členěny
do pojmenovaných sekcí; přejmenování zachovává čtyři souběžné sekce.
Sada UI: 146 kontrol (včetně hranic a barev zaškrtávátek); síťové profily
45 (včetně přepínání všech protokolů); funkční navigace 53.
Vizuálně zkontrolovány rename.png, pack.png, search.png, configuration-3.png
a network-FTP.png; sada obsahuje také náhledy v měřítku 150 %.

16. 10. 2026 – místní kopírování: verification/local-copy-ux.
Samostatný světle modrý ukazatel souboru a větší tmavě modrý ukazatel celé
úlohy, procenta, rychlost, odhad zbývajícího času, počty a tlačítko Minimalizovat.
Pevné rozměry a zkrácené cesty brání změnám velikosti při přechodu mezi soubory.
Automatická obnova panelů počká, dokud je kopírovací okno v popředí; události
se nezahodí. Obnova seznamu nepřebírá fokus z jiného aktivního okna.
Nová sada final-local-copy-ui: 16 kontrol skutečného kopírování, metrik,
barev pixelů, stabilních rozměrů, události FileSystemWatcher, aktivace oken,
minimalizace a pauzy. Náhled local-copy-progress.png vizuálně zkontrolován.
Regrese: UI 147, místní kopie 31, stav nativních oken 40, navigace 53 a spuštění.
Podrobnosti: ../reports/Mistni-kopirovani-rychlost-vzhled.md.
