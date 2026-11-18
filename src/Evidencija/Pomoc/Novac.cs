using System.Globalization;

namespace Evidencija.Pomoc;

/// <summary>Novac se u bazi čuva u centima (cijeli broj) – bez grešaka zaokruživanja.</summary>
public static class Novac
{
    private static readonly CultureInfo Hr = CultureInfo.GetCultureInfo("hr-HR");

    /// <summary>32000 → „320,00 €“</summary>
    public static string Prikaz(long centi) => (centi / 100m).ToString("N2", Hr) + " €";

    /// <summary>Za polje za unos: 32000 → „320,00“</summary>
    public static string Unos(long centi) => (centi / 100m).ToString("0.00", Hr);

    /// <summary>„320“, „320,5“, „320.50“, „1.320,00“ → centi; neispravno → null.</summary>
    public static long? Procitaj(string? tekst)
    {
        if (string.IsNullOrWhiteSpace(tekst)) return null;
        var t = tekst.Replace("€", "").Replace(" ", "").Trim();
        var zadnjiZarez = t.LastIndexOf(',');
        var zadnjaTocka = t.LastIndexOf('.');
        // decimalni znak je onaj koji je zadnji; drugi je razdjelnik tisućica
        if (zadnjiZarez > zadnjaTocka) t = t.Replace(".", "").Replace(',', '.');
        else if (zadnjiZarez < 0 && zadnjaTocka >= 0 && t.Length - zadnjaTocka == 4) t = t.Replace(".", ""); // „1.320“ = tisuću
        else t = t.Replace(",", "");
        if (!decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) || d < 0) return null;
        return (long)Math.Round(d * 100m, MidpointRounding.AwayFromZero);
    }
}
