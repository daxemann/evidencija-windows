using System.Globalization;
using System.Text;

namespace Evidencija.Pomoc;

public static class Tekst
{
    private static readonly CultureInfo Hr = CultureInfo.GetCultureInfo("hr-HR");

    /// <summary>Za pretragu: mala slova, bez kvačica (č→c, đ→d …).</summary>
    public static string Normaliziraj(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(t.Length);
        foreach (var c in t)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Hrvatsko abecedno sortiranje (č, ć, đ, š, ž na pravom mjestu).</summary>
    public static int Usporedi(string? a, string? b) => string.Compare(a, b, Hr, CompareOptions.IgnoreCase);

    public static string? Prazno(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>OIB: 11 znamenki + kontrolna znamenka (ISO 7064, MOD 11,10).</summary>
    public static bool OibIspravan(string? oib)
    {
        if (oib is not { Length: 11 } || !oib.All(char.IsAsciiDigit)) return false;
        var a = 10;
        for (var i = 0; i < 10; i++)
        {
            a = (a + (oib[i] - '0')) % 10;
            if (a == 0) a = 10;
            a = a * 2 % 11;
        }
        var kontrola = 11 - a == 10 ? 0 : 11 - a;
        return kontrola == oib[10] - '0';
    }

    /// <summary>Bodovi za prikaz: 12 / 12,5 / -3</summary>
    public static string Bodovi(double b) => Math.Round(b, 2).ToString("0.##", Hr);

    /// <summary>„2,5“, „2.5“, „-3“ → broj; prazno ili neispravno → null.</summary>
    public static double? ProcitajBroj(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim().Replace(" ", "").Replace(',', '.');
        return double.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d)
            ? Math.Round(d, 2) : null;
    }

    /// <summary>Datum iz baze (yyyy-MM-dd) za prikaz: 5.3.1970.</summary>
    public static string Datum(string? iso) =>
        DateOnly.TryParse(iso, CultureInfo.InvariantCulture, out var d) ? d.ToString("d.M.yyyy.", Hr) : "";
}
