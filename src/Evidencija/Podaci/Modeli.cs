namespace Evidencija.Podaci;

public sealed class Stavka
{
    public string Id { get; set; } = "";
    public string Naziv { get; set; } = "";
    public bool Skrivena { get; set; }
    /// <summary>Samo za popise s bodovima (vrste akcija, dodaci).</summary>
    public double Bodovi { get; set; }
}

/// <summary>Član – imena svojstava = imena stupaca u tablici Clanovi.</summary>
public sealed class Clan
{
    public const int Aktivan = 0;
    public const int Izasao = 1;

    public string Id { get; set; } = "";
    /// <summary>Članski broj (brojka) – za poziv na broj na uplatnici.</summary>
    public int? ClanskiBroj { get; set; }
    public string Ime { get; set; } = "";
    public string Prezime { get; set; } = "";
    public string? Nadimak { get; set; }
    public string? SekcijaId { get; set; }
    public string? VrstaClanstvaId { get; set; }
    public string? ClanOd { get; set; }
    public int Status { get; set; }
    public string? DatumIzlaska { get; set; }
    public string? RazlogIzlaska { get; set; }
    public string? Ulica { get; set; }
    public string? KucniBroj { get; set; }
    public string? PostanskiBroj { get; set; }
    public string? Mjesto { get; set; }
    public string? MobilniTelefon { get; set; }
    public string? FiksniTelefon { get; set; }
    public string? Email { get; set; }
    public string? Oib { get; set; }
    public string? DatumRodjenja { get; set; }
    public string? MjestoRodjenja { get; set; }
    public string? BrojIskaznice { get; set; }
    public string? IskaznicaVrijediDo { get; set; }
    public string? IspitDatum { get; set; }
    public string? IspitMjesto { get; set; }
    public string? BrojDiplome { get; set; }
    public string? IzdavacDiplome { get; set; }
    public string? Odlikovanja { get; set; }
    public string? Skolovanja { get; set; }
    public string? Napomena { get; set; }
    public string? Foto { get; set; }

    public string PrezimeIme => $"{Prezime} {Ime}".Trim();

    public string Inicijali =>
        $"{(Ime.Length > 0 ? Ime[0] : ' ')}{(Prezime.Length > 0 ? Prezime[0] : ' ')}".Trim().ToUpperInvariant();

    public string? Adresa =>
        string.Join(", ", new[] { $"{Ulica} {KucniBroj}".Trim(), $"{PostanskiBroj} {Mjesto}".Trim() }
            .Where(s => s.Length > 0)) is { Length: > 0 } a ? a : null;

    /// <summary>Dana do isteka lovačke iskaznice (null = nema datuma).</summary>
    public int? IskaznicaIsticeZaDana(DateOnly danas) =>
        DateOnly.TryParse(IskaznicaVrijediDo, out var d) ? d.DayNumber - danas.DayNumber : null;

    public Clan Kopija() => (Clan)MemberwiseClone();
}
