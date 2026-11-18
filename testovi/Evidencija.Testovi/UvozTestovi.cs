using System.Text;
using ClosedXML.Excel;
using Evidencija.Podaci;
using Evidencija.Sigurnost;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Evidencija.Testovi;

/// <summary>Uvoz i PDF popisi – samo izmišljeni podaci.</summary>
public sealed class UvozTestovi : IDisposable
{
    private readonly string _mapa = Path.Combine(Path.GetTempPath(), "ev-test-" + Guid.NewGuid().ToString("N"));
    private readonly Baza _baza;
    private readonly ClanoviServis _clanovi;
    private readonly SekcijeServis _sekcije;
    private readonly UvozServis _uvoz;

    public UvozTestovi()
    {
        var mape = new Mape(_mapa);
        _baza = new Baza(mape);
        Shema.Nadogradi(_baza);
        var s = new Sjednica(new Kljucevi(mape.Kljucevi));
        s.Postavi("lozinka123", "rezervna-lozinka-1");
        _clanovi = new ClanoviServis(_baza, s);
        _sekcije = new SekcijeServis(_baza);
        _uvoz = new UvozServis(_baza, _clanovi, _sekcije, new VrsteClanstvaServis(_baza));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_mapa)) Directory.Delete(_mapa, true);
    }

    [Fact]
    public void Csv_windows1250_tocka_zarez_navodnici()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var csv = "Prezime i ime;Adresa;Mjesto;Tel.;OIB;Datum rođenja;Sekcija\r\n" +
                  "Testić Ivo;\"Izmišljena ulica 7a\";33000 Testno Selo;0981234567;12345678903;5.3.1970.;Sjever\r\n" +
                  "Čavlović Marko;Šumska 1;Testno Selo;+385 91 333 3333;11111111111;1970;Jug\r\n" +
                  ";;;;;;\r\n";
        var (z, r) = UvozServis.Ucitaj(Encoding.GetEncoding(1250).GetBytes(csv), "clanovi.csv");
        Assert.Equal(7, z.Count);
        Assert.Equal(2, r.Count); // prazan red preskočen
        Assert.Equal("Čavlović Marko", r[1][0]);

        var p = _uvoz.PredloziPridruzivanje(z);
        Assert.Equal(["PrezimeIme", "Adresa", "Mjesto", "MobilniTelefon", "Oib", "DatumRodjenja", "Sekcija"], p);

        var pr = _uvoz.Pripremi(r, p);
        var ivo = pr[0].Clan;
        Assert.Equal(("Ivo", "Testić", "Izmišljena ulica", "7a"), (ivo.Ime, ivo.Prezime, ivo.Ulica, ivo.KucniBroj));
        Assert.Equal("098 123 4567", ivo.MobilniTelefon);
        Assert.Equal("1970-03-05", ivo.DatumRodjenja);
        Assert.Empty(pr[0].Upozorenja);
        Assert.Equal("091 333 3333", pr[1].Clan.MobilniTelefon);
        Assert.Contains(pr[1].Upozorenja, u => u.Contains("OIB"));
        Assert.Equal("1970-01-01", pr[1].Clan.DatumRodjenja); // sama godina
    }

    [Fact]
    public void Excel_s_datumom_i_brojem()
    {
        using var wb = new XLWorkbook();
        var l = wb.AddWorksheet("Članovi");
        l.Cell(1, 1).Value = "Ime"; l.Cell(1, 2).Value = "Prezime"; l.Cell(1, 3).Value = "Iskaznica vrijedi do"; l.Cell(1, 4).Value = "OIB"; l.Cell(1, 5).Value = "Nešto";
        l.Cell(2, 1).Value = "Ana"; l.Cell(2, 2).Value = "Primjerić"; l.Cell(2, 3).Value = new DateTime(2027, 4, 30); l.Cell(2, 4).Value = 12345678903; l.Cell(2, 5).Value = "x";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var (z, r) = UvozServis.Ucitaj(ms.ToArray(), "clanovi.xlsx");
        var p = _uvoz.PredloziPridruzivanje(z);
        Assert.Equal(["Ime", "Prezime", "IskaznicaVrijediDo", "Oib", UvozServis.Ignoriraj], p);
        var c = _uvoz.Pripremi(r, p)[0].Clan;
        Assert.Equal("2027-04-30", c.IskaznicaVrijediDo);
        Assert.Equal("12345678903", c.Oib);
    }

    [Fact]
    public void Duplikati_preskoci_dopuni_novi_i_nove_sekcije()
    {
        _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        var z = new List<string> { "Ime", "Prezime", "Mobitel", "Sekcija" };
        var r = new List<string[]> { new[] { "Ivo", "Testić", "098 111 1111", "Sjever" }, new[] { "Ana", "Primjerić", "", "Sjever" }, new[] { "", "Bezimeni", "", "" } };
        var p = _uvoz.PredloziPridruzivanje(z);

        var pr = _uvoz.Pripremi(r, p);
        Assert.NotNull(pr[0].Postojeci);
        Assert.True(pr[2].Greska);
        Assert.Equal((1, 0, 1), _uvoz.Uvezi(pr, Duplikat.Preskoci));
        Assert.Single(_sekcije.Sve()); // „Sjever“ napravljena jednom
        Assert.Null(_clanovi.Svi().Single(c => c.Ime == "Ivo").MobilniTelefon);

        pr = _uvoz.Pripremi(r, p);
        Assert.Equal((0, 2, 0), _uvoz.Uvezi(pr, Duplikat.Dopuni));
        Assert.Equal("098 111 1111", _clanovi.Svi().Single(c => c.Ime == "Ivo").MobilniTelefon);
        Assert.Equal(2, _clanovi.Svi().Count);
    }

    [Fact]
    public void Predlozak_se_pamti()
    {
        var z = new List<string> { "A", "B" };
        Assert.Equal([UvozServis.Ignoriraj, UvozServis.Ignoriraj], _uvoz.PredloziPridruzivanje(z));
        _uvoz.SpremiPredlozak(z, ["Prezime", "Ime"]);
        Assert.Equal(["Prezime", "Ime"], _uvoz.PredloziPridruzivanje(z));
    }

    [Theory]
    [InlineData("0981234567", "098 123 4567")]
    [InlineData("033/123-456", "033 123 456")]
    [InlineData("00385 98 123 4567", "098 123 4567")]
    [InlineData("nema", "nema")]
    public void Telefon(string ulaz, string izlaz) => Assert.Equal(izlaz, UvozServis.Telefon(ulaz));

    [Theory]
    [InlineData("05.03.1970", "1970-03-05")]
    [InlineData("1970-03-05", "1970-03-05")]
    [InlineData("25632", "1970-03-05")] // Excel broj dana
    [InlineData("32.1.1970", null)]
    public void Datumi(string ulaz, string? izlaz) => Assert.Equal(izlaz, UvozServis.Datum(ulaz));

    [Fact]
    public void Pdf_popis_nastaje()
    {
        var redovi = Enumerable.Range(1, 120).Select(i => new[] { $"Testić Ivo {i}", "Testno Selo", "098 000 0000" });
        var pdf = PdfPopis.Tablica("LU Primjer", "Imenik", "Aktivni članovi", [new("Ime", 3), new("Mjesto", 2), new("Mobitel", 2)], redovi, "Ukupno: 120");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
