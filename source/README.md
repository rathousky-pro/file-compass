# CommanderCZ

Vlastní český dvoupanelový správce souborů pro Windows. První funkční základ,
nikoliv hotová náhrada všech funkcí Total Commanderu. Žádné součásti Total
Commanderu ani jeho licenční mechanismus nepoužívá.

## Spuštění

Z hlavní složky otevřete `bin\CommanderCZ.exe` (ze složky `source` jde o `..\bin\CommanderCZ.exe`). Program nemá instalátor, nic nemění v registru
a vyžaduje Windows s .NET Framework 4.8. První sestavení je pro x64.
Oba panely začínají ve složce projektu. Jinou složku zvolte zadáním cesty
nebo výběrem disku nad panelem.

## Co funguje

- Menu **Síťová připojení**: správa uložených profilů SMB, FTP, SFTP, SCP, NFS a SSH,
  vytvoření, úprava, odstranění a otevření v aktivním panelu.
- Sjednocený kompaktní vzhled **WPF UI 4.3.0**: hlavní okno, seznamy i dialogy
  používají **Segoe UI 9 pt**, stejné ovládací prvky, mezery a zvýraznění akcí.
  Knihovny jsou přibalené ve složce `bin\ui`; nic se neinstaluje samostatně.
  **Konfigurace → Vzhled a písmo…** ukazuje společné písmo a umožňuje vybrat jednu
  ze tří sad ikon. WPF škáluje podle nastavení obrazovky ve Windows.

- Dva panely, disky, cesta zadávaná ručně, návrat zpět a do nadřazené složky.
- Názvy, velikosti a datum změny; složky jsou před soubory.
- Kompaktní vzhled podle dodané předlohy: lišta disků v každém panelu,
  volné místo a kapacita disku, modrý řádek cesty a pět sloupců
  (Název, Přípona, Velikost, Datum, Atributy). Velikost souboru je v bajtech.
- Kliknutí na záhlaví řadí seznam, opakované kliknutí obrátí směr.
  Nadřazená složka zůstává první a složky před soubory.
- Rozbalovací šipka vedle cesty otevírá historii navštívených složek;
  Alt+šipka vlevo v seznamu vrací předchozí složku.
- Horní menu, barevná lišta nástrojů, systémové ikony souborů, spodní příkazový
  řádek a funkční klávesy přes šířku okna. Aplikace má vlastní modrou ikonu.
- Pravé kliknutí zapíná/vypíná červené označení celého řádku. Tažení pravým
  tlačítkem označuje další položky; začnete-li na označeném řádku, označení ruší.
- Modrý kurzor je oddělený od červeného označení. F5/F6 pracují s označenými
  položkami, nebo s aktuálním řádkem, pokud nic označeno není.
- Insert nebo mezerník přepíná označení; Insert posune kurzor o řádek dál.
  Ctrl+A označí vše, numerické * obrátí označení. Ctrl+klik přepíná označení,
  Shift+klik označí rozsah. Tab přepíná panel. Obnovení zachová existující označení.
- Enter / dvojklik otevře složku nebo soubor v přidružené aplikaci.
- F3 otevře soubor v přidružené aplikaci (interní prohlížeč zatím není).
- F4 otevře aktuální soubor v Poznámkovém bloku.
- F5 otevírá dialog kopírování s upravitelným cílem, filtrem a rozšířenými volbami.
- F6 přesouvá položky do protějšího panelu.
- F7 vytvoří složku, F2 nebo Shift+F6 přejmenuje jednu položku, Ctrl+R obnoví oba panely.
- Zpět a vpřed mají samostatnou historii v každém panelu (Alt+šipky vlevo/vpravo).
- Příkazový řádek spustí zadaný příkaz pomocí cmd.exe v aktivní složce po Enter.
- Kopírování běží mimo vlákno rozhraní; ukazuje průběh jednotlivého souboru i celé úlohy.
- Kolize kopírování a přesunu F6 řeší vlastní dialog; složky lze slučovat a volit přepis či přeskočení.
- Při chybě kopírování souboru (například zámek jiným procesem) se zobrazí zdroj,
  cíl a důvod chyby. **Přeskočit** pokračuje dalším souborem, **Znovu** opakuje
  daný soubor, **Přeskočit vše** přeskočí i další chyby aktuální úlohy a
  **Přerušit** zastaví aktuální úlohu. Platí také při FTP/SFTP/SCP přenosu souborů.

## Síťová připojení

Editor připojení má kompaktní karty **Obecné** a **Upřesnit**. Hostitele můžete
zadat jako `server:port`, IPv6 s portem jako `[adresa]:port`. Místní složka z profilu
se po připojení otevře v protějším panelu. Prázdná zachová jeho aktuální cestu.

FTP nabízí pasivní i aktivní režim, explicitní nebo implicitní TLS, anonymní přihlášení,
SOCKS4/SOCKS5/HTTP CONNECT proxy, časový limit, příkazy po přihlášení (jeden na řádek),
kódování názvů, MLSD, výpis skrytých souborů a klientský certifikát. Automatické
rozpoznání je výchozí pro kódování a výpis složek. Proxy heslo se ukládá šifrovaně.
SFTP/SCP nabízí také proxy, kódování názvů, klíč serveru, kompresi, SSH agenta a udržování spojení.
Volby PowerShellu, komprese, časový limit, agent a interval udržování spojení se
použijí i při otevření SSH terminálu. Agent a interval platí také pro souborové SFTP/SCP relace.

**Síťová připojení → Mapovat síťový disk…** a ikona URL v horní liště otevírají
vlastní dialog CommanderCZ, stejně jako ikona sítě u panelů. Vyberte volné písmeno,
zadejte `\\\\server\\sdileni`, případně jiné přihlašovací údaje. Volba znovu připojit
při přihlášení uloží mapování prostřednictvím síťových služeb Windows. Existující
disk se nepřepisuje. Připojený disk se otevře v aktivním panelu a objeví v obou lištách.

Otevřete **Síťová připojení → Správa připojení… → Nové připojení…**. Zvolte protokol,
pojmenujte profil a vyplňte server a cestu. Tlačítko **Připojit** otevře profil
v aktivním panelu. Správu lze otevřít také tlačítkem FTP na liště.

- **SMB:** server např. nas, sdílená složka data nebo data\fotky.
  Prázdný uživatel používá účet Windows; lze zadat DOMENA\uzivatel.
  Připojuje se bez přiřazení písmene disku.
- **FTP:** server, port (standardně 21), uživatel, heslo a počáteční cesta.
  Prázdný uživatel znamená anonymní přihlášení. Výchozí je pasivní režim, lze zvolit aktivní.
  Volba TLS zapíná explicitní FTPS s kontrolou certifikátu serveru.
- **SFTP:** server, port (standardně 22), uživatel, heslo nebo soukromý klíč
  bez hesla. Při prvním připojení se zobrazí SSH otisk k porovnání se správcem
  serveru. Po potvrzení a úspěšném otevření se uloží; změněný klíč se odmítne.
- **SCP:** server, port (standardně 22), uživatel, heslo nebo soukromý klíč,
  počáteční složka a SSH otisk. Připojení používá skutečný SCP protokol přes libssh2,
  prochází složky v panelu a přenáší soubory i složky přes F5. Profil se ukládá
  stejně jako SFTP a heslo lze uložit šifrovaně. Při prvním připojení se ověří
  a uloží otisk serverového klíče. Na Unix/Linux serveru je nutný přístup k shellu,
  dostupný příkaz scp a obvyklé příkazy pro práci se soubory (např. ls).
  Účet omezený jen na SFTP nemusí SCP podporovat. Program při chybě SCP
  automaticky nepřepíná na jiný protokol.
  Požadavky: https://winscp.net/eng/docs/requirements
- **SSH:** server, port (standardně 22), uživatel (prázdný použije nastavení
  klienta), volitelný soukromý klíč a volba PowerShellu. Tlačítko **Otevřít SSH**
  spustí samostatný interaktivní terminál. Vestavěný Windows PowerShell je výchozí;
  volitelně lze použít PowerShell 7. Windows Terminal se použije, pokud je dostupný,
  jinak se otevře samostatné okno PowerShellu. Po odhlášení SSH zůstane PowerShell otevřený.
- **NFS:** server, export např. /volume1/data, volné písmeno disku a režim
  anonymní identity. Vyžaduje zapnutý **Klient pro NFS** ve Windows a používá
  jeho podporované verze protokolu a nastavení identity. Program Windows funkce
  sám nezapíná. Chybějící klient nebo obsazený disk se hlásí v dialogu.

Profily se ukládají do **%APPDATA%\CommanderCZ\connections.xml**.
Přesná cesta je vidět v okně správy. Konfigurace se načítá po dalším spuštění
a zapisuje atomickou náhradou souboru. Při chybě načítání se soubor nepřepisuje.
Heslo se uloží jen se zaškrtnutou volbou **Uložit heslo šifrovaně**; chrání je
Windows DPAPI pro aktuální účet. Bez této volby se heslo vyžádá při připojení.
Také přihlašovací dialog nabízí **Uložit heslo pro toto připojení**. Heslo se
uloží až po úspěšném připojení; příště se již nevyžaduje.
V editoru prázdné pole zachová již uložené heslo; odškrtnutí volby je odstraní.
Konfiguraci lze přenést, ale šifrovaná hesla bude potřeba zadat znovu pod jiným účtem.

### SSH terminál ze SFTP nebo SCP profilu

Vyberte uložené SFTP nebo SCP připojení a klikněte na **SSH terminál**. Použije se stejný
server, port, uživatel a soukromý klíč. V menu je také **SSH terminál aktivního SFTP/SCP…**
pro server aktuálně otevřený v panelu. Samostatný SSH profil slouží pro stroje,
u kterých nechcete používat souborový panel. Pro protokoly FTP, SMB a NFS se SSH
ze stejných údajů automaticky neodvozuje, protože mohou používat jiné služby a účty.

SSH používá systémový klient OpenSSH; pokud chybí, program ukáže, kterou volitelnou
funkci Windows zapnout. Heslo ani heslo klíče se nepředává v příkazové řádce.
SSH profily heslo neukládají. U SFTP/SCP se jeho uložené heslo pro terminál nepoužije:
OpenSSH si případné heslo vyžádá přímo v terminálu. Pro přihlášení bez opakovaného
psaní hesla použijte SSH klíč, případně Windows ssh-agent. Ten spravuje klíč pro
OpenSSH a lze jej používat i s klíčem chráněným heslem.

Ověření SSH klíče serveru v terminálu řídí OpenSSH (jeho known_hosts); otisk
ověřený pro SFTP/SCP v CommanderCZ se tam automaticky nepřepisuje. Při prvním SSH přihlášení
tedy můžete být vyzváni k samostatnému potvrzení klíče. PowerShell běží lokálně,
po přihlášení SSH se používá shell nastavený na vzdáleném serveru.
Dokumentace: https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_keymanagement

FTP/SFTP/SCP podporuje procházení, historii, řazení, označení, F3 (stažená lokální
kopie), F5 (soubory a složky mezi serverem a lokálním/SMB/NFS panelem), F7
a přejmenování. Před F5 zvolíte **Kopírovat a přeskočit existující soubory** nebo
**Kopírovat a přepsat existující soubory**. Volba platí také uvnitř složek:
existující složky se sloučí a nové soubory se zkopírují. Přepis nepovoluje náhradu
souboru složkou ani složky souborem. Síťové přenosy běží mimo vlákno
rozhraní na samostatném připojení. Panely i příkazy zůstávají dostupné; změna
složky nebo odpojení panelu nemění cíl již spuštěného přenosu. Dole přímo nad
funkčními tlačítky je průběh souboru a celé úlohy, rychlost a tlačítko Zrušit.
Oba průběhy sdílejí jeden pruh: horní světle modrá část označuje aktuální soubor,
spodní tmavě modrá část celou úlohu.
Další výběr ze stejného uloženého připojení se přidává k běžící úloze stejného
směru (stahování nebo nahrávání). Nejdříve se na pozadí načtou soubory a velikosti,
pak se zvětší fronta i celkový objem dat; aktuální přenos se nepřeruší. Celkové
procento může po přidání dalších dat klesnout. Cíl a volba přepsat/přeskočit se
uchovávají samostatně pro každý přidaný výběr. Stejná položka se stejným cílem
se v jedné úloze nezařadí podruhé.
Různá připojení a opačné směry mohou běžet současně, například FTP a SFTP.
Kliknutí na text nebo pruh průběhu otevře **Síťovou frontu kopírování**, dostupnou
také přes **Příkazy → Síťová fronta kopírování**. Horní seznam ukazuje všechny
síťové úlohy; po výběru jsou dole soubory a složky, cíle, velikosti, pravidla pro
existující soubory a aktuální stav. Lze zrušit vybraný přenos nebo všechny přenosy.
Síťová fronta nabízí pozastavení/pokračování, limit rychlosti v KiB/s,
pořadí čekajících souborů a kořenů a zrušení jednotlivé položky.
Síťové F6 odstraní jen úspěšně přenesené zdroje po kontrole; přeskočené
a chybné zdroje zůstanou. Na stejném serveru se pro chybějící cíl použije
přejmenování. F4 otevře pracovní kopii v editoru a nabídne Uložit na server
s kontrolou souběžných změn. Pracovní kopie se uchovává v AppData.
Dočasné výpadky přenosů i načítání fronty mají nejvýše tři automatické
pokusy o obnovení. Kopírování se ukáže v samostatném okně; do spodní lišty
se přesune až po minimalizaci. Platí i pro místní F5/F6.
Podrobnosti: ../reports/Sitove-F6-F4-fronta-obnova.md.

Seznam připojení má kompaktní rozložení se seznamem vlevo a tlačítky vpravo.
Profil lze také duplikovat a upravit před uložením.
Otevřené servery se zobrazují na konci lišty disků daného panelu. Ikona zůstává
i po přepnutí na lokální disk; kliknutím se vrátíte do poslední složky serveru.
Pravým tlačítkem → **Odpojit** zavřete pouze vybrané připojení a odstraníte jeho
ikonu. Samostatně běžící FTP/SFTP/SCP kopie pokračují. Ukončení programu zavře
všechna otevřená i odložená FTP/SFTP/SCP spojení.
**Odpojit aktivní panel** zavře FTP/SFTP/SCP relaci a vrátí lokální složku.
U NFS odpojí disk připojený tímto panelem. Pouhé ukončení aplikace ponechá NFS
disk připojený ve Windows. U SMB vrátí lokální složku, ale neruší systémovou
SMB relaci, kterou mohou používat další aplikace.

Síťové přenosy běží přes **plugins/network/CommanderNetwork.dll**, která
obsahuje libcurl pro FTP/FTPS a libssh2 pro SFTP/SCP včetně SSH operací. WinSCP.exe ani WinSCPnet.dll již nejsou
potřeba. Sestavovací skript kopíruje pluginy, licence a konfiguraci načítání automaticky.
Neodstraňujte CommanderCZ.exe.config: obsahuje cesty a verze těchto knihoven.
Uložené profily a šifrovaná hesla zůstávají kompatibilní. Souborová SSH připojení
ověřují uložený otisk serveru a používají heslo nebo soukromý klíč ve formátu
podporovaném libssh2 (například OpenSSH/PEM). SSH agent je dostupný také pro SFTP/SCP.
Podrobnosti jsou v THIRD-PARTY-NOTICES.md a NETWORK-PLUGINS.md.

## Kopírování

- Cíl: libovolná složka, koncové `\*.*` zachová názvy. Pro změnu názvu jediného
  souboru zapněte v Možnostech „Cílová cesta obsahuje nový název jediného souboru“.
  Strom otevře výběr složky; +F7 vytvoří cílovou podsložku.
- Filtr: např. `*.jpg;*.png`, vyloučení `*.* | *.tmp;*.bak`. Týká se souborů
  i uvnitř vybraných složek. +F8 nabídne předvolby.
- Při kolizi: Přepsat, Přepsat vše, Přeskočit, Přeskočit vše, Storno,
  Přepsat pouze starší, Přejmenovat kopii, Připojit bajty zdroje na konec cíle.
- Více možností: přepsat odlišné podle velikosti/času, přepsat pouze větší nebo
  menší zdroje, automaticky přejmenovat kolidující kopie. Podmínkové a hromadné
  volby platí do konce jedné úlohy. Nová úloha se řídí svými nastaveními.
- Starší znamená přísně starší čas změny cíle než zdroje; shodné časy se přeskočí.
  Miniatury umožňují porovnat podporované obrázky v dialogu kolize.
- Možnosti: zachování času změny a atributů, přepis souborů jen pro čtení,
  přeskočení skrytých/systémových položek a kopírování prázdných složek.
- Ověřit SHA-256 kontroluje dokončený dočasný soubor proti skutečně přečteným
  bajtům. Při připojování ověřuje i původní obsah cíle.
  U mapovaných síťových disků a UNC cest se při přesunu automaticky nezapíná;
  před odstraněním zdroje se kontroluje délka kopie a nezměněný zdroj.
  Výslovně zvolené ověření znovu čte celou kopii a může být na síti pomalé.
  Přerušené kopírování přes mapovaný disk zatím nelze navázat.
- Kopírovat práva NTFS přenáší přístupová oprávnění (DACL) souborů a nově
  vytvořených složek. Nepřenáší vlastníka ani audit. Práva existujících sloučených
  složek zůstávají zachována. Nedostatečná oprávnění se ohlásí jako chyba.
- F2 Do fronty zařadí kopírování za běžící úlohu. Na neobsazené frontě začne
  ihned na pozadí. Příkazy → Fronta kopírování umožňuje zrušit čekající úlohy
  nebo celou frontu. Kopie se provádějí postupně.
- Lokální kopie zobrazují průběh přímo dole nad funkčními tlačítky a běží na pozadí.
  Podrobný průběh: Pozastavit/Pokračovat, Storno aktuální úlohy, Na pozadí. Kliknutí na
  stavový text v hlavním okně průběh znovu otevře. Další úlohy lze přidávat přes F5.
- Přepis i připojení obsahu používají dočasný soubor na cílovém disku. Původní
  soubor se nahradí až po dokončení a případném ověření. Je potřeba místo pro
  celou novou kopii; při připojování také pro původní obsah. Zrušení nevrací již
  dokončené soubory. Změna cíle jiným procesem během kopírování zastaví přepis.

## Známá omezení

Přesun F6 mezi vzdálenými FTP/SFTP/SCP panely a přímý přenos server–server
nejsou implementované. Strom, miniatury a synchronizace mají neaktivní ovládání
s vysvětlením. Záložky a oblíbené složky nejsou součástí této etapy.
Kopírování a přesun reparse pointů (odkazy, junctiony, některé cloudové soubory)
se odmítají. Kopírovací přesun mezi svazky nepřenáší alternativní datové proudy,
vlastníka, audit ani vztahy hardlinků. Chyba nebo zrušení může zanechat v cíli
již dokončené soubory; přeskočené a neúspěšné zdroje zůstávají zachované.
Přepis vyžaduje atomické nahrazení na cílovém systému; při nepodporovaném přepisu
zůstane původní cíl zachovaný.

## Sestavení

V PowerShellu z hlavní složky projektu spusťte `powershell -ExecutionPolicy Bypass -File .\source\build.ps1`.
Výsledný program, jeho konfigurace, knihovny a pluginy se sestaví do hlavní podsložky `bin`, vedle složky `source`.
Skript používá kompilátor dodávaný s .NET Framework, není potřeba instalovat SDK.
`CommanderCZ.csproj` umožňuje další vývoj ve Visual Studiu s podporou .NET Framework 4.8.

## Ověření

Spuštění `bin\CommanderCZ.exe --self-test C:\cesta\k\pracovni\slozce` provede
testy kopírování, kolizí, přesunu, přejmenování, neplatných názvů, označování pravým
tlačítkem, výběru pro operace a barev označených řádků ve WPF okně.
Test vytváří vlastní dočasnou podsložku, po dokončení ji odstraní a zapíše `test-result.txt`.
Samostatný přepínač `--copy-test` se stejným argumentem testuje přepis,
podmíněné kolize, filtry, práva, zrušení, pozastavení a vykreslení kopírovacích dialogů.
Výsledky ukládá do `copy-test-result.txt`.

Přepínač --network-test se stejným argumentem ověřuje profily všech šesti protokolů,
zápis/čtení konfigurace, šifrování hesel, validaci a písma. Ukládá také PNG náhledy dialogů.
U SCP ověřuje také výběr správného protokolu a předání portu, uživatele,
hesla a serverového otisku. Přepínač --scp-test slozka port ověřuje skutečné
SCP přenosy, přepis, zrušení, mazání a procházení přes SSH. Testovací SCP server
v tests/scp_fixture.py neposkytuje SFTP; ověřuje se, že se jej klient nesnaží použít.
--ftp-test slozka port a --sftp-test slozka port ověřují skutečné připojení
na 127.0.0.1 s testovacím účtem tester, heslem test-password.
Testovací servery v tests vyžadují Python s pyftpdlib, resp. paramiko;
první argument skriptu je adresář těchto závislostí, druhý testovací kořen,
třetí soubor pro port. Nejde o servery určené pro běžné používání.
--ssh-test slozka testuje uložení SSH konfigurace, volbu terminálu a předání
argumentů přes skutečný PowerShell a OpenSSH. Nekontaktuje vzdálený server:
ověřuje správné předání argumentů, včetně češtiny, mezer a speciálních znaků,
bez předávání hesel nebo nechtěného spuštění dalších příkazů.

## Další etapy

Konfigurace → Nastavení otevře jedno okno s částmi Vzhled, Ikony, Archivy,
Prohlížeče, Pohled při spuštění a Síťová připojení. Cesty k externím programům
lze zadat nebo vybrat; Media Player Classic a WinRAR lze vyhledat automaticky.

Konfigurace → Uložit pohled zapíše aktuální místní složky nebo připojené síťové
disky obou panelů do %APPDATA%\CommanderCZ\settings.xml. Při příštím spuštění
se obnoví každá složka samostatně. Nedostupná složka se nahradí výchozí složkou.
Relace FTP/SFTP/SCP a vnitřní cesty archivů se tímto příkazem neobnovují.

F3 používá prohlížeč podle typu: vestavěný náhled obrázků (přizpůsobit / 100 %),
Notepad nebo zvolený editor pro text a zvolený přehrávač pro zvuk a video.
Pokud není přehrávač nastaven, vyhledá se MPC-HC v obvyklých instalačních
složkách nebo bin\plugins\mpc-hc; jinak se použije přidružení Windows.
MPC-HC není součástí balíčku. GIF se zobrazuje jako statický náhled.
F4 používá nastavený textový editor. Enter a dvojklik nadále otevírají soubory
pomocí přidružení Windows a archivy v panelu. Soubor pro F3 ze sítě se nejprve
stáhne do TEMP; soubor z archivu se rozbalí podle stávajícího postupu.

1. Koš a rozšíření přesunu o možnosti dostupné při kopírování.
2. Záložky, oblíbené složky, nastavení a asynchronní načítání adresářů.
3. Vyhledávání, hromadné přejmenování a interní prohlížeč.
4. Archivy, porovnání a synchronizace složek.
5. Rozšířené FTP/SFTP přenosy, vzdálená editace a vlastní pluginové rozhraní;
   kompatibilitu s pluginy TC posoudit samostatně.

## Etapa 1–6 (13. 10. 2026)

- SSH agent a udržování spojení platí pro SFTP/SCP i terminál. Neúspěšné přihlášení
  agentem/klíčem nabídne heslo. Udržovací pakety běží na pozadí a nesouběží s
  operacemi téže SSH relace. SMB/NFS nadále používají Windows.
- F6 používá stejnou frontu, kolize, průběh, pozastavení a zrušení jako F5.
  Na stejném svazku a bez změny pravidel souborů může použít rychlé přejmenování.
  Mezi svazky kopíruje a ověří obsah; zdroj se odstraní až po ověření kopie,
  pod otevřeným souborovým handlem, který brání změně obsahu a záměně zdroje.
- Místní a mapované složky, metadata, ikony a kapacita se načítají mimo UI.
  Nová navigace zneplatní předchozí odpověď. Nereagující složka po 15 sekundách
  uvolní panel; samotné blokované systémové volání může doběhnout na pozadí.
- Společný přehled nabízí stav a zrušení místního kopírování, přesunu, síťových
  přenosů, mazání a archivů. Čekající místní úlohy jsou také v historii.
- Konfigurace → Historie operací uchovává výsledek, přeskočení a chyby v
  %APPDATA%/CommanderCZ/operations.xml. Nabízí zrušení vybrané operace,
  opakování chybných položek a export textového protokolu. Na disku se uchovává
  nejvýše 300 posledních záznamů. Při zavření se dokončí čekající zápis historie.
  Po restartu lze opakovat místní chybná kopírování/přesuny. Síťové a archivní
  opakování vyžaduje původní otevřenou relaci/zdroje v aktuálním běhu; hesla a
  obslužné funkce se do historie neukládají. Archivní změna je jednou úlohou;
  u více samostatných archivů má každý vlastní záznam.
- Konfigurace → Uložit pohled nyní ukládá také polohu/velikost/maximalizaci okna,
  poměr panelů, řazení a šířky sloupců. Body 7–14 doplňují uložené záložky;
  vzdálené a archivní záložky se po spuštění otevřou explicitním kliknutím.
  Nedostupná uložená složka nezablokuje spuštění;
  po neúspěšném načtení se použije výchozí složka.

Podrobnosti a výsledky ověření: ../reports/Upravy-body-1-6.md.

## Body 7–14: navigace a každodenní práce

- Záložky: Ctrl+T / Ctrl+W, přepínání Ctrl+Tab, pravé menu pro pořadí a zámek.
- Oblíbené: Ctrl+D, skupiny a uložené dvojice panelů; hvězda v liště.
- Klikací cesta, Ctrl+L, doplňování místních podsložek a stejná složka ve druhém panelu.
- Psaním filtr názvů; Ctrl+F otevře filtr/hledání, Escape ho zruší.
- Num +/−, masky, stejné přípony, Shift rozsah a obnovení označení.
- Ctrl+C/X/V, Ctrl+Shift+N názvy, Ctrl+Shift+C cesty. Přetahování používá
  stávající konfliktní dialogy a bezpečné pozadí. Virtuální schránkový výběr
  platí v aktuální instanci; vzdálené/archivní přesuny zůstávají pro body 20/22.
- Alt+Enter vlastnosti, Otevřít pomocí, vlastní atributy a časy. Kontextové
  akce: Shift+F10 nebo podržení pravého tlačítka; krátký pravý klik označuje.
  Dialog atributů načte aktuální společné hodnoty výběru; šedý stav ponechá
  atribut beze změny. Nabízí podsložky, samostatné datum a čas změny, Aktuální,
  další časy a načtení hodnot z položky pod kurzorem v aktivním či cílovém
  panelu. Odkazy se neprocházejí; úprava běží na pozadí s chybami v historii.
  Doplňkové atributy pluginů Total Commanderu nejsou podporované.
- Šířky všech sloupců se mění tažením za pravý okraj záhlaví. Ruční šířky
  se současně mění v obou panelech, zůstávají při změně velikosti okna
  a přepnutí záložky. Konfigurace → Uložit pohled je uloží jako společné
  nastavení a při dalším spuštění se obnoví. Datum je zarovnáno doprava.
- Společný vzhled: bílé vstupy, roletky a zaškrtávátka na šedém pozadí.
  Logické sekce přejmenování, nastavení, hledání, archivů a síťových profilů
  mají šedé ohraničené bloky. Modrá barva označuje výběr, fokus a hlavní akci.
- Příkazy → Porovnat složky / Synchronizovat složky: jednosměrný plán,
  volitelná kontrola obsahu, prázdné složky, volitelné zrcadlení a historie.
  Dostupné pro místní složky a mapované disky; odkazy se neprocházejí.
- Ctrl+Q: náhled obrázku nebo prvních 128 KiB textu ve druhém panelu,
  hledání a volba kódování. Zavření vrátí původní obsah panelu.
  Podrobnosti: ../reports/Porovnani-synchronizace-nahled.md.
- Automatické sledování místních složek zachová kurzor, označení a filtr.
  Nastavení → Vzhled dovoluje vypnutí a samostatné povolení pro síťové disky.
- Výběr disku převezme místní složku druhého panelu, pokud je na stejném disku.
  Tlačítko kořene nadále otevře kořen. F6 u jedné položky rozpozná nový název:
  samotný název přejmenuje na místě, konkrétní cesta přesune s přejmenováním;
  koncové zpětné lomítko nebo *.* označuje cílovou složku. Po obnově seznamu
  se zachová ovládání klávesovými šipkami.

Podrobnosti, omezení a 458 kontrol: ../reports/Upravy-body-7-14.md.

Ukazatel operací používá světlý text i při malém celkovém průběhu. Ukončené
úlohy se automaticky odstraní z pruhu a detailu a zůstanou v historii operací.
Síťová fronta obsahuje pouze probíhající úlohy a po dokončení se zavře.
Před tlačítkem Zrušit se zobrazuje součet rychlostí síťových přenosů a odhad
zbývajícího času všech jejich front. Při přípravě, ověřování nebo nulové
rychlosti je odhad neznámý; po přidání dalších souborů se přepočítá.
Síťové kopírování nabízí „Kopírovat a navázat“ pro jednotlivé soubory i celé
složky. FTP/FTPS a SFTP pokračují v rozpracovaných kopiích po rychlé kontrole i po
restartu; hotové soubory přeskočí, chybějící přenesou celé. SCP nehotový soubor
přenese od začátku. „Kopírovat a přepsat“ zahodí vlastní předchozí rozpracovanou
kopii pro daný zdroj/cíl a zahájí nový přenos. Záznamy bez hesel:
%LOCALAPPDATA%/CommanderCZ/transfers. Podrobnosti a omezení ověření FTPS:
../reports/Navazovani-sitovych-prenosu.md.

Nahrávání na FTP/SFTP čte a kontroluje zdroj po blocích během přenosu.
Před začátkem nečte celý soubor ze síťového disku kvůli kontrolnímu otisku.
Při navázání se kontroluje velikost, čas a záznam průběhu. Ze zdroje nebo
místní rozpracované kopie se ověřují první a poslední uložené bloky (do 2 MiB).
Při nahrávání se se zdrojem porovná nejvýše 128 KiB ze začátku a konce
rozpracovaného souboru na serveru; celý dosavadní přenos se znovu nestahuje.
Zkrácený hraniční blok může vyžadovat další čtení do 1 MiB ze zdroje.
Tato rychlá kontrola není úplnou kontrolou obsahu: změna uprostřed při
zachování velikosti a času může zůstat nezjištěna. Podporované zůstávají
také záznamy ze starších verzí. Podrobnosti: ../reports/Rychle-navazovani.md.

F5 mezi dvěma vzdálenými panely stahuje a nahrává současně přes omezený
paměťový buffer 512 KiB; celý soubor se na místní disk neukládá.
Podporuje FTP/FTPS, SFTP a SCP v jedné frontě s oběma servery,
pravidlem přeskočit/přepsat, rušením a přidáním dalších položek do stejné
dvojice. Celkový objem počítá stažení i nahrání; zdroj se nikdy nemaže.
FTP/SFTP mezi servery podporuje přerušení s ponecháním rozpracované části
a F5 → Kopírovat a navázat. Kontrola čte nejvýše 128 KiB z každého serveru;
SCP nedokončené soubory přenáší od začátku. Zrušit nabízí i odstranění
rozpracovaných dat. Podrobnosti:
../reports/Meziserverove-kopirovani.md.

Síťová připojení mají společnou správu; filtr Protokol nabízí Všechny,
FTP, SFTP, SCP, SMB, NFS, SSH a SSHFS. Nový profil použije vybraný protokol.
Návrat přes ikonu připojení do poslední složky při chybě jednou obnoví
samostatné spojení panelu. Běžící přenosy mají vlastní relace a pokračují.
U FTP se staré spojení panelu před obnovením uvolní. Pokud složka zůstane
nedostupná, program nabídne kořen; odmítnutí zachová dosavadní panel.
Chyba uvádí připojení, cestu a dostupnou odpověď FTP serveru.
SSHFS připojí vzdálenou složku jako disk Windows přes SSHFS-Win a WinFsp.
Profil ukládá server, port, uživatele, absolutní vzdálenou složku, písmeno
disku, šifrované heslo nebo cestu ke klíči, otisk serveru a volby připojení
při spuštění / ponechání disku po zavření. Automatické připojení vyžaduje
uložený otisk a přihlašovací údaje; při startu neotevírá přihlašovací dialog.
V liště disků má SSHFS adresu v tooltipu a vlastní nabídku Odpojit,
Znovu připojit, Upravit připojení a SSH terminál.
Existující SSHFS disk vytvořený jiným programem lze odpojit přes Windows
bez změny jeho uloženého profilu. Nové připojení běží na pozadí.
Vyžaduje nainstalovaný WinFsp a SSHFS-Win; detekuje také kompletní runtime
v plugins/sshfs/bin. Pokud podpora chybí, při ručním připojení se nabídne
okno „Podpora síťového disku SSHFS“. Je také dostupné tlačítkem Podpora SSHFS
ve správě připojení a v Upřesnit profilu SSHFS. Tlačítko Stáhnout a nainstalovat
stáhne pouze chybějící komponenty z oficiálního GitHubu. Stahování používá
stávající libcurl, ukazuje průběh a kontroluje SHA-256 i podpis Authenticode.
Oddělený instalační proces může vyžádat potvrzení správce ve Windows;
mapování disku potom probíhá pod původním běžným účtem, aby disk viděl
také Antigravity. Okno instalace neblokuje hlavní okno Commanderu.
MSI poskytuje skutečný průběh a zrušení s vrácením rozpracovaných změn.
Hotová první komponenta se při zrušení druhé neodinstaluje. Restart se
nespouští automaticky; vyžaduje-li ho instalátor, další krok počká na restart.
Při startu programu se instalace sama nenabízí ani nespouští.
Soukromý klíč musí být bez heslové fráze; nepoužívá se agent pro mapování
disku. Pro SSHFS použijte IPv4 nebo název serveru, nikoli IPv6 literál.
