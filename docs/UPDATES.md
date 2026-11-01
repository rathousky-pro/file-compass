# Aktualizace programu a doplňků

V **Konfigurace → Aktualizace** najdete instalovanou a dostupnou verzi, výsledek poslední kontroly, zdroj a cestu k používanému souboru. Kontrolu lze spustit ručně nebo povolit jednou denně. Denní kontrola sama žádné balíčky nestahuje. Změna jejího nastavení se ukládá tlačítkem **Uložit**; kontrola, stažení a použití balíčku jsou samostatné okamžité akce.

## Zdroje

| Součást | Zdroj a podpora |
|---|---|
| 7-Zip | [Oficiální repozitář ip7z/7zip](https://github.com/ip7z/7zip/releases), Windows x64 balíček; `7z.exe` a `7z.dll` společně. |
| FFmpeg + FFprobe | [Plné statické Windows sestavení gyan.dev](https://www.gyan.dev/ffmpeg/builds/), na které odkazuje [ffmpeg.org](https://ffmpeg.org/download.html); oba nástroje společně. |
| rclone | [Oficiální repozitář rclone/rclone](https://github.com/rclone/rclone/releases), Windows AMD64 balíček. |
| smartmontools | [Oficiální vydání](https://github.com/smartmontools/smartmontools/releases). Verze se kontroluje, ale automatická instalace vyžaduje zveřejněný SHA-256. Aktuální starší vydání na GitHubu má místo něj PGP/MD5 soubory; dokud aplikace neověřuje PGP, nabídne stránku výrobce. |
| Magick.NET / OpenMP | Přehled ukazuje instalovanou verzi. Spravovaná knihovna, nativní OpenMP knihovna a odpovídající runtime se aktualizují společně s File Compass. Samostatná aktualizace FFmpegu je nenahrazuje. |
| Knihovny a ovladače | Síťové/náhledové knihovny zůstávají součástí vydání File Compass. SSHFS/WinFsp nadále používají instalační postupy výrobce. |
| File Compass | Řádek je připravený pro vydání na GitHubu. Adresa zatím nebyla určena, proto kontrola a stažení programu nejsou aktivní. |

Zjištění verze a ověření stahovaného balíčku jsou samostatné kroky. Pokud metadata výrobce potvrzují stejnou nebo starší verzi, stav je **Aktuální** i tehdy, když vydavatel neposkytuje SHA-256. U novější verze bez SHA-256 stav nabízí ruční stažení; automatické stažení a instalace zůstávají vypnuté. Podrobnost u řádku vysvětluje chybějící kontrolní součet. Chyba síťové kontroly se nadále zobrazuje jako chyba.

Používají se stabilní vydání. Neexistuje vlastní přebalovací server ani odhadované číslo nejnovější verze. Výpadek sítě nebo chybějící ověření se zobrazí jako chyba, nikoli jako „Aktuální“. GitHub může omezit počet anonymních požadavků; ruční kontrolu lze zopakovat později. FFmpeg se kontroluje proti verzi a SHA-256 zveřejněným přímo na stránkách poskytovatele Windows sestavení.

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
