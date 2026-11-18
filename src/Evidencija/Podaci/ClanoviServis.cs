using System.Reflection;
using Evidencija.Pomoc;
using Evidencija.Sigurnost;
using Microsoft.Data.Sqlite;

namespace Evidencija.Podaci;

/// <summary>Čitanje i spremanje članova; osjetljiva polja se šifriraju/dešifriraju ovdje i nigdje drugdje.</summary>
public sealed class ClanoviServis
{
    /// <summary>Polja koja se spremaju šifrirano (sigurnosno pravilo: adresa, kontakt, OIB, rođenje, iskaznica, napomena).</summary>
    public static readonly HashSet<string> SifriranaPolja =
    [
        nameof(Clan.Ulica), nameof(Clan.KucniBroj), nameof(Clan.MobilniTelefon), nameof(Clan.FiksniTelefon),
        nameof(Clan.Email), nameof(Clan.Oib), nameof(Clan.DatumRodjenja), nameof(Clan.MjestoRodjenja),
        nameof(Clan.BrojIskaznice), nameof(Clan.BrojDiplome), nameof(Clan.Napomena),
    ];

    private static readonly PropertyInfo[] Polja = typeof(Clan).GetProperties()
        .Where(p => p.CanWrite && p.Name != nameof(Clan.Id)).ToArray();

    private readonly Baza _baza;
    private readonly Sjednica _sjednica;

    public ClanoviServis(Baza baza, Sjednica sjednica)
    {
        _baza = baza;
        _sjednica = sjednica;
    }

    /// <summary>Svi (neobrisani) članovi, abecedno po prezimenu.</summary>
    public List<Clan> Svi()
    {
        var popis = _baza.Redovi("SELECT * FROM Clanovi WHERE Obrisano = 0", Procitaj);
        popis.Sort((a, b) => Tekst.Usporedi(a.PrezimeIme, b.PrezimeIme));
        return popis;
    }

    public Clan? Dohvati(string id) =>
        _baza.Redovi("SELECT * FROM Clanovi WHERE Id = $id AND Obrisano = 0", Procitaj, ("$id", id)).FirstOrDefault();

    /// <summary>Filtar: tekst (ime, prezime, nadimak – bez kvačica), sekcija, status (null = svi).</summary>
    public static IEnumerable<Clan> Filtriraj(IEnumerable<Clan> popis, string? upit, string? sekcijaId, int? status)
    {
        var dijelovi = Tekst.Normaliziraj(upit).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return popis.Where(c =>
            (status == null || c.Status == status) &&
            (string.IsNullOrEmpty(sekcijaId) || c.SekcijaId == sekcijaId) &&
            dijelovi.All(d => Tekst.Normaliziraj($"{c.Ime} {c.Prezime} {c.Nadimak}").Contains(d)));
    }

    /// <summary>Novi ili postojeći član. Vraća Id.</summary>
    public string Spremi(Clan c)
    {
        c.Ime = c.Ime.Trim();
        c.Prezime = c.Prezime.Trim();
        var novi = string.IsNullOrEmpty(c.Id);
        if (novi) c.Id = Baza.NoviId();
        var sada = Baza.Sada();

        var parametri = Polja.Select(p => ("$" + p.Name, Vrijednost(c, p))).ToList();
        parametri.Add(("$Id", c.Id));
        parametri.Add(("$Sada", sada));
        var sql = novi
            ? $"INSERT INTO Clanovi (Id, {string.Join(", ", Polja.Select(p => p.Name))}, Kreirano, Azurirano) " +
              $"VALUES ($Id, {string.Join(", ", Polja.Select(p => "$" + p.Name))}, $Sada, $Sada)"
            : $"UPDATE Clanovi SET {string.Join(", ", Polja.Select(p => $"{p.Name} = ${p.Name}"))}, Azurirano = $Sada " +
              "WHERE Id = $Id AND Obrisano = 0";

        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        Baza.Izvrsi(veza, tr, sql, [.. parametri]);
        Baza.BiljeziPromjenu(veza, tr, "Clanovi", c.Id, novi ? "dodano" : "promijenjeno");
        tr.Commit();
        return c.Id;
    }

    /// <summary>Brisanje = oznaka (zbog kasnijeg usklađivanja); osobni podaci se odmah brišu.</summary>
    public void Obrisi(string id)
    {
        using var veza = _baza.Otvori();
        using var tr = veza.BeginTransaction();
        var sifrirana = string.Join(", ", SifriranaPolja.Select(p => $"{p} = NULL"));
        Baza.Izvrsi(veza, tr, $"UPDATE Clanovi SET Obrisano = 1, {sifrirana}, Foto = NULL, Azurirano = $s WHERE Id = $id",
            ("$s", Baza.Sada()), ("$id", id));
        Baza.BiljeziPromjenu(veza, tr, "Clanovi", id, "obrisano");
        tr.Commit();
    }

    private object? Vrijednost(Clan c, PropertyInfo p)
    {
        var v = p.GetValue(c);
        if (v is string s)
        {
            s = s.Trim();
            if (s.Length == 0) return null;
            return SifriranaPolja.Contains(p.Name) ? Kripto.SifrirajPolje(_sjednica.Kljuc, s) : s;
        }
        return v;
    }

    private Clan Procitaj(SqliteDataReader r)
    {
        var c = new Clan { Id = r.GetString(r.GetOrdinal("Id")) };
        foreach (var p in Polja)
        {
            var i = r.GetOrdinal(p.Name);
            if (r.IsDBNull(i)) continue;
            if (p.PropertyType == typeof(int))
            {
                p.SetValue(c, r.GetInt32(i));
                continue;
            }
            var s = r.GetString(i);
            p.SetValue(c, SifriranaPolja.Contains(p.Name) ? Kripto.DesifrirajPolje(_sjednica.Kljuc, s) : s);
        }
        return c;
    }
}
