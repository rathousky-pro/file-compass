# Přihlášení Google pro všechny uživatele File Compass

Jedna aplikace OAuth identifikuje **File Compass**. Její Client ID není e-mail uživatele a neurčuje, čí disk se otevře. Každý člověk se přihlásí vlastním Google účtem v prohlížeči a schválí přístup ke svému disku. Tokeny zůstávají odděleně v jeho lokálním šifrovaném profilu.

E-mail podpory v Google Auth Platform je veřejný kontakt na vydavatele. Není to přihlašovací účet pro všechny uživatele. Může jít o samostatnou adresu podpory. Do instalačního balíčku nikdy nepatří osobní uživatelské tokeny ani obsah vydavatelova `config/cloud`.

## Registrace vydavatelem

1. Samostatný projekt; aktuálně vybraný `file-compass-512408`. Povolit Google Drive API, bez zapínání placených služeb nebo fakturace.
2. Google Auth Platform → Branding: **File Compass**, schválený kontakt podpory a kontakt vývojáře. Pro veřejnou distribuci také skutečná veřejná stránka aplikace a zásady ochrany soukromí na doméně vydavatele.
3. Audience → **External**, aby lidé používali své vlastní účty. Testing je vývojový mezikrok, nikoli dokončená veřejná služba.
4. Vytvořit OAuth klienta **Desktop app** po schválení případných podmínek a vytvoření přihlašovacích údajů vlastníkem.
5. Deklarovat oprávnění pro používané funkce Google Drive a dokončit požadované veřejné ověření Googlu. `drive.file` samo o sobě neumožňuje libovolné procházení celého existujícího disku.
6. Stažený desktopový JSON vložit jako `source/plugins/cloud/google-oauth.json`. Musí obsahovat `installed.client_id` a `installed.client_secret`. Hodnoty nepatří do logů nebo reportů; soubor je vynechán z verzování. Údaje patří aplikaci, nikoli běžnému uživateli.
7. Sestavení kopíruje soubor do `config/plugins/cloud/google-oauth.json`. Otestovat skutečné přihlášení a název File Compass v souhlasu Googlu, před veřejným vydáním také nezávislý účet bez vývojářských rolí a produkční publikum.

Bez dokončené registrace je technická podpora připravená, ale Google přihlášení není aktivované pro veřejnost. Program tento stav vysvětlí a nepožaduje po uživateli vytváření ID.

## Implementace

`CloudApplicationOAuth` načítá konfiguraci vydavatele, kontroluje desktopový formát a identitu dodává do prostředí pomocného procesu rclone. Nepředává ji v argumentech našeho spouštění. Google účty přihlášené novou cestou mají marker `AuthorizationClient=FileCompass`. Knihovna nepokračuje na vyřazované společné ID rclone a neotevírá formuláře pro vývojářská ID.

Existující účty se automaticky nepřepínají na jiné Client ID: jejich tokeny zůstávají spojené s původní aplikací do výslovného nového přihlášení. Obnova autorizace ověří nový šifrovaný profil a atomicky ho vymění včetně identity aplikace. Neúspěch ponechá původní účet beze změny.

OneDrive a Dropbox nepřebírají Google identitu; jejich dosavadní přihlášení zůstává zachované. Podmínky pracovních či týmových účtů mohou záviset na jejich správci a zvolených funkcích.

## Primární dokumentace

- [OAuth pro desktopové aplikace](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Oprávnění Google Drive](https://developers.google.com/workspace/drive/api/guides/api-specific-auth)
- [OneDrive](https://rclone.org/onedrive/), [Dropbox](https://rclone.org/dropbox/)
