# Síťové pluginy

Program se spouští z bin/CommanderCZ.exe. Jediný síťový plugin je
bin/plugins/network/CommanderNetwork.dll. Obsahuje staticky libcurl 8.22.0,
libssh2 1.11.1 a jejich závislosti včetně LibreSSL 4.3.3.
FTP a FTPS používají libcurl; SFTP a SCP používají jednu libssh2 relaci pro
výpis, atributy i přenos. Na počítači není nutné nic instalovat.
Archivy mají samostatný plugin plugins/7zip.

SMB, NFS a mapování disků nadále obsluhuje Windows. SSH terminál používá
Windows OpenSSH spuštěný v PowerShellu. Tyto funkce nejsou součástí libcurl.
Profily a šifrovaná hesla zůstávají v %APPDATA%/CommanderCZ.

Otisk SSH serveru se kontroluje před přihlášením v každé nezávislé relaci.
Podporované jsou i dřívější SHA-256 otisky s koncovým = a uložený algoritmus
klíče se zachová, nabízí-li server více klíčů. Ověřeno heslo, interaktivní
heslo, RSA PEM a Ed25519 OpenSSH klíč, SSH komprese a všechny tři proxy.
SSH agent a udržovací interval se používají v terminálu i souborových relacích SFTP/SCP.

FTPS ověřuje certifikát a název serveru s kořeny z Windows. Klientský
certifikát lze načíst jako PFX/P12, PEM s privátním klíčem nebo DER.
PFX/P12 je nutné mít bez hesla, stejně jako dosud. Certifikáty se načítají
přes .NET, proto fungují i cesty s českými znaky. Automatické vypnutí
ověřování certifikátů není implementováno.

FTP umí pasivní i aktivní režim, MLSD a MLST, Unixový LIST a Windows DOS LIST,
české názvy v UTF-8 i Windows kódování, skryté soubory a příkazy po přihlášení.
Při nepodporovaném automatickém MLSD se použije LIST; explicitní MLSD hlásí chybu.
MLST načítá atributy jednotlivého souboru, aby se nemusel opakovat celý výpis.

SCP přenáší skutečným SCP protokolem, nikdy nepřechází na SFTP. Výpis a operace
zajišťuje SSH shell s GNU find nebo tradičním Unix ls; server potřebuje scp,
mv, mkdir, rm a touch. Všechny cesty jsou doslovné a shellově citované.

Každá fronta má vlastní relaci, procházení i plánování další vlastní relace.
Přenosy zachovávají zápis do dočasných souborů a nahrazují cíl až po dokončení
a kontrole velikosti. Při nahrávání zůstává původní soubor dostupný pro obnovu.
Zrušení probudí libcurl nebo uzavře socket libssh2; UI nečeká na síťový úklid.
Při nedostupnosti serveru mohou zůstat obnovitelné soubory .CommanderCZ-upload-*
nebo .CommanderCZ-old-*. Oznámení průběhu zůstávají omezená na deset za sekundu.

Sestavení programu: source/build.ps1. Hotový plugin se automaticky přibalí.
Přestavení nativního pluginu: source/native/build.ps1 s LLVM-MinGW x64,
přiloženými statickými knihovnami a network.def. DLL se načítá absolutní cestou
pouze z plugins/network; nevyhledávají se síťové knihovny v PATH.
Původ, hash a licence uvádí manifest.json a licenses.

Ověření migrace je popsáno v NETWORK-VALIDATION.md. FTPS datové loopback testy
na tomto počítači blokuje Avast nahrazováním certifikátu. Explicitní FTPS
řídicí připojení, kontrola jména/důvěry a PFX klientský certifikát byly ověřeny;
plný FTPS datový přenos a implicitní režim vyžadují test bez této interference.
# SSHFS disky Windows

SSHFS se spravuje jako uložený síťový profil. Používá externí SSHFS-Win
a instalovaný WinFsp, případně kompletní runtime v plugins/sshfs/bin.
Ve standardní distribuci plugins/sshfs obsahuje pouze dokumentaci;
ovladač ani systémové instalátory se při spuštění aplikace automaticky nespouštějí.
Ruční připojení při chybějící podpoře nabídne vlastní okno instalace.
Tlačítko Podpora SSHFS je dostupné také ve správě a v podrobnostech profilu.
Stahují se připnuté oficiální verze WinFsp 2.1.25156 a SSHFS-Win 3.5.20357 x64.
Kontroluje se SHA-256 a Authenticode, i znovu před instalací v odděleném
procesu s oprávněním správce. Balíčky se před instalací kopírují
do soukromé složky správce; neprovádí se libovolný příkaz ani uživatelský MSI.
Průběh a zrušení předává Windows Installer přes pojmenovanou rouru.
Instalace nepřeinstaluje existující komponenty, sama nerestartuje počítač
a při zrušení nechá Windows Installer dokončit rollback. Mapování SSHFS
zůstává v původním uživatelském procesu. Záznam instalace je v
%LOCALAPPDATA%/CommanderCZ/sshfs-setup/<id>/installation.log.
Commander používá vlastní profil a připnutý veřejný klíč serveru.
SSHFS-Win nedostává heslo v příkazové řádce ani v logu.

