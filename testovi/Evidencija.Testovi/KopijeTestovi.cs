using System.IO.Compression;
using System.Text;
using Evidencija.Podaci;
using Evidencija.Sigurnost;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Evidencija.Testovi;

/// <summary>Sigurnosne kopije – samo izmišljeni podaci u privremenim mapama.</summary>
public sealed class KopijeTestovi : IDisposable
{
    private readonly string _mapa = Path.Combine(Path.GetTempPath(), "ev-test-" + Guid.NewGuid().ToString("N"));
    private string Podaci => Path.Combine(_mapa, "podaci");
    private string KopijeMapa => Path.Combine(_mapa, "usb");

    private Mape _mape = null!;
    private Baza _baza = null!;
    private Sjednica _sjednica = null!;
    private Kljucevi _kljucevi = null!;
    private ClanoviServis _clanovi = null!;
    private KopijeServis _kopije = null!;

    public KopijeTestovi()
    {
        Directory.CreateDirectory(KopijeMapa);
        Pokreni(Podaci);
        _sjednica.Postavi("lozinka123", "rezervna-lozinka-1");
    }

    private void Pokreni(string korijen)
    {
        _mape = new Mape(korijen);
        _baza = new Baza(_mape);
        Shema.Nadogradi(_baza);
        _kljucevi = new Kljucevi(_mape.Kljucevi);
        _sjednica = new Sjednica(_kljucevi);
        _clanovi = new ClanoviServis(_baza, _sjednica);
        _kopije = new KopijeServis(_mape, _baza, _sjednica, _kljucevi);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_mapa)) Directory.Delete(_mapa, true);
    }

    [Fact]
    public void Kopija_ne_sadrzi_citljive_podatke()
    {
        _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Jedinstvenoprezime", Oib = "12345678903" });
        var put = _kopije.Napravi(KopijeMapa);
        var bajtovi = File.ReadAllBytes(put);
        using var zip = ZipFile.OpenRead(put);
        foreach (var e in zip.Entries)
        {
            using var t = e.Open();
            using var m = new MemoryStream();
            t.CopyTo(m);
            var tekst = Encoding.UTF8.GetString(m.ToArray());
            Assert.DoesNotContain("Jedinstvenoprezime", tekst); // ni imena – cijela baza je šifrirana
            Assert.DoesNotContain("12345678903", tekst);
            Assert.DoesNotContain("lozinka123", tekst);
        }
        Assert.True(bajtovi.Length > 100);
    }

    [Fact]
    public void Vracanje_na_novom_racunalu_rezervnom_lozinkom()
    {
        var id = _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić", Oib = "12345678903" });
        var put = _kopije.Napravi(KopijeMapa);

        // „novo računalo“: prazna mapa, program nije postavljen
        SqliteConnection.ClearAllPools();
        Pokreni(Path.Combine(_mapa, "novo"));
        Assert.True(_sjednica.PrvoPokretanje);
        Assert.NotNull(_kopije.Vrati(put, "kriva-lozinka"));
        Assert.Null(_kopije.Vrati(put, "rezervna-lozinka-1"));

        Assert.True(_sjednica.Otkljucano);
        Assert.Equal("12345678903", _clanovi.Dohvati(id)!.Oib);
        _sjednica.Zakljucaj();
        Assert.True(_sjednica.Prijava(Kljucevi.Lozinka, "lozinka123")); // lozinke iz kopije vrijede
    }

    [Fact]
    public void Vracanje_preko_postojecih_podataka_cuva_stanje_prije()
    {
        var id = _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        var put = _kopije.Napravi(KopijeMapa);
        _clanovi.Spremi(new Clan { Ime = "Kasnije", Prezime = "Dodan" });
        Assert.Equal(2, _clanovi.Svi().Count);

        Assert.Null(_kopije.Vrati(put, "lozinka123"));
        Assert.Single(_clanovi.Svi());
        Assert.NotNull(_clanovi.Dohvati(id));
        Assert.Single(Directory.GetFiles(Path.Combine(Podaci, "prije-vracanja"), "*.evk"));
    }

    [Fact]
    public void Ostecena_kopija_ne_dira_podatke()
    {
        _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        var put = _kopije.Napravi(KopijeMapa);
        var b = File.ReadAllBytes(put);
        var los = Path.Combine(KopijeMapa, "los.evk");
        File.WriteAllBytes(los, b[..(b.Length / 2)]);
        Assert.NotNull(_kopije.Vrati(los, "lozinka123"));
        File.WriteAllText(los, "nije zip");
        Assert.NotNull(_kopije.Vrati(los, "lozinka123"));
        Assert.True(_sjednica.Otkljucano);
        Assert.Single(_clanovi.Svi());
    }

    [Fact]
    public void Automatska_samo_kad_ima_promjena_i_cuva_zadnjih_n()
    {
        Assert.Equal(IshodKopije.NijePostavljeno, _kopije.Automatska());
        _kopije.PostaviMapu(KopijeMapa, 2);
        _clanovi.Spremi(new Clan { Ime = "A", Prezime = "A" });
        Assert.Equal(IshodKopije.Spremljeno, _kopije.Automatska());
        Assert.Equal(IshodKopije.NemaPromjena, _kopije.Automatska());
        for (var i = 0; i < 3; i++)
        {
            Thread.Sleep(1100); // ime datoteke ima sekunde
            _clanovi.Spremi(new Clan { Ime = "B" + i, Prezime = "B" });
            Assert.Equal(IshodKopije.Spremljeno, _kopije.Automatska());
        }
        Assert.Equal(2, KopijeServis.Popis(KopijeMapa).Count);

        _kopije.PostaviMapu(Path.Combine(_mapa, "nema-usb-a"), 2);
        _clanovi.Spremi(new Clan { Ime = "C", Prezime = "C" });
        Assert.Equal(IshodKopije.MapaNedostupna, _kopije.Automatska());
    }
}
