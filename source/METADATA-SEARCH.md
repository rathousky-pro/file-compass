# Hledání podle metadat

Otevřete **Hledat (Alt+F7) → Metadata**. V Obecné nastavte složku, masku a hloubku hledání.
V Metadata vyberte příklad, nebo použijte **Přidat podmínku**:
- Šířka / Výška v px, megapixely;
- počet stran dokumentu, počet listů tabulky a snímků prezentace;
- autor, titulek, fotoaparát;
- interpret, album, délka média a datový tok zvuku;
- datum pořízení fotografie.

Číselné hodnoty podporují větší než, alespoň, rovno, menší než a nejvýše.
Text podporuje obsahuje, je rovno a začíná na. Přepínač rozlišování velikosti písmen je společný s ostatním hledáním.
Délku zadávejte jako hh:mm:ss (např. 01:00:00); pro více dnů také d.hh:mm:ss.
Datum pořízení se porovnává jako kalendářní datum bez času.
Více podmínek musí platit současně. Soubor bez požadovaného údaje nevyhoví podmínce.

Příklady v nabídce:
- fotografie širší než 4000 px;
- PDF s více než 20 stranami;
- dokumenty od autora (doplňte jeho jméno);
- video delší než hodinu.

Příklad nastaví podmínky a odpovídající masky souborů v Obecné.
Ostatní omezení (velikosti, data, výběr, obsah, hloubka) zůstávají platná.
Podmínky se ukládají a obnovují s profily hledání. Staré profily fungují beze změny.

Výsledky ukazují hodnoty, které podmínkám vyhověly. Dvojklik otevře umístění;
výsledky lze předat do panelu jako u běžného hledání.
Během čtení se ve spodním řádku ukazuje kontrolovaný soubor. Storno hledání ukončí.

Hledání metadat je dostupné v místních složkách a běžných síťových sdíleních.
Archivní a přímé FTP/SFTP hledání tyto podmínky nepodporuje a nesmí je tiše ignorovat.
Čtení běží na pozadí v izolovaných procesech s časovým limitem. Nepodporované přípony se předem vynechávají.
Dostupnost vlastností závisí na formátu a kodeku. Pro chráněné, poškozené či příliš náročné soubory mohou údaje chybět.
Počet stran PDF je skutečný počet z PDF; u Office/ODF se používá uložený údaj dokumentu, který nemusí odpovídat novému přepočtu.