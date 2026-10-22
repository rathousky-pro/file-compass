# SSHFS

CommanderCZ spravuje profily SSHFS a používá SSHFS-Win s ovladačem WinFsp.
V této složce nejsou instalátory ani další kopie systémového ovladače.
Při chybějící podpoře Commander nabídne jejich stažení a instalaci tlačítkem
Stáhnout a nainstalovat v okně Podpora SSHFS. Instalace se při startu
programu sama nespouští. Používá oficiální WinFsp 2.1.25156 a SSHFS-Win
3.5.20357 x64, připnuté SHA-256 a ověření podpisu Authenticode.
Vyžádá si případné potvrzení správce, ukazuje průběh, podporuje zrušení
s rollbackem a neprovádí automatický restart. Již instalované komponenty
nepřeinstaluje. Disk potom připojí pod běžným účtem Windows pro použití
v Commanderu, Antigravity a dalších aplikacích.
Program nejprve hledá kompletní runtime v plugins/sshfs/bin/sshfs.exe,
potom nainstalované SSHFS-Win v Program Files (také x86).
Samotné kopírování DLL do pluginu nenahradí instalaci ovladače WinFsp.

https://github.com/winfsp/sshfs-win
https://github.com/winfsp/winfsp

Přihlašovací údaje se předají přímo vstupní rourou. Ve známých klíčích je
pouze veřejný klíč serveru; při připojení se vyžaduje StrictHostKeyChecking.
Místní profily nezasahují do uživatelova .ssh/config ani known_hosts.
