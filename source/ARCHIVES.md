# Archivy v CommanderCZ

Konfigurace → Archivy otevře nastavení WinRARu. Cestu lze zadat ručně,
vybrat soubor nebo automaticky vyhledat běžnou instalaci. Nastavení se ukládá
do `%APPDATA%\CommanderCZ\archives.xml`.

7-Zip 26.04 je přenosný modul v `plugins\7zip` vedle CommanderCZ.exe.
Používá původní 7z.exe a 7z.dll, Windows nevyžadují instalaci ani registraci.
Při přenášení aplikace na jiný počítač přeneste i tuto složku.

- Enter / dvojklik na ZIP, 7z nebo RAR zobrazí obsah v panelu. Procházení
  načítá pouze seznam položek. Nadřazená položka nebo Backspace opustí archiv.
- F5 z archivu rozbalí označené položky do druhého panelu. F5 do archivu
  přidá položky do právě otevřené složky uvnitř archivu. Dialog nabízí
  přeskočení nebo přepsání existujících souborů.
- Soubor → Komprimovat (Alt+F5) nebo tlačítko v horní liště vytvoří
  nový ZIP, 7z nebo RAR z výběru. Výchozí cílová cesta je složka druhého
  panelu. Kompaktní dialog nabízí kompresní poměr 0–9, relativní cesty,
  podsložky, heslo, samostatné archivy a vynechání základní složky.
  RAR používá odpovídající stupeň na stupnici WinRARu 0–5. Při odstranění
  cest se shodné názvy souborů odmítnou. Existující archivy se nahrazují
  až po potvrzení. Přesun do archivu je dostupný po úplném ověření; dělení na svazky podporuje ZIP a 7z. SFX zůstává nedostupné. Podrobnosti jsou v ADVANCED-TOOLS.md.
- Soubor → Extrahovat (Alt+F9) rozbalí označené archivy do zvolené složky.
  Nabízí přeskakování nebo přepis existujících souborů. Uvnitř otevřeného
  archivu rozbalí vybrané položky do druhého panelu stejně jako F5.
- Soubor → Test archivu (Alt+Shift+F9) ověří celý obsah a kontrolní součty
  označených archivů, případně právě otevřeného archivu. Zobrazuje průběh,
  umožňuje zrušení a po dokončení oznámí výsledek. Šifrovaný archiv vyžaduje heslo.
- F4 rozbalí jednotlivý soubor do TEMP a otevře editor. Po uložení v editoru
  použijte tlačítko **Uložit do archivu** v pomocném okně Commanderu.
- Vnořený archiv se otevře stejným způsobem. Uložení změny zapíše i jeho
  nadřazené archivy, pokud všechny podporují zápis.
- F7 vytvoří složku, Shift+F6 přejmenuje položku a F8 / Delete ji odstraní.
- ZIP a 7z zapisuje 7-Zip. RAR zapisuje nastavený WinRAR (přednostně jeho
  Rar.exe ze stejné složky). Ostatní podporované formáty a vícedílné archivy
  slouží pro čtení a rozbalování.

Operace běží na pozadí a mají řádek průběhu nad funkčními tlačítky.
Ve spodní části je jeden společný vyšší ukazatel. Kliknutí otevře detail
všech operací; odtud lze otevřít síťovou frontu se seznamem souborů.
Světle modrá výplň ukazuje soubor, tmavě modrá výplň přes ni ukazuje
celou úlohu. Při souběhu úloh společný ukazatel zobrazuje průměr jejich průběhů.
Lokální kopírování do archivu nebo z archivu má vlastní dialog s filtrem,
přeskakováním nebo přepisem, prázdnými složkami, vynecháním skrytých položek
a volitelným ověřením SHA-256. Kontrola integrity upravovaného archivu je vždy zapnutá.
Přenosy mezi obsahem archivu a FTP/SFTP/SCP používají síťovou frontu.
Při zápisu vznikne pracovní kopie vedle původního archivu. Původní archiv
se nahradí až po kontrole integrity. Zrušení nebo chyba před nahrazením
zachová původní soubor. Na disku je potřeba prostor pro pracovní kopii
a dočasně rozbalené vybrané soubory.

Hesla archivů se drží pouze v paměti; neukládají se do konfigurace.
Nebezpečné cesty, duplicitní názvy a odkazy uvnitř archivů program odmítne.
Pokud archiv mezitím změnil jiný program, je nutné obnovit nebo znovu
otevřít jeho obsah.

Ověření: `CommanderCZ.exe --archive-test <výstupní-složka>` používá skutečný
7-Zip a WinRAR. Testuje ZIP, 7z, RAR, vybrané rozbalování, kolize, změny,
vnořené archivy, zrušení, šifrování a nebezpečné položky.
# Bezpečnostní aktualizace 16. 10. 2026

ZIP/7z a čtení ostatních podporovaných formátů nyní používají knihovní rozhraní
SharpSevenZip + přibalenou `7z.dll` v odděleném pracovním procesu. Heslo se předává
callbackem, nikdy příkazovou řádkou. Šifrované RAR lze číst; jejich zápis je
zablokovaný. Pro tvorbu archivů s heslem použijte ZIP nebo 7z.

Nastavení → Archivy nabízí limity položek, paměti, času pracovního kroku,
rozbalených dat, rezervy disku, výpisu a vnoření. Výchozí paměť je 1024 MiB,
čas kroku 600 s a objem 65536 MiB. Aktualizace ZIP/7z pracuje s novou pracovní
kopií a může potřebovat více času a dočasného místa než dříve.

Podrobnosti a výsledky testů: `../reports/Archivy-limity-a-prejmenovani-2026-10-16.md`.

