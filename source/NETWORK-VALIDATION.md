# Ověření migrace libcurl + libssh2 (13. 10. 2026)

Výsledné sestavení: verification/curl-migration/deploy-bin.
Síťová knihovna: source/plugins/network/CommanderNetwork.dll, x64.
Importuje pouze systémové Windows DLL/UCRT; libcurl, libssh2, kryptografie a
ostatní závislosti jsou staticky vložené. Původ a SHA-256 jsou v manifest.json.

## Provedené kontroly

- FTP: 34 kontrol skutečného lokálního připojení, aktivního/pasivního režimu,
  češtiny, metadat, přenosů, rekurze, kolizí, úklidu a síťových ikon.
- SFTP: 42 kontrol včetně serverového klíče, souběžných FTP/SFTP front,
  průběhu, rozšiřování fronty, pevného cíle a použitelnosti UI během přenosu.
- SCP: 19 kontrol proti serveru bez SFTP; skutečný přenos, doslovné cesty
  s apostrofy/brackets/dolarem, operace složek, přepis a zrušení.
- Profily/nastavení: 40 kontrol XML, hesel, voleb protokolů a fontů.
- Rozšířené backend testy: 56 kontrol. Unix LIST fallback, vynucené MLSD,
  Windows kódování i jeho automatické rozpoznání, RSA PEM/Ed25519 OpenSSH
  klíče, interaktivní heslo, SSH komprese a více nabízených klíčů serveru;
  FTP i SFTP přes SOCKS4, SOCKS5 s heslem a HTTP CONNECT s přihlášením.
  Zahrnuto explicitní FTPS řídicí spojení a příkazy, odmítnutí nedůvěryhodného
  certifikátu/chybného názvu serveru a povinný PFX klientský certifikát.
- Skutečné spuštění WPF okna a asynchronní aktualizace ikon disků: PASS.

Výsledky jsou v verification/curl-migration/final-ftp, final-sftp, final-scp,
network-test, verified-features a startup-test. Servery běží pouze na loopback;
testovací data, klíče a profily jsou ve vyhrazených pracovních složkách.
Uživatelská konfigurace v AppData se při těchto testech nepřepisuje.

## Zrušení při nereagujícím FTP serveru

Adversariální server zdržuje výpis 4 sekundy a QUIT 3 sekundy.
CancellationToken callback vrací za 0 ms. Čekající výpis skončil za 3 ms,
stahování za 4 ms, nahrávání včetně úklidu za 1048 ms.
Původní lokální i vzdálený soubor zůstaly zachované, staging se odstranil,
procházení na vlastní relaci fungovalo. Měřeno v lokálním testu;
skutečný čas úklidu závisí na dostupnosti serveru.

## Omezení tohoto ověření

Plný FTPS datový přenos a pozitivní implicitní FTPS test nejsou na tomto
počítači potvrzené. Avast při TLS datovém spojení nahrazuje serverový certifikát
certifikátem vydaným „Avast Web/Mail Shield Self-signed Root“, který není
v důvěryhodném řetězci. Stejné selhání nastalo s oficiálním curl.exe a
předloženým testovacím CA. Diagnostika je v verification/curl-migration/cli-ftps2.
Program odmítá tento certifikát; žádné automatické vypnutí kontroly nebylo přidáno.
Tyto datové testy jsou označené SKIP, nikoli PASS. PFX a řídicí explicitní
TLS spojení, správné odmítnutí důvěry/jména jsou úspěšně ověřené.

Pomocný --ssh-test pro zaznamenání argv v PowerShellu nevytvořil arguments.txt.
Stejný výsledek byl ověřen i s předchozí nasazenou verzí (baseline-ssh).
Příčina tohoto místního testovacího selhání nebyla určena; implementace
SshTerminal.cs se v této migraci nemění. Přenosové SSH ověření heslem,
interaktivním heslem, RSA a Ed25519 klíčem prošlo skutečnými serverovými testy.

Testy neprokazují kompatibilitu se všemi existujícími FTP/SSH servery.
SCP shell vyžaduje Unixové scp/find/ls/mkdir/mv/rm/touch stejně jako dosud.
Podporované FTP parsery jsou MLSD/MLST, Unix LIST a DOS LIST; exotické
z/OS/VMS/Tandem výpisy nejsou implementované.

Etapa 1–6: na izolovaném serveru ověřen skutečný podpis vygenerovaným RSA
klíčem z vlastní pojmenované roury SSH agenta (5 kontrol), příjem udržovacích
paketů po dobu nečinnosti (5) a zachované RSA/Ed25519 přihlášení (4 + 4).
Původní uživatelská služba agenta, klíče a konfigurace se nemění.
Výsledky jsou v verification/phase1-6/ssh-features. Dokumentace API:
https://libssh2.org/libssh2_agent_userauth.html
https://libssh2.org/libssh2_keepalive_config.html
https://libssh2.org/libssh2_keepalive_send.html

Navazování, 13. 10. 2026: skutečné FTP a SFTP scénáře po 30 kontrolách,
včetně nového procesu, opakovaného přerušení, integrity rozpracované kopie,
změněného zdroje/cíle a obousměrných dávek s podsložkami. FTP bez REST/APPE:
8 kontrol bezpečného odmítnutí. Zachované přenosy FTP 38, SFTP 46, SCP 19;
UI 89, společné operace 44, archivy 71. Výsledky: verification/resume.
FTPS resume používá stejnou FTP větev, ale skutečný FTPS datový test nebyl
dokončen: místní Avast nahrazuje certifikát datového spojení vlastním.
Diagnostika ftps-certificate-probe.py to ověřila; produkční kontrola TLS
zůstává zapnutá. SCP nemá offset; volba Navázat restartuje jen nehotový soubor.

Rychlé navázání, 13. 10. 2026: kontrola celého prefixu nahrazena velikostí,
časem a krajními bloky. FTP a SFTP po 36 kontrolách; FTP bez REST/APPE 10;
skutečný SSHFS disk 16 (včetně přesunu bez automatického úplného SHA-256).
Podrobnosti a omezení vzorkovaného ověření: ../reports/Rychle-navazovani.md.
Výsledky: verification/fast-resume. Starší výsledky výše zachycují předchozí
implementaci s úplným čtením rozpracované části.

14. 10. 2026 – návrat do FTP panelu při kopírování: verification/ftp-panel-recovery.
FTP: 43 kontrol, včetně běžícího 8 MiB uploadu, odchodu na místní disk,
skutečného odmítnutí CWD starou relací, automatické obnovy panelu a dokončení
uploadu. Odstraněná poslední složka nabídne kořen s cestou a odpovědí 550;
odmítnutí zachová místní panel. Obnovení zachovává ikonu i reference záložek.
FTP navazování: 36 kontrol; SFTP: 46 kontrol zachovaných operací.
Testy používají vlastní místní servery, neprokazují příčinu dřívější chyby
na uživatelově serveru. Při skutečném odmítnutí oprávnění či limitu serveru
obnovení nemusí uspět; chyba poskytne podrobnosti.

14. 10. 2026 – meziserverové F5: verification/server-relay. Nová sada 27
kontrol mezi dvěma vlastními FTP, SFTP a SCP. Přepis, přeskočení, skutečné
přerušení stahování/nahrávání, ochrana změněného cíle, rozšíření živé fronty
a průběh pro více cílů. Zachované FTP 43 a SFTP 46; UI 146, profily 45,
společné operace 53. Hranice a FTPS: ../reports/Meziserverove-kopirovani.md.

14. 10. 2026 – průběžný přenos mezi servery: verification/streaming-relay.
42 kontrol: souběh download/upload bez místního souboru, buffer do 512 KiB,
bajtově shodný 8 MiB soubor, prázdné soubory, probuzení čekajících vláken
při zrušení a chybě, ochrana původního cíle až do potvrzení zdroje,
obnovení přenosových relací při opakování a přeskočení chyby.
FTP/SSH používají samostatné pracovní relace. FTPS relay nebyl přímo
otestován; omezení a podrobnosti: ../reports/Meziserverove-kopirovani.md.

14. 10. 2026 – navázání mezi servery: verification/relay-resume/deploy-checked.
65 kontrol přenosu a navazování FTP ↔ FTP, FTP ↔ SFTP a restartu SCP.
Ponechání/odstranění vlastní části, nové relace, více souborů, ochrana
změněného zdroje/cíle, poškozené části, odmítnutí APPE, zámek společného
cíle a nejvýše 128 KiB kontrolního čtení na každém serveru. Skutečné volby
v UI, návrat z dialogu bez zastavení, přerušená historie a nové F5.
Zachované FTP 43, SFTP 46, UI 146, profily 45, společné operace 53 a spuštění.
Podrobnosti a hranice: ../reports/Navazovani-mezi-servery.md.

15. 10. 2026 – síťové F6, vzdálené F4, řízení fronty a automatická obnova.
verification/network-1-4/verified-checked: 87 kontrol relay a nových funkcí;
F6 lokál ↔ FTP, FTP ↔ FTP, přejmenování na stejném serveru, složky na SFTP,
přeskočení a přerušení bez ztráty zdroje. F4 uloží změny a odmítne cizí
obsah se stejnou velikostí a časem. Skutečný výpadek FTP po 2 MiB se obnoví
a naváže bez dialogu. Pauza, rychlost, pořadí, jednotlivé rušení a skutečné
WPF okno → minimalizace → spodní lišta → obnovení pro lokální i síťové kopie.
Regrese: FTP 43, SFTP 46, UI 147, místní kopírování 31, profily 45,
společné operace 53 a spuštění. Uživatelské servery ani mapované disky
se kvůli testům nemění. Hranice: ../reports/Sitove-F6-F4-fronta-obnova.md.

15. 10. 2026 – oprava obnovení kopírovacích oken.
verification/transfer-window-restore/final-native: 40 kontrol skutečných HWND,
nikoli jen vlastností WPF. Opakovaná minimalizace přes záhlaví i programově,
obnovení rozměrů a polohy, zrušení v popředí i na pozadí a zrušení během
čekající minimalizace. Hlavní okno zůstává v normálním nebo maximalizovaném
stavu. Minimalizace nyní přesouvá přenos do fronty skrytím okna; nevytváří
malé minimalizované okno Windows vlevo dole. Regrese UI 147, místní kopie 31
a skutečné spuštění prošly. Síťový relay: 87 kontrol na vlastních místních
FTP/SFTP/SCP serverech, verification/transfer-window-restore/relay-final.
Podrobnosti: ../reports/Oprava-obnoveni-kopirovani.md.
