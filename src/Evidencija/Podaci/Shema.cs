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
