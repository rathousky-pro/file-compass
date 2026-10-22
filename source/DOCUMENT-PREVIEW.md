# Formátovaný náhled dokumentů

Po dokončení načítání PDF a dalších dokumentů se zobrazuje stav „Náhled dokumentu je připravený“. Při výběru ZIP, RAR nebo 7z v režimu Auto se zobrazuje seznam položek archivu bez rozbalování. Ruční režimy Text/Bin/Hex zůstávají dostupné. Výpis je pouze pro čtení a zobrazuje nejvýše 10 000 položek; platí běžné limity pro čtení archivů.

V režimu Auto a se zapnutým formátovaným náhledem:
- PDF: vestavěný prohlížeč WebView2, listování, přiblížení, hledání.
- DOCX: docx-preview, textové styly, tabulky a vložené obrázky podle možností knihovny.
- XLSX, XLS, XLSM, XLSB a ODS: tabulkový náhled SheetJS, přepínání listů a formátované hodnoty. Makra ani vzorce se nespouštějí. Grafy a úplné původní styly nejsou podporovány.
- Staré DOC a další formáty nadále používají původní textový náhled.

Microsoft Edge WebView2 Runtime se nepřibaluje ani automaticky neinstaluje. Pokud není dostupný, zůstane textový náhled a odkaz na oficiální stránku Microsoftu:
https://developer.microsoft.com/microsoft-edge/webview2/#download-section

Odkaz pouze otevře webový prohlížeč. Po instalaci znovu otevřete náhled.
Také při chybě vykreslení nebo souboru přes 64 MB zůstane textový náhled.
Sešity: nejvýše 200 listů, 2 000 řádků a 100 sloupců na list. Komprimované dokumenty: nejvýše 10 000 položek a 128 MB deklarované rozbalené velikosti.

Soubory se zpracovávají místně. Zobrazovač smí načíst jen vybraný soubor a přibalené skripty; ostatní síťové požadavky, otevírání nových oken a stahování jsou blokované. Přístup ke zdrojovému souboru je pouze pro čtení, soubor se po načtení nezamyká. Cache WebView2 je v config/cache/webview2.

Licence použitých komponent jsou ve složce config/preview. Knihovny jsou přibalené; náhled je nestahuje z CDN.

## Náhled v archivech a přes síť
Stejný panel náhledu lze použít přímo v otevřeném archivu i v FTP/FTPS/SFTP (včetně cloudových připojení, která používají interní SFTP).
Vybraný soubor z archivu se rozbalí do samostatné složky v systémovém tempu. Síťový dokument nebo obrázek se stáhne jako dočasná kopie; texty přes FTP/SFTP nadále načítají pouze začátek souboru. Při přípravě je vidět stav načítání, pak hotový náhled a metadata.
Po změně výběru nebo zavření náhledu se přenos zruší a dočasná kopie odstraní. Náhled nemění zdrojový archiv ani vzdálený soubor.
Pro dočasné kopie platí limit 64 MB. Při rychlém procházení se před rozbalením/stažením čeká 150 ms.
SCP nadále používá otevření souboru přes F3. U šifrované položky musí být heslo již dostupné; chybějící nebo nesprávné heslo zobrazí chybu náhledu.

## HTML a zdrojový kód
HTML, HTM, XHTML a SHTML se v automatickém režimu zobrazují jako stránka s přepínačem „Webová stránka / Zdrojový kód“. Kód používá zvýraznění syntaxe; volba nemění nastavení formátování PDF a kancelářských dokumentů.
Stránka je statický místní náhled: skripty a vzdálené požadavky jsou vypnuté, formuláře, nové stránky a stahování blokované. Místní CSS, obrázky a fonty lze načítat pouze ze složky souboru a jejích podsložek, bez přechodu přes odkazy/reparse body; limit vedlejšího souboru je 8 MB. Soubory jiných typů se stránce nezpřístupňují.
Bez WebView2 nebo při chybě vykreslení zůstane zdrojový kód a odpovídající informace.
V archivu a na FTP/SFTP se přenáší pouze vybraný HTML soubor; samostatné doprovodné CSS/obrázky se automaticky nerozbalují ani nestahují.
