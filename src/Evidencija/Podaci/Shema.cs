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
