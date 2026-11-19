# Izmišljena udruga – za isprobavanje uvoza

**Sve osobe, adrese, brojevi telefona i e-pošte su IZMIŠLJENI.** Nema stvarnih podataka.
Telefoni su oblika `09x 000 xxxx`, e-pošta završava na `@primjer.invalid`, OIB-a nema.

| Datoteka | Sadržaj |
|---|---|
| `izmisljena-udruga.xlsx` | Excel, 64 reda |
| `izmisljena-udruga.csv` | isti podaci, CSV s točka-zarezom, Windows-1250 (kao stari Excel) |

Upotreba: u programu **📥 Uvoz članova** → odaberite datoteku → Pregled → Uvezi.

Što se može isprobati:
- 3 sekcije i 4 vrste članstva – program ih sam napravi
- telefoni u različitim zapisima (`0980001234`, `098 000 1234`, `+385 98 0001234`, `098/0001234`)
- „33401 Hrastovac Gornji“ u stupcu Mjesto → razdvaja se na poštanski broj i mjesto
- datum rođenja kao Excel-datum i kao tekst `5.3.1970.`, „Član od“ samo godina
- lovačke iskaznice: neke istekle, neke ističu za manje od 30 dana (upozorenje na Početnoj)
- namjerne greške na kraju: duplikat imena, red bez imena, krivi OIB i datum, samo jedna riječ u imenu

Savjet: isprobavajte u testnoj instalaciji, ne u stvarnim podacima udruge.
