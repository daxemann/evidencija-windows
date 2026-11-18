using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Evidencija.Sigurnost;

/// <summary>
/// Datoteka kljucevi.json: glavni ključ podataka spremljen je DVA puta, zaključan lozinkom i rezervnom lozinkom.
/// Sam ključ nikad ne stoji otvoreno na disku – postoji samo u memoriji dok je program otključan.
/// </summary>
public sealed class Kljucevi
{
    public const string Lozinka = "lozinka";
    public const string Rezervna = "rezervna";
    public const int MinLozinka = 8;
    public const int MinRezervna = 12;

    private readonly string _put;

    public Kljucevi(string put) => _put = put;

    public bool Postoji() => File.Exists(_put);

    /// <summary>Prvo pokretanje: novi nasumični ključ, zaključan s obje lozinke.</summary>
    public byte[] Kreiraj(string lozinka, string rezervna)
    {
        var kljuc = Kripto.NasumicniBajtovi(Kripto.DuljinaKljuca);
        var datoteka = new DatotekaKljuceva
        {
            Omoti = [Zakljucaj(kljuc, Lozinka, lozinka), Zakljucaj(kljuc, Rezervna, rezervna)],
        };
        Spremi(datoteka);
        return kljuc;
    }

    /// <summary>Otključava ključ lozinkom određene vrste; kriva lozinka → null.</summary>
    public byte[]? Otkljucaj(string vrsta, string lozinka)
    {
        var omot = Ucitaj().Omoti.FirstOrDefault(o => o.Vrsta == vrsta);
        if (omot == null)
        {
            return null;
        }
        try
        {
            var kljucLozinke = Kripto.IzvediKljuc(lozinka, Convert.FromBase64String(omot.Sol), omot.Iteracije);
            return Kripto.Desifriraj(kljucLozinke, Convert.FromBase64String(omot.Podaci), Dodatno(vrsta));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Nova lozinka (ili rezervna) za već otključan ključ.</summary>
    public void Promijeni(byte[] kljuc, string vrsta, string novaLozinka)
    {
        var datoteka = Ucitaj();
        datoteka.Omoti = [.. datoteka.Omoti.Where(o => o.Vrsta != vrsta), Zakljucaj(kljuc, vrsta, novaLozinka)];
        Spremi(datoteka);
    }

    /// <summary>Zaključani omoti (za sigurnosnu kopiju – bez otvorenog ključa).</summary>
    public string SadrzajDatoteke() => File.ReadAllText(_put);

    public void ZamijeniSadrzaj(string json)
    {
        JsonSerializer.Deserialize<DatotekaKljuceva>(json); // provjera oblika
        SpremiTekst(json);
    }

    public static string? GreskaLozinke(string vrsta, string lozinka, string ponovljena)
    {
        var min = vrsta == Rezervna ? MinRezervna : MinLozinka;
        if (lozinka.Length < min)
        {
            return $"Lozinka mora imati najmanje {min} znakova.";
        }
        if (lozinka != ponovljena)
        {
            return "Lozinke se ne podudaraju.";
        }
        return null;
    }

    private static Omot Zakljucaj(byte[] kljuc, string vrsta, string lozinka)
    {
        var sol = Kripto.NasumicniBajtovi(Kripto.DuljinaSoli);
        var kljucLozinke = Kripto.IzvediKljuc(lozinka, sol);
        return new Omot
        {
            Vrsta = vrsta,
            Sol = Convert.ToBase64String(sol),
            Iteracije = Kripto.Iteracije,
            Podaci = Convert.ToBase64String(Kripto.Sifriraj(kljucLozinke, kljuc, Dodatno(vrsta))),
        };
    }

    private static byte[] Dodatno(string vrsta) => Encoding.UTF8.GetBytes("evidencija-kljuc:" + vrsta);

    private DatotekaKljuceva Ucitaj() =>
        JsonSerializer.Deserialize<DatotekaKljuceva>(File.ReadAllText(_put)) ?? new DatotekaKljuceva();

    private void Spremi(DatotekaKljuceva d) =>
        SpremiTekst(JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Sigurno spremanje: prvo privremena datoteka, onda zamjena (nema polovične datoteke).</summary>
    private void SpremiTekst(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_put)!);
        var privremena = _put + ".novo";
        File.WriteAllText(privremena, json);
        File.Move(privremena, _put, overwrite: true);
    }

    public sealed class DatotekaKljuceva
    {
        public int Verzija { get; set; } = 1;
        public List<Omot> Omoti { get; set; } = [];
    }

    public sealed class Omot
    {
        public string Vrsta { get; set; } = "";
        public string Sol { get; set; } = "";
        public int Iteracije { get; set; }
        public string Podaci { get; set; } = "";
    }
}
