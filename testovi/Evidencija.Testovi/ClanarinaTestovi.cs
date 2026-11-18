using System.Text;
using Evidencija.Podaci;
using Evidencija.Pomoc;
using Evidencija.Sigurnost;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Evidencija.Testovi;

/// <summary>Članarina, rate, uplate, novac i uplatnice – samo izmišljeni podaci.</summary>
public sealed class ClanarinaTestovi : IDisposable
{
    private readonly string _mapa = Path.Combine(Path.GetTempPath(), "ev-test-" + Guid.NewGuid().ToString("N"));
    private readonly Baza _baza;
    private readonly ClanoviServis _clanovi;
    private readonly ClanarinaServis _cl;

    public ClanarinaTestovi()
    {
        var mape = new Mape(_mapa);
        _baza = new Baza(mape);
        Shema.Nadogradi(_baza);
        var s = new Sjednica(new Kljucevi(mape.Kljucevi));
        s.Postavi("lozinka123", "rezervna-lozinka-1");
        _clanovi = new ClanoviServis(_baza, s);
        _cl = new ClanarinaServis(_baza);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_mapa)) Directory.Delete(_mapa, true);
    }

    [Theory]
    [InlineData("320", 32000L)]
    [InlineData("320,5", 32050L)]
    [InlineData("320.50", 32050L)]
    [InlineData("1.320,00", 132000L)]
    [InlineData("1.320", 132000L)]
    [InlineData("12,345", 1235L)]
    [InlineData("abc", null)]
    [InlineData("-5", null)]
    public void Citanje_iznosa(string unos, long? centi) => Assert.Equal(centi, Novac.Procitaj(unos));

    [Fact]
    public void Prikaz_iznosa() => Assert.Equal("1.320,50 €", Novac.Prikaz(132050));

    [Fact]
    public void Raspored_rata_bez_gubitka_centa()
    {
        var r = ClanarinaServis.RasporedRata(10000, 3, "2026-03-31");
        Assert.Equal([3333L, 3333L, 3334L], r.Select(x => x.Iznos));
        Assert.Equal(["2026-03-31", "2026-06-30", "2026-09-30"], r.Select(x => x.Datum));
    }

    [Fact]
    public void Uplate_se_rasporeduju_na_rate_redom()
    {
        var c = new Clanarina
        {
            Iznos = 30000,
            Rate = [new() { RedniBroj = 1, Iznos = 15000, Dospijece = "2026-03-31" }, new() { RedniBroj = 2, Iznos = 15000, Dospijece = "2026-06-30" }],
            Uplate = [new() { Iznos = 20000 }],
        };
        ClanarinaServis.Izracunaj(c, new DateOnly(2026, 5, 1));
        Assert.Equal(StatusRate.Placeno, c.Rate[0].Status);
        Assert.Equal(StatusRate.Djelomicno, c.Rate[1].Status);
        Assert.Equal(StatusClanarine.Djelomicno, c.Status);

        ClanarinaServis.Izracunaj(c, new DateOnly(2026, 7, 1));
        Assert.Equal(StatusRate.Dospjelo, c.Rate[1].Status);
        Assert.Equal(StatusClanarine.Dospjelo, c.Status);

        c.Uplate.Add(new() { Iznos = 10000 });
        ClanarinaServis.Izracunaj(c, new DateOnly(2026, 7, 1));
        Assert.Equal(StatusClanarine.Placeno, c.Status);
    }

    [Fact]
    public void Zaduzi_sve_po_vrsti_clanstva()
    {
        var vrste = new VrsteClanstvaServis(_baza);
        var umirovljenik = vrste.Dodaj("Umirovljenik");
        var pocasni = vrste.Dodaj("Počasni");
        _cl.SpremiIznos(2026, new ClanarinaIznos { Iznos = 32000, Rok = "2026-03-31", BrojRata = 2 });
        _cl.SpremiIznos(2026, new ClanarinaIznos { VrstaClanstvaId = umirovljenik, Iznos = 20000, Rok = "2026-03-31" });
        _cl.SpremiIznos(2026, new ClanarinaIznos { VrstaClanstvaId = pocasni, Iznos = 0 });

        var a = _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        var b = _clanovi.Spremi(new Clan { Ime = "Ana", Prezime = "Primjer", VrstaClanstvaId = umirovljenik });
        var c = _clanovi.Spremi(new Clan { Ime = "Pero", Prezime = "Počasni", VrstaClanstvaId = pocasni });
        _clanovi.Spremi(new Clan { Ime = "Stari", Prezime = "Izašao", Status = Clan.Izasao });

        Assert.Equal(2, _cl.ZaduziSve(2026, _clanovi.Svi()));
        Assert.Equal(0, _cl.ZaduziSve(2026, _clanovi.Svi())); // drugi put ništa novo
        var god = _cl.ZaGodinu(2026);
        Assert.Equal(32000, god[a].Iznos);
        Assert.Equal(2, god[a].Rate.Count);
        Assert.Equal(20000, god[b].Iznos);
        Assert.False(god.ContainsKey(c));
    }

    [Fact]
    public void Uplata_brisanje_i_zastita_zaduzenja()
    {
        var a = _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        var id = _cl.Zaduzi(a, 2026, 32000, ClanarinaServis.RasporedRata(32000, 1, "2026-03-31"));
        var u = _cl.DodajUplatu(id, new Uplata { Datum = "2026-02-01", Iznos = 32000 });
        Assert.Equal(StatusClanarine.Placeno, _cl.Dohvati(a, 2026)!.Status);
        Assert.False(_cl.ObrisiZaduzenje(id)); // ima uplatu
        _cl.ObrisiUplatu(u);
        Assert.Equal(0, _cl.Dohvati(a, 2026)!.Uplaceno);
        Assert.True(_cl.ObrisiZaduzenje(id));
        Assert.Null(_cl.Dohvati(a, 2026));
    }

    [Fact]
    public void Promjena_rata_mijenja_iznos()
    {
        var a = _clanovi.Spremi(new Clan { Ime = "Ivo", Prezime = "Testić" });
        var id = _cl.Zaduzi(a, 2026, 32000, []);
        _cl.Promijeni(id, 0, [(10000, "2026-06-30"), (5000, "2026-03-31")], "dogovor");
        var c = _cl.Dohvati(a, 2026)!;
        Assert.Equal(15000, c.Iznos);
        Assert.Equal("2026-03-31", c.Rate[0].Dospijece); // po datumu
        Assert.Equal("dogovor", c.Napomena);
    }

    [Fact]
    public void Kopiranje_iznosa_u_novu_godinu()
    {
        _cl.SpremiIznos(2026, new ClanarinaIznos { Iznos = 32000, Rok = "2026-03-31", BrojRata = 2 });
        Assert.Equal(1, _cl.KopirajIznose(2026, 2027));
        var i = _cl.Iznosi(2027).Single();
        Assert.Equal("2027-03-31", i.Rok);
        Assert.Equal(2, i.BrojRata);
    }

    [Theory]
    [InlineData("HR1210010051863000160", true)]
    [InlineData("HR12 1001 0051 8630 0016 0", true)]
    [InlineData("HR1210010051863000161", false)]
    [InlineData("", false)]
    public void Provjera_ibana(string iban, bool ok) => Assert.Equal(ok, UdrugaServis.IbanIspravan(iban));

    [Fact]
    public void Poziv_na_broj()
    {
        Assert.Equal("2026-123-1", UdrugaServis.Poziv("{GODINA}-{BROJ}-{RATA}", 2026, 123, 0));
        Assert.Equal("2026-1", UdrugaServis.Poziv("{GODINA}-{BROJ}-{RATA}", 2026, null, 1));
        Assert.True(UdrugaServis.PozivIspravan("2026-123-1"));
        Assert.False(UdrugaServis.PozivIspravan("2026-ABC"));
    }

    private static Uplatnica Primjer() => new("Članarina 2026.", "Ivo Testić", "Izmišljena ulica 7", "33000 Testno Selo",
        "LU Primjer", "Šumska 1", "33000 Testno Selo", "HR1210010051863000160", 32050, "HR00", "2026-12-1", "", "Članarina 2026. - čćžšđ",
        "2026-03-31", false);

    [Fact]
    public void Hub3_sadrzaj()
    {
        var t = Uplatnice.HubPodaci(Primjer());
        var redovi = t.Split('\n');
        Assert.Equal("HRVHUB30", redovi[0]);
        Assert.Equal("EUR", redovi[1]);
        Assert.Equal("000000000032050", redovi[2]);
        Assert.Equal("HR1210010051863000160", redovi[9]);
        Assert.Equal(15, redovi.Length); // 14 polja + prazno nakon zadnjeg LF
        Assert.True(redovi[13].Length <= 35);
    }

    [Fact]
    public void Hub3_kod_se_moze_procitati()
    {
        var u = Primjer();
        var m = Uplatnice.Pdf417Matrica(Uplatnice.HubPodaci(u));
        // u sliku (4 piksela po modulu, bijeli rub) pa natrag čitačem
        const int s = 4, rub = 20;
        int w = m.Width * s + 2 * rub, h = m.Height * s + 2 * rub;
        var piks = Enumerable.Repeat((byte)255, w * h).ToArray();
        for (var y = 0; y < m.Height; y++)
        for (var x = 0; x < m.Width; x++)
            if (m[x, y])
                for (var dy = 0; dy < s; dy++)
                for (var dx = 0; dx < s; dx++)
                    piks[(rub + y * s + dy) * w + rub + x * s + dx] = 0;
        var izvor = new ZXing.PlanarYUVLuminanceSource(piks, w, h, 0, 0, w, h, false);
        var rez = new ZXing.PDF417.PDF417Reader().decode(new ZXing.BinaryBitmap(new ZXing.Common.HybridBinarizer(izvor)));
        Assert.NotNull(rez);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var procitano = Encoding.GetEncoding("ISO-8859-2").GetString(Encoding.Latin1.GetBytes(rez.Text));
        Assert.Equal(Uplatnice.HubPodaci(u), procitano);
        // 9 stupaca podataka: (9 + 4) * 17 + 1 = 222 modula širine
        Assert.Equal(222, m.Width);
    }

    [Fact]
    public void Pdf_uplatnica_nastaje()
    {
        var pdf = Uplatnice.Pdf([Primjer(), Primjer(), Primjer(), Primjer()]);
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Uplatnice_za_otvorene_rate()
    {
        var clan = new Clan { Ime = "Ivo", Prezime = "Testić", ClanskiBroj = 12 };
        var c = new Clanarina
        {
            Godina = 2026, Iznos = 30000,
            Rate = [new() { RedniBroj = 1, Iznos = 15000, Dospijece = "2026-03-31" }, new() { RedniBroj = 2, Iznos = 15000, Dospijece = "2026-06-30" }],
            Uplate = [new() { Iznos = 20000 }],
        };
        ClanarinaServis.Izracunaj(c, new DateOnly(2026, 5, 1));
        var u = Uplatnice.ZaClana(clan, c, new Udruga { Naziv = "LU Primjer", Iban = "HR1210010051863000160" });
        var jedna = Assert.Single(u); // prva rata plaćena
        Assert.Equal(10000, jedna.Iznos);
        Assert.True(jedna.Djelomicno);
        Assert.Equal("2026-12-2", jedna.Poziv);
        Assert.Equal("Članarina 2026. 2. rata", jedna.Opis);
    }
}
