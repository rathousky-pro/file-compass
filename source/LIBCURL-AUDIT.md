# Posouzení migrace CommanderCZ na libcurl

Ověřeno 13. 10. 2026 proti současným zdrojům CommanderCZ a oficiálnímu
Windows balíčku curl 8.22.0_3 x64. Původní posouzení před implementací je zachováno níže.
Migrace nyní používá libcurl a libssh2 v plugins/network/CommanderNetwork.dll;
FTP/FTPS obsluhuje libcurl, SFTP/SCP a jejich SSH operace libssh2 v jedné relaci.
FluentFTP a SSH.NET se již nepoužívají. Ověření a místní omezení FTPS jsou
popsány v NETWORK-VALIDATION.md.

## Ověřený balíček

- Zdroj: https://curl.se/windows/dl-8.22.0_3/curl-8.22.0_3-win64-mingw.zip
- SHA-256: f38b0ac2e3a280fd414b607c874a21e6dd4d1ed0e2ae622afbf0020917446561
- Nativní DLL: libcurl-x64.dll. Úspěšně načtena v 64bitovém procesu.
- Závislosti jsou staticky vložené: mimo jiné libssh2 1.11.1 a LibreSSL 4.3.3.
- Protokoly skutečného balíčku obsahují FTP, FTPS, SFTP a SCP; neobsahují SMB ani NFS.
- Ověřené TLS rozhraní balíčku: openssl (implementace LibreSSL); Schannel není dostupný.
- Exportuje curl_easy_init a curl_multi_wakeup.
- Neexportuje libssh2_session_init_ex ani libssh2_channel_process_startup.
  Přítomnost libssh2 uvnitř DLL tedy nezpřístupňuje obecné SSH příkazy aplikaci.
- Balíček obsahuje také statické knihovny libcurl.a, libssh2.a a jejich závislostí.
  Jsou použitelným základem pro sestavení jedné vlastní DLL s oběma rozhraními.

## Srovnání se skutečně používanými funkcemi

| Funkce CommanderCZ | Veřejné rozhraní libcurl | Co je potřeba při migraci |
|---|---|---|
| FTP a FTPS, explicitní i implicitní TLS | Ano | Nastavit správné URL a požadavek TLS; zachovat kontrolu certifikátů. |
| Pasivní i aktivní FTP | Ano | Mapovat profil na EPSV/PASV nebo FTPPORT/EPRT. |
| FTP anonymní přihlášení, uživatel a heslo | Ano | Zachovat profily a DPAPI; hesla předávat v paměti. |
| FTP příkazy po přihlášení | Ano, QUOTE/PREQUOTE | Neposílat je zbytečně při každém souboru; správně rozlišit opětovné připojení. |
| FTP MLSD, LIST a skryté soubory | Ano, CUSTOMREQUEST | Doplnit vlastní parser MLSD a běžných serverových výpisů, který dnes poskytuje FluentFTP. |
| Názvy souborů a přepínání kódování | Přenos bajtů ano | V aplikaci zachovat výběr UTF-8 / Windows kódování a správné escapování URL. |
| SFTP přenos a výpis složky | Ano | Libcurl vrací textový výpis, nikoli objekty jako SSH.NET. |
| Přesné SFTP atributy a LSTAT bez následování odkazu | Nemá ekvivalent objektového API SSH.NET | Vlastní DLL má zpřístupnit libssh2_sftp_readdir_ex a libssh2_sftp_stat_ex; důležité pro typy položek, čas a bezpečné odmítání odkazů. |
| SFTP mkdir, rename, rm, rmdir a mtime | Ano, QUOTE | Zachovat doslovné názvy, rekurzivní mazání a ochranu kořenové složky. |
| SCP přenos jednotlivého známého souboru | Ano | Ověřit Unicode a shellové znaky v cestách proti současným testům. |
| SCP panel, výpis a atributy | Ne | Dnes používá SSH exec s find / ls; doplnit přes libssh2. |
| SCP mkdir, přejmenování a mazání | Ne | QUOTE není obecný SSH exec a není určen pro SCP; doplnit přes libssh2. |
| Bezpečné přepsání souboru přes SCP | Samotný přenos nestačí | Zachovat kontrolu velikosti, dočasné soubory, rename, obnovu původního souboru a úklid přes SSH. |
| Ověření uloženého otisku SSH klíče a první připojení | Ano | SHA-256 pinning / hostkey callback; callback je podporován s libssh2 backendem. Stejné ověření i ve vlastním SSH rozhraní. |
| SSH heslo, soukromý klíč, keyboard-interactive | Ano | Zachovat pořadí a bezpečné ověřování; konkrétní formáty klíčů otestovat. |
| SSH komprese | Ano | CURLOPT_SSH_COMPRESSION; požadavek může server odmítnout. |
| SOCKS4, SOCKS5, HTTP CONNECT a přihlašování proxy | Ano | Nastavit tunel pro FTP i SSH; vlastní SSH relace musí stejné proxy skutečně respektovat. |
| FTPS klientský certifikát | Ano, závisí na TLS backendu a formátu | Ověřit současné certifikáty a důvěru Windows; balíček používá LibreSSL, ne dnešní .NET/Windows TLS. |
| Průběh, rušení a souběžné fronty | Ano, callbacks a multi API | Použít multi/wakeup a samostatné relace; žádné cleanup nebo síťové čekání na UI vlákně. |
| Součty dat, přidávání do fronty, přeskočit/přepsat, obnova při chybě | Jsou logikou CommanderCZ | Zachovat existující koordinátor a transakční zápis; knihovna je sama nezajišťuje. |
| SMB/UNC a mapování disku | Nenahradí současné Windows funkce | Zachovat Windows API. Obecná SMB implementace curl podporuje pouze SMB1 a tento balíček ji nemá zapnutou. |
| NFS a připojení jako disk | Ne | Zachovat klienta NFS a mount/umount ve Windows. |
| SSH terminál v PowerShellu | Ne, libcurl není terminál | Zachovat Windows OpenSSH v PowerShellu / Windows Terminalu. |
| Archivy a 7-Zip | Nejde o síťový přenos | Plugin 7-Zip a nastavení WinRAR zůstávají samostatně. |

## Doporučená varianta bez ztráty funkcí

Jedna distribučně přibalená DLL, například plugins/network/CommanderNetwork.dll,
obsahující staticky libcurl, libssh2 a jejich potřebné závislosti. Libcurl zajistí
FTP/FTPS a datové přenosy SFTP/SCP. Malé vlastní rozhraní nad stejným libssh2
zpřístupní SSH exec pro SCP a strukturované SFTP atributy. Aplikace bude mít
jediný síťový plugin bez FluentFTP, SSH.NET a jejich .NET závislostí.

Je to jedna výsledná DLL, nikoli čisté veřejné API libcurl. Libcurl potřebuje
SSH backend již dnes. SMB/NFS budou nadále využívat systémové služby Windows;
nevyžadují další síťové DLL v distribuci CommanderCZ.

Konečná kompatibilita vlastního sestavení musí projít testy přenosů, odkazů,
ověření klíčů, certifikátů a proxy. Samotné načtení DLL a seznam protokolů není
důkazem úplné funkční shody. Před nasazením zopakovat stávající FTP/SFTP/SCP
testy, test blokujícího serveru a zachování původního souboru po zrušení.

## Primární dokumentace

- https://curl.se/libcurl/c/CURLOPT_QUOTE.html
- https://curl.se/docs/url-syntax.html
- https://curl.se/libcurl/c/CURLOPT_SSH_AUTH_TYPES.html
- https://curl.se/libcurl/c/CURLOPT_SSH_HOSTKEYFUNCTION.html
- https://curl.se/libcurl/c/CURLOPT_SSH_COMPRESSION.html
- https://curl.se/libcurl/c/CURLOPT_SSLCERT.html
- https://curl.se/libcurl/c/CURLOPT_SSLCERTTYPE.html
- https://curl.se/libcurl/c/CURLOPT_PROXYTYPE.html
- https://curl.se/libcurl/c/CURLOPT_XFERINFOFUNCTION.html
- https://curl.se/libcurl/c/libcurl-multi.html
- https://curl.se/libcurl/c/curl_multi_wakeup.html
- https://curl.se/docs/CVE-2026-5773.html (dokumentuje omezení na SMB1)
- https://libssh2.org/libssh2_channel_exec.html
- https://libssh2.org/libssh2_sftp_stat_ex.html
- https://libssh2.org/license.html
