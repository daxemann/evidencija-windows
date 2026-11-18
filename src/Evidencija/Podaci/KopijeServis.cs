using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evidencija.Sigurnost;
using Microsoft.Data.Sqlite;

namespace Evidencija.Podaci;

public enum IshodKopije { Spremljeno, NemaPromjena, NijePostavljeno, MapaNedostupna, Greska }

/// <summary>
/// Sigurnosne kopije (.evk = ZIP): cijela baza šifrirana ključem podataka, zaključani ključevi (kljucevi.json)
/// i već šifrirane fotografije. Otvara se samo lozinkom ili rezervnom lozinkom – bez njih je kopija bezvrijedna.
/// </summary>
public sealed class KopijeServis
{
    public const string Nastavak = ".evk";
    public const string Prefiks = "Evidencija-kopija-";
    private static readonly byte[] DodatnoBaza = Encoding.UTF8.GetBytes("evidencija-kopija-baza");
    private static readonly byte[] SqliteZaglavlje = Encoding.ASCII.GetBytes("SQLite format 3\0");

    private readonly Mape _mape;
    private readonly Baza _baza;
    private readonly Sjednica _sjednica;
    private readonly Kljucevi _kljucevi;

    public KopijeServis(Mape mape, Baza baza, Sjednica sjednica, Kljucevi kljucevi)
    {
        _mape = mape;
        _baza = baza;
        _sjednica = sjednica;
        _kljucevi = kljucevi;
    }

    // ---------- Postavke ----------

    public string? Mapa => _baza.Postavka("Kopije.Mapa");
    public int Zadrzi => int.TryParse(_baza.Postavka("Kopije.Zadrzi"), out var n) ? Math.Clamp(n, 1, 100) : 10;
    public string? Zadnja => _baza.Postavka("Kopije.Zadnja");

    public void PostaviMapu(string? mapa, int zadrzi)
    {
        _baza.SpremiPostavku("Kopije.Mapa", string.IsNullOrWhiteSpace(mapa) ? null : mapa);
        _baza.SpremiPostavku("Kopije.Zadrzi", Math.Clamp(zadrzi, 1, 100).ToString());
    }

    // ---------- Izrada ----------

    /// <summary>Pri zatvaranju programa: kopija samo ako je mapa postavljena i ima promjena od zadnje kopije.</summary>
    public IshodKopije Automatska()
    {
        var mapa = Mapa;
        if (string.IsNullOrEmpty(mapa)) return IshodKopije.NijePostavljeno;
        if (ZadnjaPromjena() == _baza.Postavka("Kopije.ZadnjaPromjena")) return IshodKopije.NemaPromjena;
        if (!Directory.Exists(mapa)) return IshodKopije.MapaNedostupna;
        try
        {
            Napravi(mapa);
            return IshodKopije.Spremljeno;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return IshodKopije.Greska;
        }
    }

    /// <summary>Nova kopija u mapi; najstarije iznad broja „zadrži“ se brišu. Vraća putanju.</summary>
    public string Napravi(string mapa)
    {
        var put = Path.Combine(mapa, $"{Prefiks}{DateTime.Now:yyyyMMdd-HHmmss}{Nastavak}");
        ZapisiKopiju(put);
        _baza.SpremiPostavku("Kopije.Zadnja", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
        _baza.SpremiPostavku("Kopije.ZadnjaPromjena", ZadnjaPromjena());
        Pocisti(mapa, Zadrzi);
        return put;
    }

    public static List<FileInfo> Popis(string mapa) =>
        Directory.Exists(mapa)
            ? new DirectoryInfo(mapa).GetFiles(Prefiks + "*" + Nastavak).OrderByDescending(f => f.Name, StringComparer.Ordinal).ToList()
            : [];

    private void ZapisiKopiju(string put)
    {
        var privremena = put + ".novo";
        using (var zip = ZipFile.Open(privremena, ZipArchiveMode.Create))
        {
            Unos(zip, "info.json", JsonSerializer.SerializeToUtf8Bytes(new { Verzija = 1, Nastalo = DateTime.Now, Shema = Shema.TrenutnaVerzija }));
            Unos(zip, "kljucevi.json", Encoding.UTF8.GetBytes(_kljucevi.SadrzajDatoteke()));
            Unos(zip, "baza.bin", Kripto.Sifriraj(_sjednica.Kljuc, SnimkaBaze(), DodatnoBaza));
            foreach (var f in Directory.GetFiles(_mape.Foto, "*.foto"))
                zip.CreateEntryFromFile(f, "foto/" + Path.GetFileName(f), CompressionLevel.NoCompression);
        }
        File.Move(privremena, put, overwrite: true);
    }

    /// <summary>Dosljedna snimka baze (VACUUM INTO) u mapi podataka – tamo je baza ionako – pa odmah briše.</summary>
    private byte[] SnimkaBaze()
    {
        var tmp = Path.Combine(_mape.Korijen, "snimka-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var veza = _baza.Otvori())
                Baza.Izvrsi(veza, null, "VACUUM INTO $p", ("$p", tmp));
            return File.ReadAllBytes(tmp);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    private string ZadnjaPromjena() => Convert.ToString(_baza.Vrijednost("SELECT COALESCE(MAX(Id), 0) FROM Promjene")) ?? "0";

    private static void Pocisti(string mapa, int zadrzi)
    {
        foreach (var stara in Popis(mapa).Skip(zadrzi)) stara.Delete();
    }

    private static void Unos(ZipArchive zip, string ime, byte[] podaci)
    {
        using var tok = zip.CreateEntry(ime, CompressionLevel.Optimal).Open();
        tok.Write(podaci);
    }

    // ---------- Vraćanje ----------

    /// <summary>
    /// Vraća kopiju: prvo sve provjeri i dešifrira u memoriji, tek onda zamijeni podatke.
    /// Trenutno stanje se prije toga spremi u mapu podataka (prije-vracanja). Nakon vraćanja program je otključan.
    /// </summary>
    /// <returns>null = uspjeh, inače poruka greške.</returns>
    public string? Vrati(string put, string lozinka)
    {
        string kljuceviJson;
        byte[] baza;
        byte[] kljuc;
        List<(string Ime, byte[] Podaci)> foto = [];
        try
        {
            using var zip = ZipFile.OpenRead(put);
            kljuceviJson = Procitaj(zip, "kljucevi.json") is { } k ? Encoding.UTF8.GetString(k) : throw new InvalidDataException();
            var otkljucan = Kljucevi.OtkljucajIz(kljuceviJson, Kljucevi.Lozinka, lozinka)
                            ?? Kljucevi.OtkljucajIz(kljuceviJson, Kljucevi.Rezervna, lozinka);
            if (otkljucan == null) return "Lozinka nije točna za ovu kopiju (vrijedi lozinka ili rezervna lozinka iz vremena kopije).";
            kljuc = otkljucan;
            baza = Kripto.Desifriraj(kljuc, Procitaj(zip, "baza.bin") ?? throw new InvalidDataException(), DodatnoBaza);
            if (!baza.AsSpan(0, SqliteZaglavlje.Length).SequenceEqual(SqliteZaglavlje)) throw new InvalidDataException();
            foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith("foto/", StringComparison.Ordinal) && e.Name.EndsWith(".foto", StringComparison.Ordinal)))
            {
                if (e.Name.Length != 37 || !e.Name[..32].All(char.IsAsciiHexDigit)) continue; // samo naša imena, nikad putanje
                using var t = e.Open();
                using var m = new MemoryStream();
                t.CopyTo(m);
                foto.Add((e.Name, m.ToArray()));
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException or JsonException or CryptographicException or ArgumentException)
        {
            return "Datoteka nije ispravna sigurnosna kopija Evidencije.";
        }

        // trenutno stanje na stranu (ako postoji i otključano je)
        if (_sjednica.Otkljucano)
        {
            var mapa = Path.Combine(_mape.Korijen, "prije-vracanja");
            Directory.CreateDirectory(mapa);
            ZapisiKopiju(Path.Combine(mapa, $"{Prefiks}{DateTime.Now:yyyyMMdd-HHmmss}{Nastavak}"));
            Pocisti(mapa, 3);
        }

        _sjednica.Zakljucaj();
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _mape.Baza, _mape.Baza + "-wal", _mape.Baza + "-shm" })
            if (File.Exists(f)) File.Delete(f);
        File.WriteAllBytes(_mape.Baza, baza);
        _kljucevi.ZamijeniSadrzaj(kljuceviJson);
        foreach (var f in Directory.GetFiles(_mape.Foto, "*.foto")) File.Delete(f);
        foreach (var (ime, podaci) in foto) File.WriteAllBytes(Path.Combine(_mape.Foto, ime), podaci);

        Shema.Nadogradi(_baza); // starija kopija → nadogradi bazu
        _sjednica.OtvoriKljucem(kljuc);
        return null;
    }

    private static byte[]? Procitaj(ZipArchive zip, string ime)
    {
        var e = zip.GetEntry(ime);
        if (e == null) return null;
        using var t = e.Open();
        using var m = new MemoryStream();
        t.CopyTo(m);
        return m.ToArray();
    }
}
