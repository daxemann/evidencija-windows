namespace Evidencija.Podaci;

/// <summary>Izrada i nadogradnja baze. Verzija u PRAGMA user_version; svaki korak samo jednom.</summary>
public static class Shema
{
    private static readonly string[] Koraci =
    [
        // 1 – osnova
        """
        CREATE TABLE Postavke (
            Kljuc TEXT NOT NULL PRIMARY KEY,
            Vrijednost TEXT NULL
        );
        CREATE TABLE Promjene (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Tablica TEXT NOT NULL,
            ZapisId TEXT NOT NULL,
            Radnja TEXT NOT NULL,          -- dodano / promijenjeno / obrisano
            Vrijeme TEXT NOT NULL
        );
        CREATE INDEX IX_Promjene_Vrijeme ON Promjene (Vrijeme);
        """,

        // 2 – sekcije i članovi (polja s * su šifrirana)
        """
        CREATE TABLE Sekcije (
            Id TEXT NOT NULL PRIMARY KEY,
            Naziv TEXT NOT NULL,
            Skrivena INTEGER NOT NULL DEFAULT 0,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE Clanovi (
            Id TEXT NOT NULL PRIMARY KEY,
            Ime TEXT NOT NULL,
            Prezime TEXT NOT NULL,
            Nadimak TEXT NULL,
            SekcijaId TEXT NULL REFERENCES Sekcije (Id),
            VrstaClanstvaId TEXT NULL,
            ClanOd TEXT NULL,
            Status INTEGER NOT NULL DEFAULT 0,      -- 0 aktivan, 1 izašao
            DatumIzlaska TEXT NULL,
            RazlogIzlaska TEXT NULL,
            Ulica TEXT NULL,                        -- *
            KucniBroj TEXT NULL,                    -- *
            PostanskiBroj TEXT NULL,
            Mjesto TEXT NULL,
            MobilniTelefon TEXT NULL,               -- *
            FiksniTelefon TEXT NULL,                -- *
            Email TEXT NULL,                        -- *
            Oib TEXT NULL,                          -- *
            DatumRodjenja TEXT NULL,                -- *
            MjestoRodjenja TEXT NULL,               -- *
            BrojIskaznice TEXT NULL,                -- *
            IskaznicaVrijediDo TEXT NULL,
            IspitDatum TEXT NULL,
            IspitMjesto TEXT NULL,
            BrojDiplome TEXT NULL,                  -- *
            IzdavacDiplome TEXT NULL,
            Odlikovanja TEXT NULL,
            Skolovanja TEXT NULL,
            Napomena TEXT NULL,                     -- *
            Foto TEXT NULL,                         -- ime šifrirane datoteke u mapi foto
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IX_Clanovi_Prezime ON Clanovi (Prezime, Ime);
        """,

        // 3 – članarina (iznosi u centima). Nazivi kao u online Evidenciji: Clanarine, ClanarinaRate, Uplate.
        """
        ALTER TABLE Clanovi ADD COLUMN ClanskiBroj INTEGER NULL;
        UPDATE Clanovi SET ClanskiBroj = (SELECT COUNT(*) FROM Clanovi c2 WHERE c2.Kreirano <= Clanovi.Kreirano);
        CREATE TABLE VrsteClanstva (
            Id TEXT NOT NULL PRIMARY KEY,
            Naziv TEXT NOT NULL,
            Skrivena INTEGER NOT NULL DEFAULT 0,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        -- iznos za godinu po vrsti članstva ('' = članovi bez vrste)
        CREATE TABLE ClanarinaIznosi (
            Id TEXT NOT NULL PRIMARY KEY,
            Godina INTEGER NOT NULL,
            VrstaClanstvaId TEXT NOT NULL DEFAULT '',
            Iznos INTEGER NOT NULL,
            Rok TEXT NULL,
            BrojRata INTEGER NOT NULL DEFAULT 1,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE UNIQUE INDEX UX_ClanarinaIznosi ON ClanarinaIznosi (Godina, VrstaClanstvaId) WHERE Obrisano = 0;
        CREATE TABLE Clanarine (
            Id TEXT NOT NULL PRIMARY KEY,
            ClanId TEXT NOT NULL REFERENCES Clanovi (Id),
            Godina INTEGER NOT NULL,
            Iznos INTEGER NOT NULL,
            Napomena TEXT NULL,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE UNIQUE INDEX UX_Clanarine ON Clanarine (ClanId, Godina) WHERE Obrisano = 0;
        CREATE TABLE ClanarinaRate (
            Id TEXT NOT NULL PRIMARY KEY,
            ClanarinaId TEXT NOT NULL REFERENCES Clanarine (Id),
            RedniBroj INTEGER NOT NULL,
            Iznos INTEGER NOT NULL,
            Dospijece TEXT NOT NULL,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE Uplate (
            Id TEXT NOT NULL PRIMARY KEY,
            ClanarinaId TEXT NOT NULL REFERENCES Clanarine (Id),
            Datum TEXT NOT NULL,
            Iznos INTEGER NOT NULL,
            Nacin TEXT NOT NULL DEFAULT 'gotovina',  -- gotovina / račun
            Napomena TEXT NULL,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IX_Uplate_Clanarina ON Uplate (ClanarinaId);
        CREATE INDEX IX_Rate_Clanarina ON ClanarinaRate (ClanarinaId);
        """,

        // 4 – bodovi (radne akcije). Vrsta upisa: akcija / prijenos (staro iz prošle godine).
        """
        CREATE TABLE VrsteAkcija (
            Id TEXT NOT NULL PRIMARY KEY,
            Naziv TEXT NOT NULL,
            Bodovi REAL NOT NULL DEFAULT 0,
            Skrivena INTEGER NOT NULL DEFAULT 0,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE DodaciAkcija (
            Id TEXT NOT NULL PRIMARY KEY,
            Naziv TEXT NOT NULL,
            Bodovi REAL NOT NULL DEFAULT 0,
            Skrivena INTEGER NOT NULL DEFAULT 0,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE RadneAkcije (
            Id TEXT NOT NULL PRIMARY KEY,
            ClanId TEXT NOT NULL REFERENCES Clanovi (Id),
            Datum TEXT NOT NULL,
            Vrsta TEXT NOT NULL DEFAULT 'akcija',
            VrstaAkcijeId TEXT NULL,
            VrstaNaziv TEXT NULL,                 -- snimka naziva u trenutku upisa
            Oprema TEXT NULL,                     -- snimka dodataka: JSON [{"n":"Traktor","b":5}]
            Opis TEXT NULL,
            Bodovi REAL NOT NULL,
            Kreirano TEXT NOT NULL,
            Azurirano TEXT NOT NULL,
            Obrisano INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IX_RadneAkcije_Clan ON RadneAkcije (ClanId, Datum);
        """,
    ];

    public static int TrenutnaVerzija => Koraci.Length;

    public static void Nadogradi(Baza baza)
    {
        using var veza = baza.Otvori();
        var verzija = Convert.ToInt32(new Microsoft.Data.Sqlite.SqliteCommand("PRAGMA user_version", veza).ExecuteScalar());
        for (var i = verzija; i < Koraci.Length; i++)
        {
            using var tr = veza.BeginTransaction();
            Baza.Izvrsi(veza, tr, Koraci[i]);
            Baza.Izvrsi(veza, tr, $"PRAGMA user_version = {i + 1}");
            tr.Commit();
        }
    }
}
