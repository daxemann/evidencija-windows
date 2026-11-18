using Evidencija.Pomoc;

namespace Evidencija.Podaci;

/// <summary>Jednostavan popis (naziv + skriveno) – zajedničko za sekcije i vrste članstva.</summary>
public abstract class Sifrarnik
{
    private readonly Baza _baza;
    private readonly string _tablica;
    private readonly bool _imaBodove;

    protected Sifrarnik(Baza baza, string tablica, bool imaBodove = false)
    {
        _baza = baza;
        _tablica = tablica;
        _imaBodove = imaBodove;
    }

    public bool ImaBodove => _imaBodove;

    public List<Stavka> Sve(bool iSkrivene = false)
    {
        var popis = _baza.Redovi(
            $"SELECT Id, Naziv, Skrivena, {(_imaBodove ? "Bodovi" : "0")} FROM {_tablica} WHERE Obrisano = 0" + (iSkrivene ? "" : " AND Skrivena = 0"),
            r => new Stavka { Id = r.GetString(0), Naziv = r.GetString(1), Skrivena = r.GetInt32(2) == 1, Bodovi = r.GetDouble(3) });
        popis.Sort((a, b) => Tekst.Usporedi(a.Naziv, b.Naziv));
        return popis;
    }

    public Dictionary<string, string> Nazivi() => Sve(true).ToDictionary(s => s.Id, s => s.Naziv);

    public string? Greska(string naziv, string? id = null)
    {
        naziv = naziv.Trim();
        if (naziv.Length == 0) return "Upišite naziv.";
        var isti = Sve(true).Any(s => s.Id != id && Tekst.Normaliziraj(s.Naziv) == Tekst.Normaliziraj(naziv));
        return isti ? "Taj naziv već postoji." : null;
    }

    public string Dodaj(string naziv)
    {
        var id = Baza.NoviId();
        Spremi($"INSERT INTO {_tablica} (Id, Naziv, Kreirano, Azurirano) VALUES ($id, $n, $s, $s)", id, "dodano", ("$n", naziv.Trim()));
        return id;
    }

    /// <summary>Bodovi stavke (samo popisi s bodovima). Stari upisi zadržavaju svoju snimku.</summary>
    public void PostaviBodove(string id, double bodovi)
    {
        if (!_imaBodove) throw new InvalidOperationException("Ovaj popis nema bodove.");
        Spremi($"UPDATE {_tablica} SET Bodovi = $b, Azurirano = $s WHERE Id = $id", id, "promijenjeno", ("$b", Math.Round(bodovi, 2)));
    }

    public void Preimenuj(string id, string naziv) =>
        Spremi($"UPDATE {_tablica} SET Naziv = $n, Azurirano = $s WHERE Id = $id", id, "promijenjeno", ("$n", naziv.Trim()));

    /// <summary>Skrivena stavka se ne nudi za nove upise; postojeći je zadržavaju.</summary>
    public void Sakrij(string id, bool skrivena) =>
        Spremi($"UPDATE {_tablica} SET Skrivena = $h, Azurirano = $s WHERE Id = $id", id, "promijenjeno", ("$h", skrivena ? 1 : 0));

    private void Spremi(string sql, string id, string radnja, (string, object?) dodatno)
    {
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        Baza.Izvrsi(veza, tr, sql, ("$id", id), ("$s", Baza.Sada()), dodatno);
        Baza.BiljeziPromjenu(veza, tr, _tablica, id, radnja);
        tr.Commit();
    }
}

/// <summary>Sekcije su samo podatak „gdje tko pripada“ – bez prava i odvajanja.</summary>
public sealed class SekcijeServis(Baza baza) : Sifrarnik(baza, "Sekcije");

/// <summary>Vrste članstva (npr. redovni, umirovljenik, mladi) – za iznos članarine.</summary>
public sealed class VrsteClanstvaServis(Baza baza) : Sifrarnik(baza, "VrsteClanstva");

/// <summary>Vrste radnih akcija s predloženim bodovima (npr. Košnja = 5).</summary>
public sealed class VrsteAkcijaServis(Baza baza) : Sifrarnik(baza, "VrsteAkcija", imaBodove: true);

/// <summary>Dodaci uz akciju s bodovima povrh osnovnih (npr. Traktor +5, Motorna pila +2).</summary>
public sealed class DodaciAkcijaServis(Baza baza) : Sifrarnik(baza, "DodaciAkcija", imaBodove: true);
