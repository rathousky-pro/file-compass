# Podrobnosti souborů v náhledu
Pod obrazovým nebo textovým náhledem je rozbalovací část **Podrobnosti**. Zobrazuje souhrn podle typu souboru a dostupné údaje; tlačítkem Kopírovat údaje lze celý přehled vložit do schránky.
Ve spodním náhledu jednoho panelu se rozbalené údaje přesunou vedle obrázku, aby nezakryly samotný náhled.

- Obecně: název, formát, velikost, vytvoření, změna a atributy.
- Obrázky: původní rozměry, megapixely, DPI, barevný formát a hloubka, alfa kanál / průhledná paleta, počet snímků, přítomnost barevného profilu.
- Fotografie s EXIF: datum pořízení, výrobce a model fotoaparátu, dostupný objektiv, clona, ISO, expoziční čas, ohnisko, orientace, GPS, software a copyright. Údaje závisejí na tom, co skutečně obsahuje soubor.
- ICO/CUR: počet variant a jejich rozměry.
- PDF: skutečný počet stran, verze, titul a další uložená metadata, rozměr první strany a textová ukázka první strany. Nejde o grafické vykreslení PDF; sken bez textu vyžaduje OCR, které tato funkce neprovádí.
- DOCX/DOCM a šablony Wordu: vlastnosti dokumentu, autor a poslední editor, uložené počty stran/slov, odstavce, komentáře, přítomnost maker, krátká textová ukázka. Uložené počty mohou být zastaralé.
- XLSX/XLSM a šablony Excelu: listy a jejich skrytí, uložené oblasti prvních šesti listů, vlastnosti a ukázka sdílených textových hodnot. Oblasti jsou uložené v souboru a nemusí odpovídat přesně neprázdným buňkám. Vzorce se nepřepočítávají, makra se nespouštějí.
- PPTX/PPTM a šablony prezentací: počet snímků, vlastnosti a textová ukázka prvního snímku.
- ODT/ODS/ODP: uložená metadata, dostupné statistiky a textová ukázka.
- Starší DOC/XLS/PPT, zprávy a zástupci: dostupné vlastnosti poskytované Windows; rozsah závisí na instalovaných ovladačích vlastností.
- Audio/video: dostupná délka, titul, interpret, album, žánr, rok, stopa, datový tok, vzorkování, kanály a rozměry videa.
- ZIP/7z/RAR/TAR a další podporované archivy: počty souborů a složek, celková rozbalená velikost, poměr velikostí a informace o šifrování; u ZIP i dostupný komentář.
- EXE/DLL: produkt, společnost, verze, copyright, původní název a architektura, bez spuštění programu.
- TTF/OTF/TTC: dostupná rodina, řez, styl, váha, verze, mapované znaky a práva vložení.
- Text/CSV/JSON/XML/SVG: kódování, konce řádků, počet řádků nebo počet v ukázce, oddělovač a sloupce prvního řádku, kořen JSON/XML a rozměry SVG/viewBox.

Podrobnosti se čtou na pozadí v odděleném procesu (384 MiB, přibližně 6,5 s, nejvýše dvě čtečky najednou). Rozsáhlé nebo poškozené soubory mohou zobrazit pouze základní údaje. XML odmítá DTD a externí entity. Metadata se načítají z místních souborů; vzdálený FTP/SFTP náhled si ponechává dosavadní omezené čtení.
