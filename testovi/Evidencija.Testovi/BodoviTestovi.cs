using Evidencija.Podaci;
using Evidencija.Pomoc;
using Evidencija.Sigurnost;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Evidencija.Testovi;

/// <summary>Bodovi – samo izmišljeni podaci.</summary>
public sealed class BodoviTestovi : IDisposable
{
    private readonly string _mapa = Path.Combine(Path.GetTempPath(), "ev-test-" + Guid.NewGuid().ToString("N"));
    private readonly Baza _baza;
    private readonly ClanoviServis _clanovi;
    private readonly BodoviServis _bodovi;

    public BodoviTestovi()
    {
        var mape = new Mape(_mapa);
        _baza = new Baza(mape);
        Shema.Nadogradi(_baza);
        var s = new Sjednica(new Kljucevi(mape.Kljucevi));
        s.Postavi("lozinka123", "rezervna-lozinka-1");
        _clanovi = new ClanoviServis(_baza, s);
        _bodovi = new BodoviServis(_baza);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_mapa)) Directory.Delete(_mapa, true);
    }

    [Theory]
    [InlineData("2026-03-31", "04-01", 2025)]
    [InlineData("2026-04-01", "04-01", 2026)]
    [InlineData("2026-12-31", "01-01", 2026)]
    public void Bodovna_godina(string datum, string pocetak, int godina) =>
        Assert.Equal(godina, BodoviServis.Godina(DateOnly.Parse(datum), pocetak));

    [Fact]
    public void Raspon_i_naziv()
    {
        Assert.Equal(("2026-04-01", "2027-03-31"), BodoviServis.Raspon(2026, "04-01"));
        Assert.Equal("2026./27.", BodoviServis.NazivGodine(2026, "04-01"));
        Assert.Equal("2026.", BodoviServis.NazivGodine(2026, "01-01"));
        Assert.False(BodoviServis.PocetakIspravan("02-30"));
        Assert.True(BodoviServis.PocetakIspravan("09-15"));
    }

    [Fact]
    public void Prijedlog_s_dodacima() =>
        Assert.Equal(12, BodoviServis.Prijedlog(5, [new Dodatak { N = "Traktor", B = 5 }, new Dodatak { N = "Motorna pila", B = 2 }]));

    [Theory]
    [InlineData(NacinPrijenosa.Nista, 0, false, 80, 0)]
    [InlineData(NacinPrijenosa.Sve, 0, false, 80, 80)]
    [InlineData(NacinPrijenosa.Najvise, 50, false, 80, 50)]
    [InlineData(NacinPrijenosa.Najvise, 50, false, 30, 30)]
    [InlineData(NacinPrijenosa.Fiksno, 50, false, 80, 50)]
    [InlineData(NacinPrijenosa.Fiksno, 50, false, 30, 0)]
    [InlineData(NacinPrijenosa.Sve, 0, false, -10, 0)]
    [InlineData(NacinPrijenosa.Sve, 0, true, -10, -10)]
    public void Pravila_prijenosa(NacinPrijenosa n, double broj, bool minus, double ukupno, double ocekivano) =>
        Assert.Equal(ocekivano, BodoviServis.PrijedlogPrijenosa(ukupno, new PostavkeBodova { Prijenos = n, PrijenosBroj = broj, PrijenosMinus = minus }));

    [Fact]
    public void Upis_snimka_dodataka_i_stanje()
    {
        var c = _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        _bodovi.Spremi(new RadnaAkcija { ClanId = c, Datum = "2026-05-10", VrstaNaziv = "Košnja", Bodovi = 12,
            Oprema = [new Dodatak { N = "Traktor", B = 5 }, new Dodatak { N = "Motorna pila", B = 2 }] });
        _bodovi.Spremi(new RadnaAkcija { ClanId = c, Datum = "2026-06-01", VrstaNaziv = "Kazna", Bodovi = -3 });
        _bodovi.Spremi(new RadnaAkcija { ClanId = c, Datum = "2026-03-01", Bodovi = 100 }); // prošla bodovna godina
        var g = _bodovi.ZaGodinu(2026);
        Assert.Equal(2, g.Count);
        Assert.Equal(2, g.Single(a => a.VrstaNaziv == "Košnja").Oprema.Count);
        Assert.Equal(new StanjeBodova(0, 9), _bodovi.Stanje(2026)[c]);
        Assert.Equal(100, _bodovi.Stanje(2025)[c].Ukupno);
    }

    [Fact]
    public void Prijenos_je_staro_i_moze_se_promijeniti()
    {
        var c = _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        _bodovi.Spremi(new RadnaAkcija { ClanId = c, Datum = "2026-05-10", Bodovi = 20 });
        _bodovi.Prenesi(c, 2026, 50);
        Assert.Equal(new StanjeBodova(50, 20), _bodovi.Stanje(2026)[c]);
        _bodovi.Prenesi(c, 2026, 30); // promjena, ne novi upis
        Assert.Single(_bodovi.ZaGodinu(2026, c), a => a.JePrijenos);
        Assert.Equal(30, _bodovi.Stanje(2026)[c].Staro);
        _bodovi.Prenesi(c, 2026, 0);
        Assert.DoesNotContain(_bodovi.ZaGodinu(2026, c), a => a.JePrijenos);
        _bodovi.Prenesi(c, 2026, -5); // minus nije dopušten → 0
        Assert.DoesNotContain(_bodovi.ZaGodinu(2026, c), a => a.JePrijenos);
    }

    [Fact]
    public void Postavke_i_popisi_s_bodovima()
    {
        _bodovi.SpremiPostavke(new PostavkeBodova { Pocetak = "01-01", Minimum = 20, Prijenos = NacinPrijenosa.Najvise, PrijenosBroj = 50 });
        var p = _bodovi.Postavke();
        Assert.Equal(("01-01", 20.0, NacinPrijenosa.Najvise, 50.0), (p.Pocetak, p.Minimum, p.Prijenos, p.PrijenosBroj));

        var vrste = new VrsteAkcijaServis(_baza);
        var id = vrste.Dodaj("Košnja");
        vrste.PostaviBodove(id, 5);
        Assert.Equal(5, vrste.Sve().Single().Bodovi);
        Assert.Throws<InvalidOperationException>(() => new SekcijeServis(_baza).PostaviBodove(id, 1));
    }

    [Fact]
    public void Brisanje_upisa()
    {
        var c = _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        var id = _bodovi.Spremi(new RadnaAkcija { ClanId = c, Datum = "2026-05-10", Bodovi = 5 });
        _bodovi.Obrisi(id);
        Assert.Null(_bodovi.Dohvati(id));
        Assert.Empty(_bodovi.ZaGodinu(2026));
    }

    [Theory]
    [InlineData("2,5", 2.5)]
    [InlineData("-3", -3.0)]
    [InlineData("10.25", 10.25)]
    [InlineData("x", null)]
    public void Citanje_bodova(string s, double? b) => Assert.Equal(b, Tekst.ProcitajBroj(s));
}
