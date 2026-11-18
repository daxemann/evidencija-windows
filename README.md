# Lovačka Evidencija za Windows

Program za Windows **bez interneta**: članovi, imenik, članarina (s HUB-3A uplatnicama) i bodovi za lovačke udruge.
Besplatno. Samostalan projekt, odvojen od online aplikacije Lovačka Evidencija.

## Instalacija
1. Preuzmite `Evidencija-Setup-x.y.z.exe` s [Releases](https://github.com/daxemann/evidencija-windows/releases).
2. Dvoklik → **Instaliraj**. Ne treba administratorska lozinka ni dodatni programi.
3. Windows može prikazati **„Windows je zaštitio vaše računalo“** (program nije potpisan plaćenim certifikatom):
   kliknite **Više informacija** → **Ipak pokreni**.
4. Prvo pokretanje: postavite **lozinku** i **rezervnu lozinku** (zapišite je!), zatim u
   *Postavke → Sigurnosne kopije* odaberite USB ili mapu u oblaku.

Provjera preuzete datoteke (nije obavezno): SHA-256 je u opisu izdanja (`Get-FileHash Evidencija-Setup-x.y.z.exe`).

- Windows 10 (verzija 2004 ili novija) ili Windows 11, 64-bitni
- Nova verzija se instalira preko stare – podaci ostaju
- Deinstalacija ne briše podatke (`%LOCALAPPDATA%\LovackaEvidencija`)

## Što program radi
- **Članovi** sa svim podacima (osjetljivi šifrirani), fotografija, sekcija, upozorenje za lovačku iskaznicu
- **Imenik**, pretraga bez kvačica, filtar po sekciji
- **Članarina**: iznosi po godini i vrsti članstva, rate, uplate, HUB-3A uplatnice (PDF s 2D kodom)
- **Bodovi**: vrste akcija i dodaci (traktor, motorna pila …), obveza, prijenos u novu godinu (staro/novo)
- **Uvoz** iz Excela/CSV-a, **PDF popisi** (uz ponovni upis lozinke)
- **Šifrirane sigurnosne kopije** pri svakom zatvaranju, vraćanje i na novom računalu

## Sigurnost ukratko
- Lozinka + rezervna lozinka; ključ podataka postoji samo u memoriji dok je program otključan.
- Osjetljiva polja šifrirana (AES-256-GCM, PBKDF2-SHA256 – samo ugrađeni .NET alati); kopije su šifrirane cijele.
- Bez interneta, bez slanja podataka. Podaci u `%LOCALAPPDATA%\LovackaEvidencija`, nikad u mapi programa.
- Bez obje lozinke podaci se ne mogu vratiti – namjerno nema „stražnjih vrata“.

## Licenca
AGPL-3.0 (vidi `LICENSE`). Bootstrap: MIT · ZXing.Net: Apache-2.0 · QuestPDF: Community licenca (besplatno za neprofitne udruge i pojedince) · ClosedXML: MIT.

## Razvoj
- .NET 10 SDK, Windows 10 (2004) ili noviji
- `dotnet build EvidencijaWindows.sln` · `dotnet test EvidencijaWindows.sln`
- Instalacija: `.\alati\izgradi.ps1` (treba Inno Setup 6 i `instalacija\MicrosoftEdgeWebview2Setup.exe` od Microsofta)
- Testni podaci (samo Debug): varijabla `EVIDENCIJA_TEST_MAPA`
