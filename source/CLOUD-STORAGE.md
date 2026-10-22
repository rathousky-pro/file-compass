# Cloudová úložiště: Google Drive, OneDrive, Dropbox

Vlastní desktopová aplikace OAuth File Compass používá konfiguraci vydavatele `config/plugins/cloud/google-oauth.json`. Každý uživatel se přihlásí vlastním účtem a nevyplňuje Client ID ani Client Secret. Bez konfigurace vydavatele zůstává nové přihlášení Google nedostupné s vysvětlením. Starší účty si zachovávají původní identitu do nového přihlášení. Viz GOOGLE-OAUTH-PUBLISHER.md pro registraci a veřejné ověření aplikace.

## Přidání účtu
1. Otevřete **Připojení → Správa připojení… → Cloudová úložiště**. Stejné okno otevře tlačítko Připojení v horní liště; přepněte v něm na záložku Cloudová úložiště.
2. Vyberte **Přidat účet…**, službu a vlastní název účtu.
3. Klikněte na **Přihlásit v prohlížeči**.
4. V prohlížeči vyberte účet, případně potvrďte MFA, a odsouhlaste požadovaný přístup.
   V okně poskytovatele může být aplikace označena jako **rclone**.
5. U OneDrivu může následovat výběr disku nebo knihovny. Firemní správce může vyžadovat schválení aplikace.
6. Po úspěšném přihlášení vyberte **Připojit**.

Lze uložit více účtů jedné služby. Heslo Google/Microsoft/Dropbox se ve File Compass nezadává.
Přihlašovací souhlas probíhá u poskytovatele ve výchozím prohlížeči. Po jeho schválení se autorizace automaticky vrátí do programu.
Při zrušení či vypršení přihlášení použijte **Znovu přihlásit…**.

### Profil přenesený z jiného počítače

Vyberte původní účet a **Znovu přihlásit…**. Nové přihlášení nepoužívá šifrovací klíč původního počítače. Po úspěšném přihlášení a ověření přístupu zachová název i identitu účtu a uloží nové místní přihlašovací údaje. Ostatní účty ani jejich šifrovací klíče se nemění. Zrušení původní profil nepřepíše.

Předchozí místní konfigurace zůstává v chráněné záložní složce účtu s příponou `.before-signin-…`. Příkaz Odebrat účet odstraní také tyto zálohy daného účtu. Přihlášení a případné MFA je třeba dokončit ve vlastním prohlížeči; program jej nemůže potvrdit za uživatele.

## Soubory
Cloud se chová jako vzdálený panel: procházení, F5 kopírování, F6 přesun, vytváření složek, přejmenování a mazání.
Používá stávající frontu a průběh přenosů. Přenosy mohou běžet současně s procházením.
Cloudová záložka a pracovní sada si pamatují identitu účtu; automatické přihlášení při startu se nevynucuje.
Přerušené nahrávání do cloudu se opakuje od začátku souboru, již dokončené cíle lze přeskočit.
Úpravy a náhledy vzdálených souborů používají dosavadní dočasné stažení a limity.
Cloudové panely neposkytují SSH terminál. Přímé hledání nových metadat uvnitř cloudu zatím není podporované.
Tato verze nepřipojuje cloud pod písmeno disku do Průzkumníka; nevyžaduje WinFsp ani administrátorskou instalaci.

## Odpojení a odebrání
Panel odpojíte přes Připojení → Aktivní připojení → Odpojit aktivní panel nebo místní nabídku tlačítka připojení.
Uložené přihlášení zůstává pro další použití.
**Odebrat účet** odstraní místní konfiguraci a tokeny, nikoli soubory v úložišti.
Účet s otevřeným panelem nebo nedokončenými přenosy je nejprve potřeba odpojit a přenosy dokončit/odebrat.
Pro úplné odvolání souhlasu lze přístup aplikace zrušit také v nastavení Google/Microsoft/Dropbox.

## Uložení a ochrana
Účty a zašifrovaná konfigurace jsou v **config/cloud**.
Každý účet má vlastní šifrovaný soubor rclone.conf. Klíč chrání Windows DPAPI pro aktuálního uživatele.
Přenesení programu jinému uživateli nebo na jiný počítač nepřenáší funkční přihlašovací oprávnění.
Cloudová složka má omezená oprávnění přístupu. Tokeny se nepředávají v příkazové řádce ani nevypisují do uživatelských protokolů.
Interní přenos používá SFTP pouze přes 127.0.0.1, s vlastním heslem a připnutým klíčem pro každý účet.
Toto interní připojení není přístupné z místní sítě a není veřejným serverem.
Pomocné procesy běží skrytě a ukončují se s programem.

## Ověření
Ověřen je skutečný přenos přes rclone na místním testovacím úložišti a zahájení OAuth pro všechny tři služby bez zadávání uživatelských přihlašovacích údajů.
Dokončení přihlášení a konkrétní oprávnění osobního či firemního účtu vyžadují jeho vlastníka.
Nativní dokumenty Google mohou být při stažení exportovány do běžného formátu podle možností rclone; dostupnost operací závisí na službě a oprávněních účtu.
