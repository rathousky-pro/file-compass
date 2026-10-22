# Volitelné archivátory

V Nastavení → Archivy lze vlastní instalaci vyhledat, vybrat její EXE nebo použít tlačítko Získat doplněk / Získat WinRAR / RAR. Stahování začíná až tlačítkem v samostatném okně. Jestliže je nástroj dostupný v nastavené cestě, běžné instalaci nebo PATH, použije se bez stahování.

ARJ32 a LHa se připravují pod `%LOCALAPPDATA%\CommanderCZ\plugins\archivers`, bez změny Windows či instalace do Program Files. Okno je modeless: nastavení lze zavřít a pokračovat v práci. Stažení lze zrušit. Po dokončení se cesta automaticky uloží; ručně nastavené cesty zůstávají podporované.

WinRAR/RAR používá původní instalátor RARLAB. Po stažení je nutné stisknout Spustit instalátor a dokončit jeho běžné instalační kroky. Aplikace nespouští instalaci automaticky, neprovádí tichou instalaci ani redistribuci samostatného Rar.exe. Následně vyhledá instalovaný program. Pokud instalace ještě pokračuje, lze použít Vyhledat WinRAR v nastavení.

## Zdroje a licence

- WinRAR / RAR 7.23 x64: https://www.rarlab.com/download.htm ; EULA https://www.rarlab.com/license.htm . Jde o zkušební software, nejvýše 40 dní bez nákupu licence. Stažení licenci nezahrnuje.
- ARJ32 3.31: https://arj32.com/ . Shareware; platí přiložené podmínky ARJ Software. Originální ZIP a vnořený SFX se pouze rozbalí, SFX ani jeho instalační/testovací příkazy se nespouštějí. Licenční a další dokumentace se zachovává.
- LHa 1.14i, GnuWin32: https://gnuwin32.sourceforge.net/packages/lha.htm . Zachovává se obsah původního distribučního balíčku včetně dokumentace. Jde o kompresní LHa, nikoli rozbalovací nástroj Lhasa.

K vytváření RAR, ARJ a LHA slouží samostatné externí procesy. Hesla se jim nepředávají v argumentech. Získání doplňků nemění dřívější omezení ARJ/LHA při práci s českými názvy.

## Ověření a údržba

Stahování používá HTTPS s ověřováním certifikátů, nejvýše 128 MiB a omezený čas. Konkrétní verze, adresy a SHA-256 balíčků i výsledných EXE jsou pevně uvedeny v ArchivePluginInstaller.cs. ARJ se kontroluje také před otevřením vnořeného SFX. WinRARův instalátor prochází navíc kontrolou Authenticode před publikováním i před spuštěním. Změněný obsah se odmítne; kontrola se nevypíná ani při změně souboru na serveru.

ARJ/LHA se rozbalují pomocí přibaleného ověřeného kodeku v odděleném procesu a s běžnými archivními limity/kontrolami cest. Publikují se přesunem celé připravené složky. Nedokončené a chybné stažení neaktivuje program. Spravovaný archivátor se před každým spuštěním znovu ověří a jeho soubor zůstane uzamčený během použití. Vlastní instalace mimo úložiště doplňků se řídí cestou zvolenou uživatelem.

Neprobíhá automatické hledání „nejnovějšího EXE“ na webu ani automatická aktualizace. Nové ověřené vydání vyžaduje aktualizaci záznamu verze, URL a hashů, ověření licence/balíčku a integrační test. To brání tomu, aby změna vzdáleného souboru způsobila spuštění neověřené verze. Novější vlastní instalaci lze vybrat ručně.

Ověření implementace: `--archive-plugin-test <testovací složka>` stahuje skutečné balíčky do testovacího úložiště, kontroluje opakované použití, integritu, zámky, rušení, odstranění stagingu, přípravu ARJ/LHA, vytvoření archivů i uložení cesty. WinRARův instalátor tento test nikdy nespouští.
