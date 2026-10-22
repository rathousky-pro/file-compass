# File Compass

Správce souborů pro Windows od Rathousky.pro. Projekt je zatím ve vývoji.

## Zdrojové kódy

- `source/` — aplikace, dokumentace, testy a potřebné knihovny.
- `tools/` — pomocné skripty pro vývoj.
- `source/FILE-COMPASS.md` — podrobnější popis programu.

## Sestavení

Na Windows s .NET Framework 4.8 spusťte z kořene projektu:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File source/build.ps1
```

Program se sestaví do `bin/File Compass.exe`.

Uživatelské profily, přihlašovací údaje, sestavení a místní reporty nejsou součástí repozitáře. Vlastní Google OAuth konfiguraci dodává vydavatel zvlášť; veřejné přihlašování Google Drive zatím není dokončené. Postup popisuje `source/GOOGLE-OAUTH-PUBLISHER.md`.
