using Evidencija.Pomoc;

namespace Evidencija.Podaci;

/// <summary>Sekcije su samo podatak „gdje tko pripada“ – bez prava i odvajanja.</summary>
public sealed class SekcijeServis
{
    private readonly Baza _baza;

    public SekcijeServis(Baza baza) => _baza = baza;

    public List<Sekcija> Sve(bool iSkrivene = false)
    {
        var popis = _baza.Redovi(
            "SELECT Id, Naziv, Skrivena FROM Sekcije WHERE Obrisano = 0" + (iSkrivene ? "" : " AND Skrivena = 0"),
            r => new Sekcija { Id = r.GetString(0), Naziv = r.GetString(1), Skrivena = r.GetInt32(2) == 1 });
        popis.Sort((a, b) => Tekst.Usporedi(a.Naziv, b.Naziv));
        return popis;
    }

    public string? Greska(string naziv, string? id = null)
    {
        naziv = naziv.Trim();
        if (naziv.Length == 0) return "Upišite naziv sekcije.";
        var isti = Sve(true).Any(s => s.Id != id && Tekst.Normaliziraj(s.Naziv) == Tekst.Normaliziraj(naziv));
        return isti ? "Sekcija s tim nazivom već postoji." : null;
    }

    public string Dodaj(string naziv)
    {
        var id = Baza.NoviId();
        Spremi("INSERT INTO Sekcije (Id, Naziv, Kreirano, Azurirano) VALUES ($id, $n, $s, $s)", id, "dodano", ("$n", naziv.Trim()));
        return id;
    }

    public void Preimenuj(string id, string naziv) =>
        Spremi("UPDATE Sekcije SET Naziv = $n, Azurirano = $s WHERE Id = $id", id, "promijenjeno", ("$n", naziv.Trim()));

    /// <summary>Skrivena sekcija se ne nudi za nove upise; članovi je zadržavaju.</summary>
    public void Sakrij(string id, bool skrivena) =>
        Spremi("UPDATE Sekcije SET Skrivena = $h, Azurirano = $s WHERE Id = $id", id, "promijenjeno", ("$h", skrivena ? 1 : 0));

    private void Spremi(string sql, string id, string radnja, (string, object?) dodatno)
    {
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        Baza.Izvrsi(veza, tr, sql, ("$id", id), ("$s", Baza.Sada()), dodatno);
        Baza.BiljeziPromjenu(veza, tr, "Sekcije", id, radnja);
        tr.Commit();
    }
}
