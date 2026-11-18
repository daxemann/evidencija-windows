using System.Windows.Media;
using System.Windows.Media.Imaging;
using Evidencija.Sigurnost;

namespace Evidencija.Podaci;

/// <summary>
/// Fotografije članova: slika se uvijek ponovno iscrta (smanji na najviše 600 px, JPEG),
/// sprema se šifrirano pod nasumičnim imenom. Ništa iz izvorne datoteke (EXIF, GPS …) ne ostaje.
/// </summary>
public sealed class FotoServis
{
    public const long NajvecaDatoteka = 15 * 1024 * 1024;
    private const int NajvecaStranica = 600;

    private readonly Mape _mape;
    private readonly Sjednica _sjednica;

    public FotoServis(Mape mape, Sjednica sjednica)
    {
        _mape = mape;
        _sjednica = sjednica;
    }

    /// <summary>Sprema novu sliku i vraća ime datoteke. Neispravna slika → InvalidDataException.</summary>
    public string Spremi(byte[] izvor)
    {
        var jpeg = PonovnoIscrtaj(izvor);
        var ime = Guid.NewGuid().ToString("N") + ".foto";
        File.WriteAllBytes(Put(ime), Kripto.Sifriraj(_sjednica.Kljuc, jpeg));
        return ime;
    }

    /// <summary>Slika za prikaz (data: URL) ili null.</summary>
    public string? DataUrl(string? ime)
    {
        if (string.IsNullOrEmpty(ime) || !File.Exists(Put(ime))) return null;
        try
        {
            var jpeg = Kripto.Desifriraj(_sjednica.Kljuc, File.ReadAllBytes(Put(ime)));
            return "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public void Obrisi(string? ime)
    {
        if (!string.IsNullOrEmpty(ime) && File.Exists(Put(ime))) File.Delete(Put(ime));
    }

    /// <summary>Samo naše ime (32 heks znaka + .foto) – nikad putanja izvana.</summary>
    private string Put(string ime)
    {
        if (ime.Length != 37 || !ime.EndsWith(".foto", StringComparison.Ordinal) || !ime[..32].All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("Neispravno ime fotografije.");
        }
        return Path.Combine(_mape.Foto, ime);
    }

    internal static byte[] PonovnoIscrtaj(byte[] izvor)
    {
        BitmapSource slika;
        try
        {
            using var ulaz = new MemoryStream(izvor);
            var dekoder = BitmapDecoder.Create(ulaz, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            slika = dekoder.Frames[0];
        }
        catch (Exception e) when (e is NotSupportedException or FileFormatException or ArgumentException or IOException)
        {
            throw new InvalidDataException("Datoteka nije slika (JPG, PNG …).", e);
        }

        var omjer = Math.Min(1.0, (double)NajvecaStranica / Math.Max(slika.PixelWidth, slika.PixelHeight));
        BitmapSource izlaz = omjer < 1 ? new TransformedBitmap(slika, new ScaleTransform(omjer, omjer)) : slika;
        izlaz = new FormatConvertedBitmap(izlaz, PixelFormats.Bgr24, null, 0); // bez prozirnosti i metapodataka

        var koder = new JpegBitmapEncoder { QualityLevel = 85 };
        koder.Frames.Add(BitmapFrame.Create(izlaz));
        using var van = new MemoryStream();
        koder.Save(van);
        return van.ToArray();
    }
}
