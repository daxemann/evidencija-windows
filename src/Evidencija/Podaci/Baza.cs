using Microsoft.Data.Sqlite;

namespace Evidencija.Podaci;

/// <summary>
/// SQLite baza. Svaki zapis ima Id (GUID), Kreirano, Azurirano i Obrisano (brisanje = oznaka),
/// a tablica Promjene bilježi što se mijenjalo – priprema za kasnije usklađivanje s online aplikacijom.
/// </summary>
public sealed class Baza
{
    private readonly string _vezniNiz;

    public Baza(Mape mape)
    {
        _vezniNiz = new SqliteConnectionStringBuilder
        {
            DataSource = mape.Baza,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false, // datoteka se smije kopirati/zamijeniti (kopija, vraćanje)
        }.ToString();
    }

    public SqliteConnection Otvori()
    {
        var veza = new SqliteConnection(_vezniNiz);
        veza.Open();
        using var k = veza.CreateCommand();
        k.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        k.ExecuteNonQuery();
        return veza;
    }

    public static string NoviId() => Guid.NewGuid().ToString("D");

    public static string Sada() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>Upis s parametrima (nikad vrijednosti u SQL tekstu).</summary>
    public int Izvrsi(string sql, params (string Ime, object? Vrijednost)[] parametri)
    {
        using var veza = Otvori();
        return Izvrsi(veza, null, sql, parametri);
    }

    public static int Izvrsi(SqliteConnection veza, SqliteTransaction? tr, string sql, params (string Ime, object? Vrijednost)[] parametri)
    {
        using var k = Naredba(veza, tr, sql, parametri);
        return k.ExecuteNonQuery();
    }

    public object? Vrijednost(string sql, params (string Ime, object? Vrijednost)[] parametri)
    {
        using var veza = Otvori();
        using var k = Naredba(veza, null, sql, parametri);
        var v = k.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    public List<T> Redovi<T>(string sql, Func<SqliteDataReader, T> pretvori, params (string Ime, object? Vrijednost)[] parametri)
    {
        using var veza = Otvori();
        using var k = Naredba(veza, null, sql, parametri);
        using var r = k.ExecuteReader();
        var popis = new List<T>();
        while (r.Read())
        {
            popis.Add(pretvori(r));
        }
        return popis;
    }

    /// <summary>Zapis u dnevnik promjena (za kasnije usklađivanje).</summary>
    public static void BiljeziPromjenu(SqliteConnection veza, SqliteTransaction? tr, string tablica, string zapisId, string radnja) =>
        Izvrsi(veza, tr, "INSERT INTO Promjene (Tablica, ZapisId, Radnja, Vrijeme) VALUES ($t, $z, $r, $v)",
            ("$t", tablica), ("$z", zapisId), ("$r", radnja), ("$v", Sada()));

    public string? Postavka(string kljuc) =>
        Vrijednost("SELECT Vrijednost FROM Postavke WHERE Kljuc = $k", ("$k", kljuc)) as string;

    public void SpremiPostavku(string kljuc, string? vrijednost) =>
        Izvrsi("INSERT INTO Postavke (Kljuc, Vrijednost) VALUES ($k, $v) ON CONFLICT(Kljuc) DO UPDATE SET Vrijednost = excluded.Vrijednost",
            ("$k", kljuc), ("$v", vrijednost));

    private static SqliteCommand Naredba(SqliteConnection veza, SqliteTransaction? tr, string sql, (string Ime, object? Vrijednost)[] parametri)
    {
        var k = veza.CreateCommand();
        k.Transaction = tr;
        k.CommandText = sql;
        foreach (var (ime, vrijednost) in parametri)
        {
            k.Parameters.AddWithValue(ime, vrijednost ?? DBNull.Value);
        }
        return k;
    }
}
