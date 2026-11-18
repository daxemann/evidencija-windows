using System.Security.Cryptography;
using System.Text;

namespace Evidencija.Sigurnost;

/// <summary>
/// Šifriranje samo s ugrađenim .NET alatima (AES-256-GCM, PBKDF2-SHA256) – bez vlastite kriptografije.
/// </summary>
public static class Kripto
{
    public const int DuljinaKljuca = 32;   // AES-256
    public const int DuljinaNoncea = 12;
    public const int DuljinaTaga = 16;
    public const int DuljinaSoli = 16;
    public const int Iteracije = 600_000;  // PBKDF2-SHA256, preporuka OWASP
    private const string OznakaPolja = "e1:";

    /// <summary>Ključ iz lozinke (PBKDF2-SHA256).</summary>
    public static byte[] IzvediKljuc(string lozinka, byte[] sol, int iteracije = Iteracije) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(lozinka), sol, iteracije, HashAlgorithmName.SHA256, DuljinaKljuca);

    public static byte[] NasumicniBajtovi(int n) => RandomNumberGenerator.GetBytes(n);

    /// <summary>Šifrira podatke: rezultat = nonce | šifrat | tag.</summary>
    public static byte[] Sifriraj(byte[] kljuc, byte[] podaci, byte[]? dodatno = null)
    {
        var nonce = NasumicniBajtovi(DuljinaNoncea);
        var sifrat = new byte[podaci.Length];
        var tag = new byte[DuljinaTaga];
        using (var aes = new AesGcm(kljuc, DuljinaTaga))
        {
            aes.Encrypt(nonce, podaci, sifrat, tag, dodatno);
        }
        var izlaz = new byte[DuljinaNoncea + sifrat.Length + DuljinaTaga];
        nonce.CopyTo(izlaz, 0);
        sifrat.CopyTo(izlaz, DuljinaNoncea);
        tag.CopyTo(izlaz, DuljinaNoncea + sifrat.Length);
        return izlaz;
    }

    /// <summary>Dešifrira (nonce | šifrat | tag). Krivi ključ ili izmijenjeni podaci → CryptographicException.</summary>
    public static byte[] Desifriraj(byte[] kljuc, byte[] ulaz, byte[]? dodatno = null)
    {
        if (ulaz.Length < DuljinaNoncea + DuljinaTaga)
        {
            throw new CryptographicException("Podaci su prekratki.");
        }
        var nonce = ulaz.AsSpan(0, DuljinaNoncea);
        var sifrat = ulaz.AsSpan(DuljinaNoncea, ulaz.Length - DuljinaNoncea - DuljinaTaga);
        var tag = ulaz.AsSpan(ulaz.Length - DuljinaTaga);
        var izlaz = new byte[sifrat.Length];
        using var aes = new AesGcm(kljuc, DuljinaTaga);
        aes.Decrypt(nonce, sifrat, tag, izlaz, dodatno);
        return izlaz;
    }

    /// <summary>Tekstualno polje za bazu: „e1:“ + base64. Prazno ostaje null.</summary>
    public static string? SifrirajPolje(byte[] kljuc, string? tekst)
    {
        if (string.IsNullOrEmpty(tekst))
        {
            return null;
        }
        return OznakaPolja + Convert.ToBase64String(Sifriraj(kljuc, Encoding.UTF8.GetBytes(tekst)));
    }

    public static string? DesifrirajPolje(byte[] kljuc, string? vrijednost)
    {
        if (string.IsNullOrEmpty(vrijednost))
        {
            return null;
        }
        if (!vrijednost.StartsWith(OznakaPolja, StringComparison.Ordinal))
        {
            return vrijednost; // nešifrirano (ne bi se smjelo dogoditi)
        }
        var bajtovi = Desifriraj(kljuc, Convert.FromBase64String(vrijednost[OznakaPolja.Length..]));
        return Encoding.UTF8.GetString(bajtovi);
    }
}
