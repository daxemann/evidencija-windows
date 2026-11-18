using System.Security.Cryptography;
using Evidencija.Sigurnost;
using Xunit;

namespace Evidencija.Testovi;

public sealed class SigurnostTestovi : IDisposable
{
    private readonly string _mapa = Path.Combine(Path.GetTempPath(), "ev-test-" + Guid.NewGuid().ToString("N"));
    private string Put => Path.Combine(_mapa, "kljucevi.json");

    public void Dispose()
    {
        if (Directory.Exists(_mapa)) Directory.Delete(_mapa, true);
    }

    [Fact]
    public void Polje_se_sifrira_i_desifrira()
    {
        var k = Kripto.NasumicniBajtovi(32);
        var s = Kripto.SifrirajPolje(k, "12345678903 Čakovec ž");
        Assert.StartsWith("e1:", s);
        Assert.DoesNotContain("12345678903", s);
        Assert.Equal("12345678903 Čakovec ž", Kripto.DesifrirajPolje(k, s));
        Assert.Null(Kripto.SifrirajPolje(k, ""));
    }

    [Fact]
    public void Isti_tekst_daje_razlicit_sifrat()
    {
        var k = Kripto.NasumicniBajtovi(32);
        Assert.NotEqual(Kripto.SifrirajPolje(k, "isto"), Kripto.SifrirajPolje(k, "isto"));
    }

    [Fact]
    public void Krivi_kljuc_ili_izmjena_se_otkriva()
    {
        var k = Kripto.NasumicniBajtovi(32);
        var s = Kripto.Sifriraj(k, [1, 2, 3, 4]);
        Assert.ThrowsAny<CryptographicException>(() => Kripto.Desifriraj(Kripto.NasumicniBajtovi(32), s));
        s[13] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => Kripto.Desifriraj(k, s));
    }

    [Fact]
    public void Obje_lozinke_otvaraju_isti_kljuc_kriva_ne()
    {
        var kl = new Kljucevi(Put);
        var kljuc = kl.Kreiraj("lozinka123", "rezervna-lozinka-1");
        Assert.Equal(kljuc, kl.Otkljucaj(Kljucevi.Lozinka, "lozinka123"));
        Assert.Equal(kljuc, kl.Otkljucaj(Kljucevi.Rezervna, "rezervna-lozinka-1"));
        Assert.Null(kl.Otkljucaj(Kljucevi.Lozinka, "kriva"));
        // lozinka ne otvara rezervni omot i obrnuto
        Assert.Null(kl.Otkljucaj(Kljucevi.Rezervna, "lozinka123"));
        Assert.Null(kl.Otkljucaj(Kljucevi.Lozinka, "rezervna-lozinka-1"));
    }

    [Fact]
    public void Kljuc_nije_otvoreno_u_datoteci()
    {
        var kl = new Kljucevi(Put);
        var kljuc = kl.Kreiraj("lozinka123", "rezervna-lozinka-1");
        var sadrzaj = File.ReadAllText(Put);
        Assert.DoesNotContain(Convert.ToBase64String(kljuc), sadrzaj);
        Assert.DoesNotContain("lozinka123", sadrzaj);
    }

    [Fact]
    public void Zaboravljena_lozinka_preko_rezervne()
    {
        var kl = new Kljucevi(Put);
        var s = new Sjednica(kl);
        s.Postavi("lozinka123", "rezervna-lozinka-1");
        var kljuc = s.Kljuc.ToArray();
        s.Zakljucaj();
        Assert.False(s.Otkljucano);

        Assert.True(s.Prijava(Kljucevi.Rezervna, "rezervna-lozinka-1"));
        s.PromijeniLozinku(Kljucevi.Lozinka, "nova-lozinka");
        s.Zakljucaj();

        Assert.False(s.Prijava(Kljucevi.Lozinka, "lozinka123"));
        Assert.True(s.Prijava(Kljucevi.Lozinka, "nova-lozinka"));
        Assert.Equal(kljuc, s.Kljuc);
    }

    [Fact]
    public void Previse_krivih_lozinki_koci()
    {
        var s = new Sjednica(new Kljucevi(Put));
        s.Postavi("lozinka123", "rezervna-lozinka-1");
        s.Zakljucaj();
        for (var i = 0; i < 5; i++) Assert.False(s.Prijava(Kljucevi.Lozinka, "kriva"));
        Assert.True(s.CekajSekundi > 0);
        Assert.False(s.Prijava(Kljucevi.Lozinka, "lozinka123")); // i točna mora čekati
    }

    [Fact]
    public void Pravila_lozinke()
    {
        Assert.NotNull(Kljucevi.GreskaLozinke(Kljucevi.Lozinka, "kratka", "kratka"));
        Assert.NotNull(Kljucevi.GreskaLozinke(Kljucevi.Lozinka, "dovoljno1", "drukcija1"));
        Assert.Null(Kljucevi.GreskaLozinke(Kljucevi.Lozinka, "dovoljno1", "dovoljno1"));
        Assert.NotNull(Kljucevi.GreskaLozinke(Kljucevi.Rezervna, "jedanaest11", "jedanaest11"));
    }
}
