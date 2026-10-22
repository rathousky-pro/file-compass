# Sloupce s metadaty a automatické pohledy

## Volitelné sloupce
Klikněte pravým tlačítkem na záhlaví Název / Přípona / Velikost. Zaškrtněte požadované sloupce:
rozlišení, megapixely, datum pořízení, fotoaparát, počet stran/listů/snímků prezentace,
autor, titulek, délka, interpret, album, datový tok zvuku, rodina písma, verze produktu, architektura.
Sloupce se zobrazují v podrobném seznamu. Kliknutí na záhlaví řadí vzestupně/sestupně.
Počty, délky a datum se řadí jako hodnoty; rozlišení podle počtu pixelů. Chybějící hodnoty zůstávají na konci mezi soubory.
Adresáře zůstávají před soubory. Šířku sloupců lze upravit tažením.
Volba sloupců je součástí uloženého pohledu, záložek i pracovních sad.

Metadata viditelných řádků se načítají na pozadí, nejvýše dvěma izolovanými pomocnými procesy.
Řazení podle metadat postupně načte také ostatní soubory složky. Při odchodu se čtení ruší.
Nečitelné či nepodporované údaje zůstanou prázdné. Obsah archivu, vzdálené FTP/SFTP panely,
virtuální souhrny a systémové přehledy se takto neprocházejí. Místní a běžné sdílené složky ano.
Počty stran Office/ODF jsou uložené údaje dokumentu a nemusí odpovídat novému přepočtu v editoru.
Počet stran PDF se čte přímo z PDF.

## Automatické pohledy
Konfigurace → Nastavení → Vzhled → Automatické pohledy složek:
**Automaticky přizpůsobovat pohled obsahu složky**. Ve výchozím stavu vypnuto.

Po zapnutí se při vstupu do jiné složky vyhodnotí běžné soubory (bez skrytých/systémových):
- alespoň 60 % obrázků: Miniatury; v detailním režimu rozlišení, pořízení, fotoaparát;
- dokumenty: podrobný seznam, autor, počet stran a listů;
- hudba: interpret, album, titulek, délka;
- video: délka a rozlišení;
- prázdné a smíšené složky: původní pohled.

Ručně změněný pohled se při pouhém obnovení stejné složky nepřepíná.
Vypnutí volby vrátí původní pohled aktuálního panelu.

## Vlastní pohled složky
Nastavte sloupce, šířky, řazení a režim zobrazení.
Pravým tlačítkem na záhlaví vyberte **Uložit pohled pro tuto složku**,
nebo **Uložit pohled pro tuto složku i podsložky**. Uložení nevyžaduje potvrzení.
Tyto předvolby se používají, když jsou zapnuté automatické pohledy.
Přesnější cesta má přednost před předvolbou nadřazené složky; vlastní předvolba před rozpoznáním obsahu.
**Odstranit uložený pohled této složky** smaže předvolbu pro aktuální cestu.
Předvolby jsou v přenosném souboru config/folder-views.xml; vypínač je v config/settings.xml.