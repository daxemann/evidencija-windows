using Evidencija.Podaci;
using Evidencija.Pomoc;
using Evidencija.Sigurnost;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Evidencija.Testovi;

/// <summary>Testovi samo s izmišljenim podacima u privremenoj mapi.</summary>
public sealed class ClanoviTestovi : IDisposable
{
    private readonly string _mapa = Path.Combine(Path.GetTempPath(), "ev-test-" + Guid.NewGuid().ToString("N"));
    private readonly Mape _mape;
    private readonly Baza _baza;
    private readonly Sjednica _sjednica;
    private readonly ClanoviServis _clanovi;

    public ClanoviTestovi()
    {
        _mape = new Mape(_mapa);
        _baza = new Baza(_mape);
        Shema.Nadogradi(_baza);
        _sjednica = new Sjednica(new Kljucevi(_mape.Kljucevi));
        _sjednica.Postavi("lozinka123", "rezervna-lozinka-1");
        _clanovi = new ClanoviServis(_baza, _sjednica);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_mapa)) Directory.Delete(_mapa, true);
    }

    private static Clan Izmisljen() => new()
    {
        Ime = "Ivo", Prezime = "Testić", Nadimak = "Đuro", Mjesto = "Testno Selo",
        Ulica = "Izmišljena ulica", KucniBroj = "7", MobilniTelefon = "099 000 0000",
        Oib = "12345678903", Napomena = "tajna napomena", DatumRodjenja = "1970-03-05",
    };

    [Fact]
    public void Spremanje_i_citanje()
    {
        var id = _clanovi.Spremi(Izmisljen());
        var c = _clanovi.Dohvati(id)!;
        Assert.Equal("Testić", c.Prezime);
        Assert.Equal("12345678903", c.Oib);
        Assert.Equal("tajna napomena", c.Napomena);
        Assert.Equal("1970-03-05", c.DatumRodjenja);
    }

    [Fact]
    public void Osjetljiva_polja_su_u_bazi_sifrirana()
    {
        var id = _clanovi.Spremi(Izmisljen());
        var oib = _baza.Vrijednost("SELECT Oib FROM Clanovi WHERE Id = $id", ("$id", id)) as string;
        var nap = _baza.Vrijednost("SELECT Napomena FROM Clanovi WHERE Id = $id", ("$id", id)) as string;
        Assert.StartsWith("e1:", oib);
        Assert.StartsWith("e1:", nap);
        Assert.DoesNotContain("12345678903", oib);
        // ime i mjesto namjerno nisu šifrirani (pretraga, filtar)
        Assert.Equal("Testić", _baza.Vrijednost("SELECT Prezime FROM Clanovi WHERE Id = $id", ("$id", id)));
    }

    [Fact]
    public void Datoteka_baze_ne_sadrzi_osjetljive_podatke()
    {
        _clanovi.Spremi(Izmisljen());
        SqliteConnection.ClearAllPools();
        using (var v = _baza.Otvori()) Baza.Izvrsi(v, null, "PRAGMA wal_checkpoint(TRUNCATE)");
        var sve = File.ReadAllBytes(_mape.Baza);
        var tekst = System.Text.Encoding.UTF8.GetString(sve);
        Assert.DoesNotContain("12345678903", tekst);
        Assert.DoesNotContain("tajna napomena", tekst);
        Assert.DoesNotContain("099 000 0000", tekst);
    }

    [Fact]
    public void Promjena_se_biljezi_za_uskladivanje()
    {
        var c = Izmisljen();
        var id = _clanovi.Spremi(c);
        c.Mjesto = "Drugo Selo";
        _clanovi.Spremi(c);
        var radnje = _baza.Redovi("SELECT Radnja FROM Promjene WHERE ZapisId = $id ORDER BY Id", r => r.GetString(0), ("$id", id));
        Assert.Equal(["dodano", "promijenjeno"], radnje);
        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public void Brisanje_je_oznaka_i_brise_osobne_podatke()
    {
        var id = _clanovi.Spremi(Izmisljen());
        _clanovi.Obrisi(id);
        Assert.Null(_clanovi.Dohvati(id));
        Assert.Empty(_clanovi.Svi());
        Assert.Equal(1L, _baza.Vrijednost("SELECT Obrisano FROM Clanovi WHERE Id = $id", ("$id", id)));
        Assert.Null(_baza.Vrijednost("SELECT Oib FROM Clanovi WHERE Id = $id", ("$id", id)));
    }

    [Fact]
    public void Pretraga_bez_kvacica_i_po_nadimku()
    {
        _clanovi.Spremi(Izmisljen());
        _clanovi.Spremi(new Clan { Ime = "Ana", Prezime = "Primjer", Status = Clan.Izasao });
        var svi = _clanovi.Svi();
        Assert.Single(ClanoviServis.Filtriraj(svi, "testic", null, null));
        Assert.Single(ClanoviServis.Filtriraj(svi, "duro", null, null));
        Assert.Single(ClanoviServis.Filtriraj(svi, "ivo test", null, null));
        Assert.Single(ClanoviServis.Filtriraj(svi, "", null, Clan.Aktivan));
        Assert.Equal(2, ClanoviServis.Filtriraj(svi, null, null, null).Count());
    }

    [Fact]
    public void Filtar_po_sekciji()
    {
        var sekcije = new SekcijeServis(_baza);
        var a = sekcije.Dodaj("Sekcija A");
        sekcije.Dodaj("Sekcija B");
        var c = Izmisljen();
        c.SekcijaId = a;
        _clanovi.Spremi(c);
        _clanovi.Spremi(new Clan { Ime = "Ana", Prezime = "Primjer" });
        Assert.Single(ClanoviServis.Filtriraj(_clanovi.Svi(), null, a, null));
        Assert.NotNull(sekcije.Greska("sekcija a")); // isti naziv
    }

    [Fact]
    public void Abecedno_hrvatski()
    {
        foreach (var p in new[] { "Čavić", "Cvitan", "Ćosić", "Babić" })
            _clanovi.Spremi(new Clan { Ime = "X", Prezime = p });
        Assert.Equal(["Babić", "Cvitan", "Čavić", "Ćosić"], _clanovi.Svi().Select(c => c.Prezime));
    }

    [Theory]
    [InlineData("12345678903", true)]
    [InlineData("12345678901", false)]
    [InlineData("1234567890", false)]
    [InlineData("1234567890a", false)]
    public void Provjera_oiba(string oib, bool ispravan) => Assert.Equal(ispravan, Tekst.OibIspravan(oib));

    [Fact]
    public void Fotografija_se_iscrtava_i_sifrira()
    {
        // PNG 2000x1000 napravljen u testu
        var bmp = System.Windows.Media.Imaging.BitmapSource.Create(2000, 1000, 96, 96,
            System.Windows.Media.PixelFormats.Bgr24, null, new byte[2000 * 1000 * 3], 2000 * 3);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);

        var foto = new FotoServis(_mape, _sjednica);
        var ime = foto.Spremi(ms.ToArray());
        var naDisku = File.ReadAllBytes(Path.Combine(_mape.Foto, ime));
        Assert.False(naDisku[0] == 0xFF && naDisku[1] == 0xD8); // nije čisti JPEG
        var url = foto.DataUrl(ime)!;
        var jpeg = Convert.FromBase64String(url["data:image/jpeg;base64,".Length..]);
        var dek = System.Windows.Media.Imaging.BitmapDecoder.Create(new MemoryStream(jpeg),
            System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        Assert.Equal(600, dek.Frames[0].PixelWidth);
        Assert.Throws<InvalidDataException>(() => foto.Spremi([1, 2, 3]));
        Assert.Throws<ArgumentException>(() => foto.DataUrl("..\\..\\kljucevi.json"));
    }
}
