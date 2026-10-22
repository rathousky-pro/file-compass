# Hledání, přejmenování a velikosti složek

- Hledat soubory: Příkazy → Hledat soubory, lupa nebo Alt+F7. Masky lze
  oddělit středníkem. Dostupné jsou regulární výrazy, omezení na vybrané
  položky, hloubka podsložek a rozsah velikostí. Dvojklik otevře umístění.
- Hledání názvů a velikostí funguje v místních/SMB/NFS složkách, v otevřeném
  archivu a na FTP/SFTP/SCP. Síťové hledání používá nezávislé spojení.
  Hledání textu je dostupné pouze v místních/SMB/NFS souborech, podle BOM
  nebo UTF-8, v souborech do 64 MB. Výsledky mají limit 20 000 položek.
- Hromadné přejmenování: Soubor nebo Příkazy → Hromadné přejmenování,
  ikona A/B nebo Ctrl+M. Dostupné pro místní/SMB/NFS soubory a složky.
  V archivu a na FTP/SFTP/SCP je zatím dostupné jednotlivé přejmenování.
- Masky: `[N]` původní název, `[E]` přípona, `[C]` počítadlo,
  `[N1-3]` první tři znaky, `[E1]` první znak přípony, `[RMD]` datum změny,
  `[hms]` čas změny. Najít/nahradit podporuje regulární výrazy a velikost písmen.
- Náhled se aktualizuje během psaní. Kolize a neplatné názvy blokují spuštění.
  Přejmenování používá dočasné názvy pro podporu výměny názvů. Chyba nebo
  zrušení vrací rozpracované změny; poslední úspěšný běh lze vrátit tlačítkem.
- Mezerník na složce ji označí a na pozadí spočítá velikost celého obsahu.
  Číslo nahradí `<DIR>` a zahrne se do souhrnu výběru. Funguje také
  v archivu a na FTP/SFTP/SCP. Odkazy se nenásledují. Při chybě se místo
  neúplného čísla zobrazí hláška a složka ponechá `<DIR>`.
