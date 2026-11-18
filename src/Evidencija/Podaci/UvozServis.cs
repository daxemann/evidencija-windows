using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Evidencija.Pomoc;

namespace Evidencija.Podaci;

/// <summary>Cilj stupca pri uvozu.</summary>
public sealed record PoljeUvoza(string Kljuc, string Naziv, string[] Sinonimi);

public enum Duplikat { Preskoci, Dopuni, Novi }

public sealed class RedUvoza
{
    public int Broj { get; init; }
    public Clan Clan { get; init; } = new();
    public string? Sekcija { get; set; }
    public string? Vrsta { get; set; }
    public List<string> Upozorenja { get; } = [];
    public Clan? Postojeci { get; set; }
    public bool Greska => string.IsNullOrWhiteSpace(Clan.Ime) || string.IsNullOrWhiteSpace(Clan.Prezime);
}

/// <summary>
/// Uvoz članova iz Excela (.xlsx) ili CSV-a. Datoteka se samo čita – ništa ne napušta računalo.
/// Program pogađa stupce po naslovu, korisnik potvrđuje, prije uvoza se radi sigurnosna kopija.
/// </summary>
public sealed partial class UvozServis
{
    public const string Ignoriraj = "";

    /// <summary>Ciljevi s nazivima stupaca kakvi se često vide u tablicama (bez kvačica, mala slova).</summary>
    public static readonly PoljeUvoza[] Polja =
    [
        new("Ime", "Ime", ["ime", "firstname", "name"]),
        new("Prezime", "Prezime", ["prezime", "lastname", "surname"]),
        new("PrezimeIme", "Prezime i ime (u jednom stupcu)", ["prezimeiime", "prezimeime", "clan", "lovac", "imeiprezime", "imeprezime", "punoime"]),
        new("ImePrezime", "Ime i prezime (u jednom stupcu)", []),
        new("Nadimak", "Nadimak", ["nadimak"]),
        new("Adresa", "Adresa (ulica i broj)", ["adresa", "adresastanovanja", "ulicaibroj"]),
        new("Ulica", "Ulica", ["ulica"]),
        new("KucniBroj", "Kućni broj", ["kucnibroj", "broj", "kbr"]),
        new("PostaMjesto", "Poštanski broj i mjesto", ["postanskibrojimjesto", "pbrmjesto", "postaimjesto"]),
        new("PostanskiBroj", "Poštanski broj", ["postanskibroj", "pbr", "posta", "zip"]),
        new("Mjesto", "Mjesto", ["mjesto", "grad", "naselje", "mjestostanovanja"]),
        new("MobilniTelefon", "Mobitel", ["mobitel", "mob", "gsm", "mobilni", "tel", "telefon", "kontakt"]),
        new("FiksniTelefon", "Fiksni telefon", ["fiksni", "fiksnitelefon", "telfiksni", "kucnitelefon"]),
        new("Email", "E-pošta", ["email", "mail", "epost", "eposta"]),
        new("Oib", "OIB", ["oib"]),
        new("DatumRodjenja", "Datum rođenja", ["datumrodenja", "rodjen", "roden", "datumrod", "rodenje"]),
        new("MjestoRodjenja", "Mjesto rođenja", ["mjestorodenja"]),
        new("BrojIskaznice", "Broj lovačke iskaznice", ["brojiskaznice", "iskaznica", "lovackaiskaznica", "brlovackeiskaznice"]),
        new("IskaznicaVrijediDo", "Iskaznica vrijedi do", ["iskaznicavrijedido", "vrijedido"]),
        new("IspitDatum", "Lovački ispit – datum", ["ispit", "datumispita", "lovackiispit"]),
        new("BrojDiplome", "Broj diplome", ["diploma", "brojdiplome"]),
        new("ClanOd", "Član od", ["clanod", "uclanjen", "datumuclanjenja", "clanodgodine"]),
        new("ClanskiBroj", "Članski broj", ["clanskibroj", "rb", "redbr", "rbr", "brojclana"]),
        new("Sekcija", "Sekcija", ["sekcija", "druzina", "lovnadruzina"]),
        new("Vrsta", "Vrsta članstva", ["vrstaclanstva", "kategorija", "vrsta"]),
        new("Napomena", "Napomena", ["napomena", "biljeska", "opaska"]),
    ];

    private static readonly string[] FormatiDatuma =
        ["d.M.yyyy", "d.M.yyyy.", "dd.MM.yyyy", "dd.MM.yyyy.", "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d. M. yyyy.", "d. M. yyyy", "yyyy"];

    private readonly Baza _baza;
    private readonly ClanoviServis _clanovi;
    private readonly SekcijeServis _sekcije;
    private readonly VrsteClanstvaServis _vrste;

    public UvozServis(Baza baza, ClanoviServis clanovi, SekcijeServis sekcije, VrsteClanstvaServis vrste)
    {
        _baza = baza;
        _clanovi = clanovi;
        _sekcije = sekcije;
        _vrste = vrste;
    }

    // ---------- Čitanje datoteke ----------

    public static (List<string> Zaglavlja, List<string[]> Redovi) Ucitaj(byte[] podaci, string imeDatoteke)
    {
        var redovi = imeDatoteke.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? CitajExcel(podaci) : CitajCsv(podaci);
        redovi = redovi.Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
        if (redovi.Count == 0) throw new InvalidDataException("Datoteka je prazna.");
        var zaglavlja = redovi[0].Select((z, i) => string.IsNullOrWhiteSpace(z) ? $"Stupac {i + 1}" : z.Trim()).ToList();
        var sirina = zaglavlja.Count;
        return (zaglavlja, redovi.Skip(1).Select(r => Enumerable.Range(0, sirina).Select(i => i < r.Length ? r[i].Trim() : "").ToArray()).ToList());
    }

    private static List<string[]> CitajExcel(byte[] podaci)
    {
        try
        {
            using var wb = new XLWorkbook(new MemoryStream(podaci));
            var list = wb.Worksheets.First();
            var raspon = list.RangeUsed();
            if (raspon == null) return [];
            return raspon.Rows().Select(r => r.Cells(1, raspon.ColumnCount()).Select(Celija).ToArray()).ToList();
        }
        catch (Exception e) when (e is not InvalidDataException)
        {
            throw new InvalidDataException("Excel datoteku nije moguće pročitati (je li .xlsx?).", e);
        }

        static string Celija(IXLCell c) => c.DataType switch
        {
            XLDataType.DateTime => c.GetDateTime().ToString("yyyy-MM-dd"),
            XLDataType.Number when Math.Abs(c.GetDouble() % 1) < 1e-9 => c.GetDouble().ToString("0", CultureInfo.InvariantCulture),
            XLDataType.Number => c.GetDouble().ToString(CultureInfo.GetCultureInfo("hr-HR")),
            _ => c.GetFormattedString(),
        };
    }

    /// <summary>CSV: UTF-8 ili Windows-1250 (stari Excel), razdjelnik ; , ili tab.</summary>
    private static List<string[]> CitajCsv(byte[] podaci)
    {
        string tekst;
        try { tekst = new UTF8Encoding(false, true).GetString(podaci); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            tekst = Encoding.GetEncoding(1250).GetString(podaci);
        }
        tekst = tekst.TrimStart('﻿');
        var prvi = tekst.Split('\n')[0];
        var razdjelnik = new[] { ';', ',', '\t' }.OrderByDescending(z => prvi.Count(c => c == z)).First();

        var redovi = new List<string[]>();
        var red = new List<string>();
        var polje = new StringBuilder();
        var navodnici = false;
        for (var i = 0; i < tekst.Length; i++)
        {
            var z = tekst[i];
            if (navodnici)
            {
                if (z == '"' && i + 1 < tekst.Length && tekst[i + 1] == '"') { polje.Append('"'); i++; }
                else if (z == '"') navodnici = false;
                else polje.Append(z);
            }
            else if (z == '"') navodnici = true;
            else if (z == razdjelnik) { red.Add(polje.ToString()); polje.Clear(); }
            else if (z == '\n') { red.Add(polje.ToString().TrimEnd('\r')); polje.Clear(); redovi.Add([.. red]); red.Clear(); }
            else polje.Append(z);
        }
        if (polje.Length > 0 || red.Count > 0) { red.Add(polje.ToString().TrimEnd('\r')); redovi.Add([.. red]); }
        return redovi;
    }

    // ---------- Pridruživanje stupaca ----------

    /// <summary>Prijedlog: spremljeni predložak za iste naslove, inače pogađanje po nazivu stupca.</summary>
    public string[] PredloziPridruzivanje(List<string> zaglavlja)
    {
        if (_baza.Postavka("Uvoz.Predlozak." + Potpis(zaglavlja)) is { } json &&
            JsonSerializer.Deserialize<string[]>(json) is { } spremljeno && spremljeno.Length == zaglavlja.Count)
            return spremljeno;

        var iskoristeno = new HashSet<string>();
        return zaglavlja.Select(z =>
        {
            var n = Kljuc(z);
            var p = Polja.FirstOrDefault(p => !iskoristeno.Contains(p.Kljuc) && p.Sinonimi.Contains(n))
                    ?? Polja.FirstOrDefault(p => !iskoristeno.Contains(p.Kljuc) && p.Sinonimi.Any(s => s.Length >= 4 && n.StartsWith(s)));
            if (p == null) return Ignoriraj;
            iskoristeno.Add(p.Kljuc);
            return p.Kljuc;
        }).ToArray();
    }

    public void SpremiPredlozak(List<string> zaglavlja, string[] pridruzivanje) =>
        _baza.SpremiPostavku("Uvoz.Predlozak." + Potpis(zaglavlja), JsonSerializer.Serialize(pridruzivanje));

    private static string Potpis(List<string> zaglavlja) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", zaglavlja.Select(Kljuc)))))[..16];

    private static string Kljuc(string naslov) => NeSlovaRegex().Replace(Tekst.Normaliziraj(naslov), "");

    // ---------- Pretvorba i pregled ----------

    public List<RedUvoza> Pripremi(List<string[]> redovi, string[] pridruzivanje)
    {
        var postojeci = _clanovi.Svi();
        var rezultat = new List<RedUvoza>();
        for (var i = 0; i < redovi.Count; i++)
        {
            var r = new RedUvoza { Broj = i + 2 }; // +1 zaglavlje, +1 jer Excel broji od 1
            for (var s = 0; s < pridruzivanje.Length; s++)
                if (pridruzivanje[s] != Ignoriraj && !string.IsNullOrWhiteSpace(redovi[i][s]))
                    Postavi(r, pridruzivanje[s], redovi[i][s].Trim());
            if (!string.IsNullOrWhiteSpace(r.Clan.Oib) && !Tekst.OibIspravan(r.Clan.Oib)) r.Upozorenja.Add("OIB nije ispravan");
            r.Postojeci = postojeci.FirstOrDefault(p =>
                (!string.IsNullOrEmpty(r.Clan.Oib) && p.Oib == r.Clan.Oib) ||
                (Tekst.Normaliziraj(p.PrezimeIme) == Tekst.Normaliziraj(r.Clan.PrezimeIme) && r.Clan.PrezimeIme.Length > 0));
            // isti član dvaput u datoteci
            if (r.Postojeci == null && rezultat.Any(x => Tekst.Normaliziraj(x.Clan.PrezimeIme) == Tekst.Normaliziraj(r.Clan.PrezimeIme)))
                r.Upozorenja.Add("isti naziv već je u datoteci");
            rezultat.Add(r);
        }
        return rezultat;
    }

    private static void Postavi(RedUvoza r, string cilj, string v)
    {
        var c = r.Clan;
        switch (cilj)
        {
            case "PrezimeIme" or "ImePrezime":
                var dijelovi = v.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (dijelovi.Length < 2) { c.Prezime = v; r.Upozorenja.Add("ime i prezime nisu razdvojeni"); break; }
                (c.Prezime, c.Ime) = cilj == "PrezimeIme" ? (dijelovi[0], dijelovi[1]) : (dijelovi[1], dijelovi[0]);
                break;
            case "Adresa":
                var m = AdresaRegex().Match(v);
                (c.Ulica, c.KucniBroj) = m.Success ? (m.Groups[1].Value.Trim(' ', ','), m.Groups[2].Value) : (v, null);
                break;
            case "PostaMjesto":
                var pm = PostaRegex().Match(v);
                (c.PostanskiBroj, c.Mjesto) = pm.Success ? (pm.Groups[1].Value, pm.Groups[2].Value.Trim()) : (null, v);
                break;
            case "Mjesto" when PostaRegex().Match(v) is { Success: true } mp && c.PostanskiBroj == null:
                (c.PostanskiBroj, c.Mjesto) = (mp.Groups[1].Value, mp.Groups[2].Value.Trim()); // „33000 Virovitica“
                break;
            case "MobilniTelefon": c.MobilniTelefon = Telefon(v); break;
            case "FiksniTelefon": c.FiksniTelefon = Telefon(v); break;
            case "Oib": c.Oib = new string(v.Where(char.IsAsciiDigit).ToArray()); break;
            case "ClanskiBroj":
                if (int.TryParse(v, out var br) && br > 0) c.ClanskiBroj = br; else r.Upozorenja.Add($"članski broj „{v}“ nije broj");
                break;
            case "Sekcija": r.Sekcija = v; break;
            case "Vrsta": r.Vrsta = v; break;
            case "DatumRodjenja" or "IskaznicaVrijediDo" or "IspitDatum" or "ClanOd":
                if (Datum(v) is { } d) typeof(Clan).GetProperty(cilj)!.SetValue(c, d);
                else r.Upozorenja.Add($"datum „{v}“ nije prepoznat");
                break;
            default:
                typeof(Clan).GetProperty(cilj)?.SetValue(c, v);
                break;
        }
    }

    /// <summary>Datum u yyyy-MM-dd; prepoznaje 5.3.1970., 05.03.1970, 1970-03-05, Excelov broj dana i samu godinu (→ 1.1.).</summary>
    public static string? Datum(string v)
    {
        v = v.Trim();
        if (int.TryParse(v, out var broj) && broj is > 20000 and < 80000) return DateTime.FromOADate(broj).ToString("yyyy-MM-dd");
        return DateTime.TryParseExact(v, FormatiDatuma, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d.Year is > 1900 and < 2100
            ? d.ToString("yyyy-MM-dd") : null;
    }

    /// <summary>Jednak zapis: 098 123 4567 / 033 123 456; ostalo ostaje kako je.</summary>
    public static string Telefon(string v)
    {
        var brojke = new string(v.Where(z => char.IsAsciiDigit(z) || z == '+').ToArray());
        if (brojke.StartsWith("+385")) brojke = "0" + brojke[4..];
        else if (brojke.StartsWith("00385")) brojke = "0" + brojke[5..];
        return brojke.Length is 9 or 10 && brojke[0] == '0' && !brojke.Contains('+')
            ? $"{brojke[..3]} {brojke[3..6]} {brojke[6..]}"
            : v.Trim();
    }

    // ---------- Uvoz ----------

    /// <returns>(novih, dopunjenih, preskočenih)</returns>
    public (int Novih, int Dopunjenih, int Preskocenih) Uvezi(List<RedUvoza> redovi, Duplikat duplikati)
    {
        int novih = 0, dopunjenih = 0, preskocenih = 0;
        var sekcije = _sekcije.Sve(true).ToDictionary(s => Tekst.Normaliziraj(s.Naziv), s => s.Id);
        var vrste = _vrste.Sve(true).ToDictionary(s => Tekst.Normaliziraj(s.Naziv), s => s.Id);
        string? Id(Dictionary<string, string> popis, Sifrarnik servis, string? naziv)
        {
            if (string.IsNullOrWhiteSpace(naziv)) return null;
            var k = Tekst.Normaliziraj(naziv);
            if (!popis.TryGetValue(k, out var id)) popis[k] = id = servis.Dodaj(naziv);
            return id;
        }

        foreach (var r in redovi.Where(r => !r.Greska))
        {
            r.Clan.SekcijaId = Id(sekcije, _sekcije, r.Sekcija);
            r.Clan.VrstaClanstvaId = Id(vrste, _vrste, r.Vrsta);
            if (r.Postojeci != null && duplikati == Duplikat.Preskoci) { preskocenih++; continue; }
            if (r.Postojeci != null && duplikati == Duplikat.Dopuni)
            {
                var p = _clanovi.Dohvati(r.Postojeci.Id)!;
                foreach (var prop in typeof(Clan).GetProperties().Where(x => x.CanWrite && x.Name is not ("Id" or "Status")))
                {
                    var stara = prop.GetValue(p);
                    var nova = prop.GetValue(r.Clan);
                    if ((stara == null || stara is "") && nova != null && nova is not "") prop.SetValue(p, nova);
                }
                _clanovi.Spremi(p);
                dopunjenih++;
                continue;
            }
            _clanovi.Spremi(r.Clan);
            novih++;
        }
        return (novih, dopunjenih, preskocenih);
    }

    [GeneratedRegex(@"[^a-z0-9]")] private static partial Regex NeSlovaRegex();
    [GeneratedRegex(@"^(.*?)[\s,]+(\d+\s*[a-zA-Z]?(?:\s*/\s*\d+)?)$")] private static partial Regex AdresaRegex();
    [GeneratedRegex(@"^(\d{5})\s+(.+)$")] private static partial Regex PostaRegex();
}
