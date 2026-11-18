namespace Evidencija.Podaci;

/// <summary>Iznos članarine za godinu i vrstu članstva ("" = članovi bez vrste).</summary>
public sealed class ClanarinaIznos
{
    public string VrstaClanstvaId { get; set; } = "";
    public long Iznos { get; set; }
    public string? Rok { get; set; }
    public int BrojRata { get; set; } = 1;
}

public sealed class Uplata
{
    public string Id { get; set; } = "";
    public string Datum { get; set; } = "";
    public long Iznos { get; set; }
    public string Nacin { get; set; } = NacinGotovina;
    public string? Napomena { get; set; }

    public const string NacinGotovina = "gotovina";
    public const string NacinRacun = "račun";
}

public enum StatusRate { Placeno, Djelomicno, Dospjelo, NijeDospjelo }

public sealed class Rata
{
    public int RedniBroj { get; set; }
    public long Iznos { get; set; }
    public string Dospijece { get; set; } = "";
    public long Placeno { get; set; }
    public long Preostalo => Iznos - Placeno;
    public StatusRate Status { get; set; }
}

public enum StatusClanarine { Placeno, Djelomicno, Dospjelo, Otvoreno, NemaZaduzenja }

/// <summary>Zaduženje člana za godinu s ratama i uplatama.</summary>
public sealed class Clanarina
{
    public string Id { get; set; } = "";
    public string ClanId { get; set; } = "";
    public int Godina { get; set; }
    public long Iznos { get; set; }
    public string? Napomena { get; set; }
    public List<Rata> Rate { get; set; } = [];
    public List<Uplata> Uplate { get; set; } = [];

    public long Uplaceno => Uplate.Sum(u => u.Iznos);
    public long Preostalo => Iznos - Uplaceno;
    public StatusClanarine Status { get; set; }
}
