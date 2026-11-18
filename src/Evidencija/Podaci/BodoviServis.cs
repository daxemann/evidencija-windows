using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Evidencija.Podaci;

public sealed class Dodatak
{
    public string N { get; set; } = "";   // naziv
    public double B { get; set; }         // bodovi
}

/// <summary>Jedan upis bodova (radna akcija ili prijenos iz prošle godine).</summary>
public sealed class RadnaAkcija
{
    public const string VrstaAkcija = "akcija";
    public const string VrstaPrijenos = "prijenos";

    public string Id { get; set; } = "";
    public string ClanId { get; set; } = "";
    public string Datum { get; set; } = "";
    public string Vrsta { get; set; } = VrstaAkcija;
    public string? VrstaAkcijeId { get; set; }
    public string? VrstaNaziv { get; set; }
    public List<Dodatak> Oprema { get; set; } = [];
    public string? Opis { get; set; }
    public double Bodovi { get; set; }

    public bool JePrijenos => Vrsta == VrstaPrijenos;
}

public sealed record StanjeBodova(double Staro, double Novo)
{
    public double Ukupno => Staro + Novo;
}

public enum NacinPrijenosa { Nista, Sve, Najvise, Fiksno }

public sealed class PostavkeBodova
{
    /// <summary>Početak bodovne godine „MM-dd“ (04-01 = lovna godina, 01-01 = kalendarska).</summary>
    public string Pocetak { get; set; } = "04-01";
    /// <summary>Najmanje bodova u godini (0 = bez obveze).</summary>
    public double Minimum { get; set; }
    public NacinPrijenosa Prijenos { get; set; } = NacinPrijenosa.Nista;
    public double PrijenosBroj { get; set; }
    /// <summary>Prenosi se i minus (dug).</summary>
    public bool PrijenosMinus { get; set; }
}

/// <summary>
/// Bodovi za radne akcije: upisuju se ručno. Bodovna godina počinje na dan iz postavki (zadano 1.4. – lovna godina).
/// „Staro“ = preneseno iz prošle godine, „Novo“ = sve upisano u ovoj godini – kao u online Evidenciji.
/// </summary>
public sealed partial class BodoviServis
{
    private readonly Baza _baza;

    public BodoviServis(Baza baza) => _baza = baza;

    // ---------- Postavke ----------

    public PostavkeBodova Postavke()
    {
        var p = new PostavkeBodova();
        if (_baza.Postavka("Bodovi.Pocetak") is { } poc && PocetakIspravan(poc)) p.Pocetak = poc;
        p.Minimum = Broj(_baza.Postavka("Bodovi.Minimum"));
        if (Enum.TryParse<NacinPrijenosa>(_baza.Postavka("Bodovi.Prijenos"), out var n)) p.Prijenos = n;
        p.PrijenosBroj = Broj(_baza.Postavka("Bodovi.PrijenosBroj"));
        p.PrijenosMinus = _baza.Postavka("Bodovi.PrijenosMinus") == "1";
        return p;
    }

    public void SpremiPostavke(PostavkeBodova p)
    {
        if (!PocetakIspravan(p.Pocetak)) throw new ArgumentException("Neispravan početak bodovne godine.");
        _baza.SpremiPostavku("Bodovi.Pocetak", p.Pocetak);
        _baza.SpremiPostavku("Bodovi.Minimum", Math.Max(0, p.Minimum).ToString(CultureInfo.InvariantCulture));
        _baza.SpremiPostavku("Bodovi.Prijenos", p.Prijenos.ToString());
        _baza.SpremiPostavku("Bodovi.PrijenosBroj", Math.Max(0, p.PrijenosBroj).ToString(CultureInfo.InvariantCulture));
        _baza.SpremiPostavku("Bodovi.PrijenosMinus", p.PrijenosMinus ? "1" : "0");
    }

    public static bool PocetakIspravan(string md) =>
        PocetakRegex().IsMatch(md) && DateOnly.TryParseExact($"2024-{md}", "yyyy-MM-dd", out _);

    // ---------- Bodovna godina ----------

    /// <summary>Bodovna godina (godina početka) u kojoj je datum.</summary>
    public static int Godina(DateOnly datum, string pocetak)
    {
        var p = DateOnly.ParseExact($"{datum.Year}-{pocetak}", "yyyy-MM-dd");
        return datum >= p ? datum.Year : datum.Year - 1;
    }

    public static (string Od, string Do) Raspon(int godina, string pocetak)
    {
        var od = DateOnly.ParseExact($"{godina}-{pocetak}", "yyyy-MM-dd");
        return (od.ToString("yyyy-MM-dd"), od.AddYears(1).AddDays(-1).ToString("yyyy-MM-dd"));
    }

    /// <summary>„2026.“ (od 1.1.) ili „2026./27.“</summary>
    public static string NazivGodine(int godina, string pocetak) =>
        pocetak == "01-01" ? $"{godina}." : $"{godina}./{(godina + 1) % 100:00}.";

    public int TrenutnaGodina() => Godina(DateOnly.FromDateTime(DateTime.Today), Postavke().Pocetak);

    // ---------- Upisi ----------

    public List<RadnaAkcija> ZaGodinu(int godina, string? clanId = null)
    {
        var (od, @do) = Raspon(godina, Postavke().Pocetak);
        return _baza.Redovi(
            "SELECT * FROM RadneAkcije WHERE Obrisano = 0 AND Datum BETWEEN $od AND $do" + (clanId != null ? " AND ClanId = $c" : "") +
            " ORDER BY Datum DESC, Kreirano DESC",
            Procitaj, ("$od", od), ("$do", @do), ("$c", clanId));
    }

    public RadnaAkcija? Dohvati(string id) =>
        _baza.Redovi("SELECT * FROM RadneAkcije WHERE Id = $id AND Obrisano = 0", Procitaj, ("$id", id)).FirstOrDefault();

    /// <summary>Staro / novo po članu za godinu.</summary>
    public Dictionary<string, StanjeBodova> Stanje(int godina) =>
        ZaGodinu(godina).GroupBy(a => a.ClanId).ToDictionary(g => g.Key, g => Zbroj(g));

    public static StanjeBodova Zbroj(IEnumerable<RadnaAkcija> upisi)
    {
        var popis = upisi.ToList();
        return new StanjeBodova(
            Math.Round(popis.Where(a => a.JePrijenos).Sum(a => a.Bodovi), 2),
            Math.Round(popis.Where(a => !a.JePrijenos).Sum(a => a.Bodovi), 2));
    }

    /// <summary>Osnovni bodovi vrste + zbroj dodataka (prijedlog, korisnik ga smije promijeniti).</summary>
    public static double Prijedlog(double osnovni, IEnumerable<Dodatak> dodaci) => Math.Round(osnovni + dodaci.Sum(d => d.B), 2);

    public string Spremi(RadnaAkcija a)
    {
        var novi = string.IsNullOrEmpty(a.Id);
        if (novi) a.Id = Baza.NoviId();
        var p = new (string, object?)[]
        {
            ("$id", a.Id), ("$c", a.ClanId), ("$d", a.Datum), ("$v", a.Vrsta), ("$va", a.VrstaAkcijeId), ("$vn", Prazno(a.VrstaNaziv)),
            ("$o", a.Oprema.Count > 0 ? JsonSerializer.Serialize(a.Oprema) : null), ("$op", Prazno(a.Opis)),
            ("$b", Math.Round(a.Bodovi, 2)), ("$s", Baza.Sada()),
        };
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        Baza.Izvrsi(veza, tr, novi
            ? "INSERT INTO RadneAkcije (Id, ClanId, Datum, Vrsta, VrstaAkcijeId, VrstaNaziv, Oprema, Opis, Bodovi, Kreirano, Azurirano) VALUES ($id, $c, $d, $v, $va, $vn, $o, $op, $b, $s, $s)"
            : "UPDATE RadneAkcije SET ClanId = $c, Datum = $d, Vrsta = $v, VrstaAkcijeId = $va, VrstaNaziv = $vn, Oprema = $o, Opis = $op, Bodovi = $b, Azurirano = $s WHERE Id = $id AND Obrisano = 0",
            p);
        Baza.BiljeziPromjenu(veza, tr, "RadneAkcije", a.Id, novi ? "dodano" : "promijenjeno");
        tr.Commit();
        return a.Id;
    }

    public void Obrisi(string id)
    {
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        Baza.Izvrsi(veza, tr, "UPDATE RadneAkcije SET Obrisano = 1, Azurirano = $s WHERE Id = $id", ("$s", Baza.Sada()), ("$id", id));
        Baza.BiljeziPromjenu(veza, tr, "RadneAkcije", id, "obrisano");
        tr.Commit();
    }

    // ---------- Prijenos u novu godinu ----------

    /// <summary>Prijedlog prijenosa iz ukupnih bodova prošle godine prema pravilu.</summary>
    public static double PrijedlogPrijenosa(double ukupnoProsle, PostavkeBodova p)
    {
        if (ukupnoProsle < 0) return p.PrijenosMinus ? Math.Round(ukupnoProsle, 2) : 0;
        return p.Prijenos switch
        {
            NacinPrijenosa.Sve => ukupnoProsle,
            NacinPrijenosa.Najvise => Math.Min(ukupnoProsle, Math.Max(0, p.PrijenosBroj)),
            NacinPrijenosa.Fiksno => p.PrijenosBroj > 0 && ukupnoProsle >= p.PrijenosBroj ? p.PrijenosBroj : 0,
            _ => 0,
        };
    }

    /// <summary>Upisuje (ili mijenja) prijenos člana u godinu. 0 = briše postojeći prijenos.</summary>
    public void Prenesi(string clanId, int godina, double bodovi)
    {
        var p = Postavke();
        if (!p.PrijenosMinus) bodovi = Math.Max(0, bodovi);
        var postojeci = ZaGodinu(godina, clanId).FirstOrDefault(a => a.JePrijenos);
        if (Math.Abs(bodovi) < 0.005)
        {
            if (postojeci != null) Obrisi(postojeci.Id);
            return;
        }
        var a = postojeci ?? new RadnaAkcija { ClanId = clanId, Vrsta = RadnaAkcija.VrstaPrijenos, Datum = Raspon(godina, p.Pocetak).Od };
        a.Bodovi = Math.Round(bodovi, 2);
        a.VrstaNaziv = "↪️ Preneseno iz " + NazivGodine(godina - 1, p.Pocetak);
        Spremi(a);
    }

    // ---------- interno ----------

    private static RadnaAkcija Procitaj(Microsoft.Data.Sqlite.SqliteDataReader r)
    {
        string? S(string ime) => r.IsDBNull(r.GetOrdinal(ime)) ? null : r.GetString(r.GetOrdinal(ime));
        var oprema = S("Oprema");
        return new RadnaAkcija
        {
            Id = S("Id")!, ClanId = S("ClanId")!, Datum = S("Datum")!, Vrsta = S("Vrsta")!,
            VrstaAkcijeId = S("VrstaAkcijeId"), VrstaNaziv = S("VrstaNaziv"), Opis = S("Opis"),
            Oprema = oprema == null ? [] : JsonSerializer.Deserialize<List<Dodatak>>(oprema) ?? [],
            Bodovi = r.GetDouble(r.GetOrdinal("Bodovi")),
        };
    }

    private static string? Prazno(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static double Broj(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    [GeneratedRegex(@"^(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$")] private static partial Regex PocetakRegex();
}
