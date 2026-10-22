# Synchronizace, velikosti, obrázky a dělení souborů

## 9. Obousměrná synchronizace

Otevřete dvě složky a zvolte **Příkazy → Synchronizovat složky… → Obousměrně ↔ → Porovnat**. Výběr obousměrného režimu zapne porovnání obsahu; lze jej ručně vypnout pro porovnání pouze podle velikosti a času.

U každého řádku lze zvolit **→**, **←** nebo **Přeskočit**. V obousměrném režimu šipky odpovídají levému a pravému panelu. **≠** označuje nevyřešený konflikt, který blokuje spuštění. Kolizi souboru se složkou lze přeskočit.

Jednostranné položky se nabídnou ke kopírování na druhou stranu. U rozdílných souborů se navrhne směr od novějšího; shodné časy vyžadují rozhodnutí. Režim nepropaguje mazání a neukládá databázi předchozího běhu. U obou kopií změněných od minulého běhu tedy návrh vychází z aktuálních časů a lze jej změnit.

Před prvním zápisem se ověří oba konce plánu. Zastaralý plán se odmítne. Jednosměrná synchronizace a její explicitní zrcadlení zůstávají dostupné. Sdílené směrování využívá také stávající FTP/SFTP transportní vrstvu; živé produkční servery nebyly při této úpravě použity.

## 9. Přehled velikostí v panelu

**Alt+Shift+Enter** nebo **Příkazy → Analýza velikosti podsložek** spustí výpočet na pozadí. U položek se zobrazí procentuální pruh, podíl na celku a velikost složky. Další stisk analýzu zruší nebo pruhy skryje; opuštění složky ruší výpočet.

Podporují se místní a připojené souborové disky. Součty jsou logické délky souborů, nikoli skutečná alokace clusterů či deduplikovaný prostor; nejde o čtení MFT jako u WizTree. Odkazy se neprocházejí. Nepřístupná místa se vynechají a jejich počet a podrobnosti se zobrazí ve stavovém řádku a jeho tooltipu. Takový součet je částečný.

## 10. Rychlé úpravy obrázků

Označte obrázky a zvolte **Soubor → Rychlé úpravy obrázků…**. K dispozici jsou otočení 0°/90°/180°/270°, ponechání rozlišení, maximální delší strana například 1920 px nebo procenta například 50 %. Maximální strana menší obrázky nezvětšuje.

Výstup může být PNG, JPEG, BMP nebo TIFF; JPEG má nastavitelnou kvalitu. Vznikají nové kopie s doplněným **_upraveno** před příponou. Originály ani existující výsledky se nepřepisují. Shodné cílové názvy se odmítnou před zpracováním.

EXIF orientace se zohlední před ručním otočením. Metadata se do výsledků nepřenášejí. JPEG/BMP používají bílé pozadí místo průhlednosti. Vícesnímkové vstupy se zachovávají v TIFF; převod do jednosnímkového formátu se odmítne. Při zrušení mohou zůstat již dokončené kopie, originály zůstávají zachované.

## 11. Přesun do archivu a svazky

V dialogu **Komprimovat (Alt+F5)** jsou aktivní **Přesunout do archivu** a **Dělený archiv na více médií**. Přesun vyžaduje zahrnutí podsložek a nový výstupní název.

Archiv se automaticky otestuje, rozbalené soubory se porovnají se zdroji pomocí SHA-256 a při ukládání cest se ověří také prázdné složky. Před mazáním se znovu porovnají všechny zdroje. Každý soubor se uzamkne proti změně a přejmenování, ověří a odstraní přes stejný souborový handle; složky se odstraňují pouze prázdné.

Změna zdroje zastaví další mazání. Ověřený archiv a zbývající zdroje zůstanou zachované. Při zrušení mohou být některé již ověřené zdroje odstraněné; jejich obsah zůstává v archivu.

Dělení podporuje **ZIP a 7z**, včetně hesla. Nabízí **100 MB / 700 MB / 4 GB** nebo vlastní velikost. Jednotky jsou násobky 1024. Vznikají například **data.7z.001**, **data.7z.002**… a **data.7z.parts.json**. ZIP používá **.zip.001**, nikoli **.z01**.

První **.zip.001 / .7z.001** otevře obsah přímo v programu, pokud jsou všechny díly ve stejné složce. Svazky lze testovat a rozbalovat; průběžná úprava archivu není povolena. Vytvoření potřebuje místo na dočasný úplný archiv. Existující výstupy se nepřepisují.

## 11. Rozdělit a sloučit soubory

**Soubor → Rozdělit soubor…** vytvoří z jednoho vybraného souboru díly **.001, .002…** a kontrolní **.parts.json**. Původní soubor zůstává zachovaný.

**Soubor → Sloučit soubory…** nad vybraným číslovaným dílem najde celou řadu od .001. S manifestem ověří počty, délky, SHA-256 dílů i výsledku. Chybějící nebo poškozený díl zabrání zveřejnění výsledku.

Díly z jiných programů lze spojit i bez manifestu; potom nelze ověřit původní hash ani chybějící poslední díl. Pro vlastní přenos zachovejte i .parts.json. Vstupní díly se nemažou a výsledný soubor vyžaduje nový název.