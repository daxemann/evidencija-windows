# Lovačka Evidencija za Windows

Program za Windows **bez interneta**: članovi, članarina, bodovi i imenik za lovačke udruge.
Besplatno. Samostalan projekt, odvojen od online aplikacije Lovačka Evidencija.

## Stanje
U izradi (0.2.0): prijava i šifriranje, članovi, imenik, sekcije. Slijede članarina, bodovi, uvoz, kopije, instalacija.

## Sigurnost ukratko
- Lozinka + rezervna lozinka; ključ podataka postoji samo u memoriji dok je program otključan.
- Osjetljiva polja šifrirana (AES-256-GCM, PBKDF2-SHA256 – samo ugrađeni .NET alati).
- Podaci u `%LOCALAPPDATA%\LovackaEvidencija`, nikad u mapi programa.

## Licenca
AGPL-3.0 (vidi `LICENSE`). Bootstrap: MIT.

## Razvoj
- .NET 10 SDK, Windows 10 (2004) ili noviji
- `dotnet build EvidencijaWindows.sln` · `dotnet test EvidencijaWindows.sln`
- Testni podaci (samo Debug): varijabla `EVIDENCIJA_TEST_MAPA`
