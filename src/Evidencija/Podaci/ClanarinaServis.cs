using Microsoft.Data.Sqlite;

namespace Evidencija.Podaci;

/// <summary>
/// Članarina: iznosi po godini i vrsti članstva, zaduženja, rate i uplate.
/// Uplate se raspoređuju na rate redom (najstarija prva) – kao u online Evidenciji.
/// </summary>
public sealed class ClanarinaServis
{
    /// <summary>Razmak između rata kad se zaduženje dijeli automatski.</summary>
    public const int RazmakRataMjeseci = 3;

    private readonly Baza _baza;

    public ClanarinaServis(Baza baza) => _baza = baza;

    // ---------- Iznosi po godini ----------

    public List<ClanarinaIznos> Iznosi(int godina) => _baza.Redovi(
        "SELECT VrstaClanstvaId, Iznos, Rok, BrojRata FROM ClanarinaIznosi WHERE Godina = $g AND Obrisano = 0",
        r => new ClanarinaIznos
        {
            VrstaClanstvaId = r.GetString(0), Iznos = r.GetInt64(1),
            Rok = r.IsDBNull(2) ? null : r.GetString(2), BrojRata = r.GetInt32(3),
        }, ("$g", godina));

    /// <summary>Iznos za vrstu; ako vrsta nema svoj iznos, vrijedi iznos „bez vrste“.</summary>
    public static ClanarinaIznos? IznosZa(List<ClanarinaIznos> iznosi, string? vrstaId) =>
        iznosi.FirstOrDefault(i => i.VrstaClanstvaId == (vrstaId ?? "")) ?? iznosi.FirstOrDefault(i => i.VrstaClanstvaId == "");

    public void SpremiIznos(int godina, ClanarinaIznos i)
    {
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        var id = Skalar(veza, tr, "SELECT Id FROM ClanarinaIznosi WHERE Godina = $g AND VrstaClanstvaId = $v AND Obrisano = 0",
            ("$g", godina), ("$v", i.VrstaClanstvaId)) as string;
        var p = new (string, object?)[]
        {
            ("$id", id ?? Baza.NoviId()), ("$g", godina), ("$v", i.VrstaClanstvaId), ("$i", i.Iznos),
            ("$r", i.Rok), ("$n", Math.Clamp(i.BrojRata, 1, 12)), ("$s", Baza.Sada()),
        };
        Baza.Izvrsi(veza, tr, id == null
            ? "INSERT INTO ClanarinaIznosi (Id, Godina, VrstaClanstvaId, Iznos, Rok, BrojRata, Kreirano, Azurirano) VALUES ($id, $g, $v, $i, $r, $n, $s, $s)"
            : "UPDATE ClanarinaIznosi SET Iznos = $i, Rok = $r, BrojRata = $n, Azurirano = $s WHERE Id = $id", p);
        Baza.BiljeziPromjenu(veza, tr, "ClanarinaIznosi", (string)p[0].Item2!, id == null ? "dodano" : "promijenjeno");
        tr.Commit();
    }

    /// <summary>Iznos za vrstu se briše → za nju opet vrijedi iznos „bez vrste“.</summary>
    public void ObrisiIznos(int godina, string vrstaId)
    {
        var id = _baza.Vrijednost("SELECT Id FROM ClanarinaIznosi WHERE Godina = $g AND VrstaClanstvaId = $v AND Obrisano = 0",
            ("$g", godina), ("$v", vrstaId)) as string;
        if (id == null) return;
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        OznaciObrisano(veza, tr, "ClanarinaIznosi", "Id", id);
        tr.Commit();
    }

    /// <summary>Iznosi prošle godine kao prijedlog za novu (rok pomaknut za godinu).</summary>
    public int KopirajIznose(int izGodine, int uGodinu)
    {
        var n = 0;
        foreach (var i in Iznosi(izGodine))
        {
            if (DateOnly.TryParse(i.Rok, out var r)) i.Rok = r.AddYears(uGodinu - izGodine).ToString("yyyy-MM-dd");
            SpremiIznos(uGodinu, i);
            n++;
        }
        return n;
    }

    // ---------- Zaduženja ----------

    /// <summary>Sva zaduženja godine po članu (s ratama, uplatama i statusom).</summary>
    public Dictionary<string, Clanarina> ZaGodinu(int godina) =>
        Ucitaj("c.Godina = $g", ("$g", godina)).ToDictionary(c => c.ClanId);

    public List<Clanarina> ZaClana(string clanId) =>
        Ucitaj("c.ClanId = $c", ("$c", clanId)).OrderByDescending(c => c.Godina).ToList();

    public Clanarina? Dohvati(string clanId, int godina) =>
        Ucitaj("c.ClanId = $c AND c.Godina = $g", ("$c", clanId), ("$g", godina)).FirstOrDefault();

    /// <summary>Zaduži aktivne članove koji za godinu još nemaju zaduženje. Iznos 0 (oslobođeni) se preskače.</summary>
    public int ZaduziSve(int godina, IEnumerable<Clan> clanovi)
    {
        var iznosi = Iznosi(godina);
        var postoje = ZaGodinu(godina);
        var n = 0;
        foreach (var c in clanovi.Where(c => c.Status == Clan.Aktivan && !postoje.ContainsKey(c.Id)))
        {
            var iz = IznosZa(iznosi, c.VrstaClanstvaId);
            if (iz == null || iz.Iznos <= 0) continue;
            Zaduzi(c.Id, godina, iz.Iznos, iz.Rok == null ? [] : RasporedRata(iz.Iznos, iz.BrojRata, iz.Rok));
            n++;
        }
        return n;
    }

    public string Zaduzi(string clanId, int godina, long iznos, IReadOnlyList<(long Iznos, string Datum)> rate)
    {
        var id = Baza.NoviId();
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        Baza.Izvrsi(veza, tr, "INSERT INTO Clanarine (Id, ClanId, Godina, Iznos, Kreirano, Azurirano) VALUES ($id, $c, $g, $i, $s, $s)",
            ("$id", id), ("$c", clanId), ("$g", godina), ("$i", iznos), ("$s", Baza.Sada()));
        Baza.BiljeziPromjenu(veza, tr, "Clanarine", id, "dodano");
        UpisiRate(veza, tr, id, rate);
        tr.Commit();
        return id;
    }

    /// <summary>Novi iznos i rate (prazno = bez rata). Iznos = zbroj rata ako ih ima.</summary>
    public void Promijeni(string clanarinaId, long iznos, IReadOnlyList<(long Iznos, string Datum)> rate, string? napomena)
    {
        if (rate.Count > 0) iznos = rate.Sum(r => r.Iznos);
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        Baza.Izvrsi(veza, tr, "UPDATE Clanarine SET Iznos = $i, Napomena = $n, Azurirano = $s WHERE Id = $id",
            ("$i", iznos), ("$n", string.IsNullOrWhiteSpace(napomena) ? null : napomena.Trim()), ("$s", Baza.Sada()), ("$id", clanarinaId));
        Baza.BiljeziPromjenu(veza, tr, "Clanarine", clanarinaId, "promijenjeno");
        UpisiRate(veza, tr, clanarinaId, rate);
        tr.Commit();
    }

    /// <summary>Zaduženje se smije obrisati samo dok nema uplata.</summary>
    public bool ObrisiZaduzenje(string clanarinaId)
    {
        if (Convert.ToInt64(_baza.Vrijednost("SELECT COUNT(*) FROM Uplate WHERE ClanarinaId = $id AND Obrisano = 0", ("$id", clanarinaId))) > 0)
            return false;
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        OznaciObrisano(veza, tr, "ClanarinaRate", "ClanarinaId", clanarinaId);
        OznaciObrisano(veza, tr, "Clanarine", "Id", clanarinaId);
        tr.Commit();
        return true;
    }

    // ---------- Uplate ----------

    public string DodajUplatu(string clanarinaId, Uplata u)
    {
        var id = Baza.NoviId();
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        Baza.Izvrsi(veza, tr,
            "INSERT INTO Uplate (Id, ClanarinaId, Datum, Iznos, Nacin, Napomena, Kreirano, Azurirano) VALUES ($id, $c, $d, $i, $na, $n, $s, $s)",
            ("$id", id), ("$c", clanarinaId), ("$d", u.Datum), ("$i", u.Iznos), ("$na", u.Nacin),
            ("$n", string.IsNullOrWhiteSpace(u.Napomena) ? null : u.Napomena.Trim()), ("$s", Baza.Sada()));
        Baza.BiljeziPromjenu(veza, tr, "Uplate", id, "dodano");
        tr.Commit();
        return id;
    }

    public void ObrisiUplatu(string uplataId)
    {
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        OznaciObrisano(veza, tr, "Uplate", "Id", uplataId);
        tr.Commit();
    }

    // ---------- Izračun (bez baze – lako za testirati) ----------

    /// <summary>Jednake rate (zadnja dobije ostatak centi), prva na rok, ostale svakih 3 mjeseca.</summary>
    public static List<(long Iznos, string Datum)> RasporedRata(long iznos, int brojRata, string prviRok)
    {
        brojRata = Math.Clamp(brojRata, 1, 12);
        var prvi = DateOnly.Parse(prviRok);
        var dio = iznos / brojRata;
        return Enumerable.Range(0, brojRata)
            .Select(i => (i == brojRata - 1 ? iznos - dio * (brojRata - 1) : dio, prvi.AddMonths(i * RazmakRataMjeseci).ToString("yyyy-MM-dd")))
            .ToList();
    }

    /// <summary>Raspodjela uplata na rate i status (plaćeno / djelomično / dospjelo / otvoreno).</summary>
    public static void Izracunaj(Clanarina c, DateOnly danas)
    {
        var ostatak = c.Uplaceno;
        var danasTekst = danas.ToString("yyyy-MM-dd");
        foreach (var r in c.Rate.OrderBy(r => r.RedniBroj))
        {
            r.Placeno = Math.Min(r.Iznos, Math.Max(0, ostatak));
            ostatak -= r.Placeno;
            r.Status = r.Preostalo <= 0 ? StatusRate.Placeno
                : string.CompareOrdinal(r.Dospijece, danasTekst) < 0 ? StatusRate.Dospjelo
                : r.Placeno > 0 ? StatusRate.Djelomicno : StatusRate.NijeDospjelo;
        }
        c.Status = c.Preostalo <= 0 ? StatusClanarine.Placeno
            : c.Rate.Any(r => r.Status == StatusRate.Dospjelo) ? StatusClanarine.Dospjelo
            : c.Uplaceno > 0 ? StatusClanarine.Djelomicno : StatusClanarine.Otvoreno;
    }

    // ---------- interno ----------

    private List<Clanarina> Ucitaj(string uvjet, params (string, object?)[] p)
    {
        var popis = _baza.Redovi($"SELECT c.Id, c.ClanId, c.Godina, c.Iznos, c.Napomena FROM Clanarine c WHERE c.Obrisano = 0 AND {uvjet}",
            r => new Clanarina
            {
                Id = r.GetString(0), ClanId = r.GetString(1), Godina = r.GetInt32(2), Iznos = r.GetInt64(3),
                Napomena = r.IsDBNull(4) ? null : r.GetString(4),
            }, p);
        if (popis.Count == 0) return popis;
        var poId = popis.ToDictionary(c => c.Id);

        foreach (var (cid, rata) in _baza.Redovi(
                     $"SELECT x.ClanarinaId, x.RedniBroj, x.Iznos, x.Dospijece FROM ClanarinaRate x JOIN Clanarine c ON c.Id = x.ClanarinaId WHERE x.Obrisano = 0 AND c.Obrisano = 0 AND {uvjet} ORDER BY x.RedniBroj",
                     r => (r.GetString(0), new Rata { RedniBroj = r.GetInt32(1), Iznos = r.GetInt64(2), Dospijece = r.GetString(3) }), p))
            poId[cid].Rate.Add(rata);

        foreach (var (cid, u) in _baza.Redovi(
                     $"SELECT x.ClanarinaId, x.Id, x.Datum, x.Iznos, x.Nacin, x.Napomena FROM Uplate x JOIN Clanarine c ON c.Id = x.ClanarinaId WHERE x.Obrisano = 0 AND c.Obrisano = 0 AND {uvjet} ORDER BY x.Datum",
                     r => (r.GetString(0), new Uplata
                     {
                         Id = r.GetString(1), Datum = r.GetString(2), Iznos = r.GetInt64(3), Nacin = r.GetString(4),
                         Napomena = r.IsDBNull(5) ? null : r.GetString(5),
                     }), p))
            poId[cid].Uplate.Add(u);

        var danas = DateOnly.FromDateTime(DateTime.Today);
        foreach (var c in popis) Izracunaj(c, danas);
        return popis;
    }

    private static void UpisiRate(SqliteConnection veza, SqliteTransaction tr, string clanarinaId, IReadOnlyList<(long Iznos, string Datum)> rate)
    {
        OznaciObrisano(veza, tr, "ClanarinaRate", "ClanarinaId", clanarinaId);
        var rb = 0;
        foreach (var (iznos, datum) in rate.Where(r => r.Iznos > 0).OrderBy(r => r.Datum, StringComparer.Ordinal))
        {
            var id = Baza.NoviId();
            Baza.Izvrsi(veza, tr,
                "INSERT INTO ClanarinaRate (Id, ClanarinaId, RedniBroj, Iznos, Dospijece, Kreirano, Azurirano) VALUES ($id, $c, $rb, $i, $d, $s, $s)",
                ("$id", id), ("$c", clanarinaId), ("$rb", ++rb), ("$i", iznos), ("$d", datum), ("$s", Baza.Sada()));
            Baza.BiljeziPromjenu(veza, tr, "ClanarinaRate", id, "dodano");
        }
    }

    /// <summary>Brisanje = oznaka (zbog kasnijeg usklađivanja).</summary>
    private static void OznaciObrisano(SqliteConnection veza, SqliteTransaction tr, string tablica, string stupac, string vrijednost)
    {
        var idovi = new List<string>();
        using (var k = veza.CreateCommand())
        {
            k.Transaction = tr;
            k.CommandText = $"SELECT Id FROM {tablica} WHERE {stupac} = $v AND Obrisano = 0";
            k.Parameters.AddWithValue("$v", vrijednost);
            using var r = k.ExecuteReader();
            while (r.Read()) idovi.Add(r.GetString(0));
        }
        foreach (var id in idovi)
        {
            Baza.Izvrsi(veza, tr, $"UPDATE {tablica} SET Obrisano = 1, Azurirano = $s WHERE Id = $id", ("$s", Baza.Sada()), ("$id", id));
            Baza.BiljeziPromjenu(veza, tr, tablica, id, "obrisano");
        }
    }

    private static object? Skalar(SqliteConnection veza, SqliteTransaction tr, string sql, params (string, object?)[] p)
    {
        using var k = veza.CreateCommand();
        k.Transaction = tr;
        k.CommandText = sql;
        foreach (var (ime, v) in p) k.Parameters.AddWithValue(ime, v ?? DBNull.Value);
        return k.ExecuteScalar();
    }
}
