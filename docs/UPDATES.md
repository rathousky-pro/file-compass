# Aktualizace programu a doplňků

V **Konfigurace → Aktualizace** najdete instalovanou a dostupnou verzi, výsledek poslední kontroly, zdroj a cestu k používanému souboru. Kontrolu lze spustit ručně nebo povolit jednou denně. Denní kontrola sama žádné balíčky nestahuje. Změna jejího nastavení se ukládá tlačítkem **Použít** nebo **Uložit**; kontrola, stažení a použití balíčku jsou samostatné okamžité akce.

## Zdroje

| Součást | Zdroj a podpora |
|---|---|
| 7-Zip | [Oficiální repozitář ip7z/7zip](https://github.com/ip7z/7zip/releases), Windows x64 balíček; `7z.exe` a `7z.dll` společně. |
| FFmpeg + FFprobe | [Plné statické Windows sestavení gyan.dev](https://www.gyan.dev/ffmpeg/builds/), na které odkazuje [ffmpeg.org](https://ffmpeg.org/download.html); oba nástroje společně. |
| rclone | [Oficiální repozitář rclone/rclone](https://github.com/rclone/rclone/releases), Windows AMD64 balíček. |
| smartmontools | [Oficiální vydání](https://github.com/smartmontools/smartmontools/releases). Verze se kontroluje, ale automatická instalace vyžaduje zveřejněný SHA-256. Aktuální starší vydání na GitHubu má místo něj PGP/MD5 soubory; dokud aplikace neověřuje PGP, nabídne stránku výrobce. |
| Magick.NET / OpenMP | Přehled ukazuje instalovanou verzi. Spravovaná knihovna, nativní OpenMP knihovna a odpovídající runtime se aktualizují společně s File Compass. Samostatná aktualizace FFmpegu je nenahrazuje. |
| Knihovny a ovladače | Síťové/náhledové knihovny zůstávají součástí vydání File Compass. SSHFS/WinFsp nadále používají instalační postupy výrobce. |
| File Compass | [GitHub Releases rathousky-pro/file-compass-web](https://github.com/rathousky-pro/file-compass-web/releases), Windows x64 ZIP; porovnává se verze, kanál i build. |

Zjištění verze a ověření stahovaného balíčku jsou samostatné kroky. Pokud metadata výrobce potvrzují stejnou nebo starší verzi, stav je **Aktuální** i tehdy, když vydavatel neposkytuje SHA-256. U novější verze bez SHA-256 stav nabízí ruční stažení; automatické stažení a instalace zůstávají vypnuté. Podrobnost u řádku vysvětluje chybějící kontrolní součet. Chyba síťové kontroly se nadále zobrazuje jako chyba.

Doplňky používají stabilní vydání. File Compass v beta kanálu připouští beta i stabilní vydání; stabilní instalace nepřebírají předběžná vydání. Neexistuje vlastní přebalovací server ani odhadované číslo nejnovější verze. Výpadek sítě nebo chybějící ověření se zobrazí jako chyba, nikoli jako „Aktuální“. GitHub může omezit počet anonymních požadavků; ruční kontrolu lze zopakovat později. FFmpeg se kontroluje proti verzi a SHA-256 zveřejněným přímo na stránkách poskytovatele Windows sestavení.

## Stažení a použití

1. Vyberte součást a stiskněte **Stáhnout aktualizaci**. Otevře se známé okno průběhu s možností zrušení nebo odložení do spodního přehledu úloh.
2. Balíček se stáhne přes HTTPS z povolených zdrojů, porovná se SHA-256 a rozbalí v odděleném pracovním adresáři. Instalační EXE 7-Zipu se nespouští; aplikace z něj pouze vybere požadované soubory.
3. Kontroluje se Windows x64 formát souborů, jejich přesný soupis a funkční spuštění s očekávanou verzí. Do aktivní složky se nepřenášejí libovolné další DLL z archivu.
4. Nová verze zůstává v datové části profilu, například `data/updates/packages/7zip/<sha256>/7zip/`. Původní dodané soubory se nepřepisují. Tím funguje i instalace bez práva měnit programové soubory, pokud je zapisovatelná datová část profilu.
5. Je-li součást volná a v programu neběží práce, připravená verze se atomicky aktivuje. Pokud už byla DLL 7-Zipu načtena do procesu, změna počká na příští spuštění. rclone se přepíná při příštím spuštění kvůli jeho dlouho žijícím cloudovým procesům. Rozpracované převody, archivace a přenosy se kvůli aktualizaci neruší.

**Použít připravené** zkusí aktivovat čekající balíčky, pro které jsou splněné podmínky. **Vrátit předchozí** vrátí předchozí verzi, případně původní dodanou součást; u načtené DLL a rclone se návrat připraví pro další spuštění. Nastavení, taby, připojení a uživatelské soubory tím nejsou nahrazovány.

## Ověřování a obnova

Dodané soubory stále používají kontrolní součty zkompilované v programu. Stažené verze mají chráněný záznam s verzí, původem a hashi jednotlivých souborů, uložený ve stejném systému integrity konfigurace jako ostatní citlivé nastavení. Výběr aktivní verze se provádí až po úspěšném ověření. Při každém použití se hashe znovu kontrolují a soubory zůstávají během operace zamčené proti přepsání.

SHA-256 pochází z metadat oficiálního vydání nebo z HTTPS stránky poskytovatele. Není vydáván za digitální podpis výrobce. Instalace se neprovádí, pokud očekávaný součet chybí, liší se obsah, nesouhlasí verze, chybí požadovaný soubor nebo není balíček vhodný pro Windows x64.

Přerušené stahování a neúspěšná příprava neaktivují nový balíček. Poškozený seznam aktualizací zablokuje aktualizační akce a ukáže chybu; nepřepíše se tiše novým stavem a aplikace může dál použít původní ověřené doplňky. Již ověřená verze v běžícím procesu zůstává připnutá, aby se její cesty neměnily pod rozpracovanou operací. Historické balíčky se automaticky nemažou; dostupné místo je proto potřeba sledovat při opakovaných změnách verzí.

## Ověření implementace

Testovací příkazy ve vývojovém sestavení:

```powershell
& '.\File Compass.exe' --vendor-updates-test 'E:\testy\updates'
& '.\File Compass.exe' --vendor-updates-live-test 'E:\testy\updates-live'
```

Každý příkaz používá vlastní testovací profil. Živý test navíc načte metadata výrobců a do testovací složky stáhne oficiální 7-Zip, ověří hash, rozbalení a použití nové knihovny archivovacím procesem. Běžná instalace se tím neaktualizuje. Testovací příkazy nejsou součástí distribučního Release sestavení.

## Vydání a vlastní aktualizace File Compass

Kontrola používá `https://api.github.com/repos/rathousky-pro/file-compass-web/releases?per_page=100`. Zdrojový repozitář aplikace zůstává soukromý. Veřejný repozitář webu nese pouze distribuční ZIP a poznámky vydání. Záznam bez vhodného vydání není označen jako aktuální verze.

Tag má tvar `v0.9.5-beta-build.20261104.66`, stabilní verze například `v1.0.0-build.20261105.01`. Soubor se vždy jmenuje `FileCompass-win-x64.zip`. Jednoznačný odkaz je `https://github.com/rathousky-pro/file-compass-web/releases/download/<tag>/FileCompass-win-x64.zip`. Pro poslední stabilní vydání lze použít `https://github.com/rathousky-pro/file-compass-web/releases/latest/download/FileCompass-win-x64.zip`; beta aktualizátor vybírá vydání přes API, protože latest vynechává prerelease.

Porovnává se číselná verze, kanál a build (datum a číselné pořadí). Stejný build ani downgrade se nenabízí. Stabilní verze má přednost před beta verzí stejného čísla. ZIP musí obsahovat `file-compass-release.json` s verzí, kanálem, buildem a SHA-256 všech dodaných souborů; shoda je kontrolována také proti informativní verzi EXE a Windows x64 hlavičce. Kontrolní součet celého ZIP pochází z GitHub asset digest.

**Stáhnout a aktualizovat** připraví balíček v datové složce a při volném programu zahájí restart. Při běžící práci zůstane dostupné **Nainstalovat a restartovat**. Pomocný EXE je součástí zkompilovaného programu, není stahován z jiného zdroje. Při běžném zavření proběhne uložení pohledů a historie; při zrušení zavření se instalace neprovede. Pomocník čeká na skutečný konec původního PID včetně kontroly času jeho vzniku a pak provádí zálohované náhrady souborů. Nezabíjí jiné instance. Zamčené soubory zkouší znovu; při neúspěchu vrací již změněné soubory.

Profilové složky `config`, `data`, `local-profile` a vývojová značka nejsou součástí aktualizačního ZIP a nejsou přepisovány. Povolen je pouze hlavní EXE/konfigurace EXE a adresáře libraries, plugins, preview, icons, languages a help. Instalace do nezapisovatelné složky se odmítne ještě před zavřením aplikace. Každý pokus má samostatnou zálohu, žurnál a výsledek ve složce `data/updates/application/<id>/attempt-<id>`. Starší soubory, které nové vydání neobsahuje, se automaticky nemažou.

Příprava distribučního balíčku bez publikace:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/package-release.ps1 -Build
```

Skript sestaví čistý Release, vytvoří manifest, ZIP, součet a release-info.json. Volitelné `-Publish` nahraje balíček přímo přes GitHub REST API; GitHub CLI není potřeba. Použije existující GH_TOKEN/GITHUB_TOKEN z prostředí nebo přihlášení uložené u Gitu přes správce přihlašovacích údajů. Git se hledá stejně jako při publikaci webu i v přibaleném vývojovém runtime. Pokud uložené přihlášení chybí, správce Gitu může otevřít přihlášení v prohlížeči. Token se nevypisuje, neukládá do skriptu a není součástí klienta. Přihlášený účet potřebuje oprávnění Contents write v repozitáři webu. Beta vydání se označí jako prerelease.

Již připravený ZIP lze nahrát bez sestavování a přebalení:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/package-release.ps1 -ReleaseDirectory "E:\Programování\Commander\ai-docs\releases\github-updater-final" -Publish
```

Bez `-Publish` stejný příkaz pouze ověří připravené soubory a nepřistupuje k přihlášení ani síti. Před nahráním se ověří lokální ZIP, jeho součet a shoda manifestu s tagem. Vydání nejprve vznikne jako koncept, nahraje se ZIP a součet a zkontrolují se hashe vrácené GitHubem; teprve potom se zveřejní. Přerušené nahrávání lze dokončit stejným příkazem. Již zveřejněné totožné vydání se pouze ověří; konfliktní soubory se nepřepisují. Tyto změny samy žádné vydání nepublikují.

Publikační postup lze ověřit offline příkazem `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-github-release.ps1`. Testy používají simulované GitHub odpovědi a nečtou skutečné přihlašovací údaje.

Test: `--application-updates-test <adresář>` používá vlastní x64 EXE a instalační složku; ověřuje výběr vydání, podvržené balíčky, čekání na konec procesu, výměnu, restart a rollback, aniž by měnil skutečnou instalaci.

Pokud byl pomocník přerušen výpadkem napájení, lze po zavření aplikace spustit `restore.ps1` v příslušné složce attempt. Pomocník ověří zálohy podle žurnálu, vrátí předchozí soubory a spustí aplikaci. Po úspěšné aktualizaci může tentýž postup posloužit jako ruční návrat původního sestavení.