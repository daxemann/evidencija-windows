using System.Numerics;
using System.Text.RegularExpressions;

namespace Evidencija.Podaci;

/// <summary>Podaci udruge i postavke uplatnice (Postavke tablica).</summary>
public sealed class Udruga
{
    public string Naziv { get; set; } = "";
    public string Adresa { get; set; } = "";
    public string Mjesto { get; set; } = "";
    public string Oib { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Model { get; set; } = "HR00";
    public string Poziv { get; set; } = "{GODINA}-{BROJ}-{RATA}";
    public string Sifra { get; set; } = "";
    public string Opis { get; set; } = "Članarina {GODINA}. {RATA_OPIS}";
}

public sealed partial class UdrugaServis
{
    private static readonly (string Kljuc, Func<Udruga, string> Dohvati, Action<Udruga, string> Postavi)[] Polja =
    [
        ("Udruga.Naziv", u => u.Naziv, (u, v) => u.Naziv = v),
        ("Udruga.Adresa", u => u.Adresa, (u, v) => u.Adresa = v),
        ("Udruga.Mjesto", u => u.Mjesto, (u, v) => u.Mjesto = v),
        ("Udruga.Oib", u => u.Oib, (u, v) => u.Oib = v),
        ("Uplatnica.Iban", u => u.Iban, (u, v) => u.Iban = v),
        ("Uplatnica.Model", u => u.Model, (u, v) => u.Model = v),
        ("Uplatnica.Poziv", u => u.Poziv, (u, v) => u.Poziv = v),
        ("Uplatnica.Sifra", u => u.Sifra, (u, v) => u.Sifra = v),
        ("Uplatnica.Opis", u => u.Opis, (u, v) => u.Opis = v),
    ];

    private readonly Baza _baza;

    public UdrugaServis(Baza baza) => _baza = baza;

    public Udruga Ucitaj()
    {
        var u = new Udruga();
        foreach (var (kljuc, _, postavi) in Polja)
        {
            if (_baza.Postavka(kljuc) is { } v) postavi(u, v);
        }
        return u;
    }

    public void Spremi(Udruga u)
    {
        u.Iban = BezRazmaka(u.Iban).ToUpperInvariant();
        u.Model = u.Model.Trim().ToUpperInvariant();
        u.Sifra = u.Sifra.Trim().ToUpperInvariant();
        foreach (var (kljuc, dohvati, _) in Polja) _baza.SpremiPostavku(kljuc, dohvati(u).Trim());
    }

    public static List<string> Greske(Udruga u)
    {
        var g = new List<string>();
        if (string.IsNullOrWhiteSpace(u.Naziv)) g.Add("Upišite naziv udruge.");
        if (u.Iban.Length > 0 && !IbanIspravan(u.Iban)) g.Add("IBAN nije ispravan.");
        if (!ModelRegex().IsMatch(u.Model.Trim().ToUpperInvariant())) g.Add("Model mora biti oblika HR00, HR01 …");
        var primjer = Poziv(u.Poziv, 2026, 123, 1);
        if (!PozivIspravan(primjer)) g.Add($"Poziv na broj „{primjer}“ nije ispravan (samo znamenke, najviše 3 dijela s crticom, do 22 znaka).");
        return g;
    }

    public static bool UplatnicaSpremna(Udruga u) => !string.IsNullOrWhiteSpace(u.Naziv) && IbanIspravan(u.Iban);

    public static string BezRazmaka(string s) => Regex.Replace(s ?? "", @"\s+", "");

    public static string IbanRazmaci(string iban) =>
        string.Join(' ', BezRazmaka(iban).ToUpperInvariant().Chunk(4).Select(c => new string(c)));

    public static bool IbanIspravan(string iban)
    {
        var i = BezRazmaka(iban).ToUpperInvariant();
        if (!IbanRegex().IsMatch(i)) return false;
        var brojke = string.Concat((i[4..] + i[..4]).Select(z => char.IsLetter(z) ? (z - 55).ToString() : z.ToString()));
        return BigInteger.Parse(brojke) % 97 == 1;
    }

    /// <summary>{GODINA}, {BROJ}/{CLAN} (članski broj), {RATA} → npr. 2026-123-1</summary>
    public static string Poziv(string predlozak, int godina, int? clanskiBroj, int rata)
    {
        var p = predlozak.Replace("{GODINA}", godina.ToString()).Replace("{BROJ}", clanskiBroj?.ToString() ?? "")
            .Replace("{CLAN}", clanskiBroj?.ToString() ?? "").Replace("{RATA}", Math.Max(1, rata).ToString());
        return Regex.Replace(p, "-{2,}", "-").Trim('-');
    }

    public static bool PozivIspravan(string poziv) =>
        poziv.Length == 0 || (poziv.Length <= 22 && PozivRegex().IsMatch(poziv) && poziv.Split('-').All(d => d.Length <= 12));

    [GeneratedRegex(@"^[A-Z]{2}\d{2}[A-Z0-9]{10,30}$")] private static partial Regex IbanRegex();
    [GeneratedRegex(@"^HR\d{2}$")] private static partial Regex ModelRegex();
    [GeneratedRegex(@"^\d+(-\d+){0,2}$")] private static partial Regex PozivRegex();
}
