# Duplicity a porovnání souborů

Příkazy → Hledat duplicity…
Zadejte jednu nebo více složek oddělených středníkem. Hledání prochází podsložky, nejprve seskupí soubory podle velikosti a potom porovná SHA-256 celého obsahu. Stejný fyzický soubor dostupný přes alias nebo pevný odkaz se počítá pouze jednou. Symbolické odkazy a reparse body se přeskočí; nedostupné položky se uvedou v souhrnu.
Výsledky obsahují skupinu, název, cestu, velikost a společný náhled. Dlouhý společný začátek cest je zkrácen; plná cesta je v tooltipu řádku.
Označit kopie ponechá v každé skupině první soubor podle cesty a označí ostatní. Výběr lze upravit jednotlivě.
Přesun do koše vyžaduje potvrzení. Kontroluje se obsah, velikost, čas a fyzická identita souborů; změna blokuje plán. V každé skupině musí zůstat neoznačený zástupce, který se znovu ověřuje i během operace. Částečně dokončený přesun se ve výsledcích zachová. Hledání i kontrolu lze zrušit.
Archivy a FTP/SFTP nejprve stáhněte/rozbalte. Připojené disky dostupné jako běžné složky lze prohledávat.

Příkazy → Porovnat podle obsahu… / Ctrl+Shift+D
Vyberte dva soubory v jednom panelu nebo dvojici ve dvou panelech. V dialogu lze oba soubory také vybrat ručně.
- Texty: původní barevné porovnání řádků vedle sebe, navigace mezi rozdíly, volby ignorování velikosti písmen/koncových mezer.
- Binární soubory: původní porovnání bajtů.
- Obrázky: automatické rozpoznání běžných obrazových formátů, zobrazení vedle sebe nebo překrytí odkrývané posuvníkem. V překrytí mají oba obrázky společné měřítko a počátek, proto je patrný i rozdíl rozměrů.
Obrázky se z důvodu paměti dekódují nejvýše na 4096 px na delší straně, zdroje se nemění. Limit souboru je 128 MB a 100 milionů zdrojových pixelů. Vizuální porovnání nenahrazuje porovnání původních bajtů.
Dva výsledky hledání duplicit lze otevřít ve stejném porovnávacím dialogu pomocí Ctrl + kliknutí na řádky a tlačítka Porovnat.
