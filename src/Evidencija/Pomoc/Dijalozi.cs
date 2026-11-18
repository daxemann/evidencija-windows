using System.Windows;

namespace Evidencija.Pomoc;

/// <summary>Windows dijalozi za izbor mape i datoteke.</summary>
public static class Dijalozi
{
    public static string? Mapa(string naslov, string? pocetna = null)
    {
        if (TestniOdgovor() is { } t) return t;
        var d = new Microsoft.Win32.OpenFolderDialog { Title = naslov };
        if (!string.IsNullOrEmpty(pocetna) && Directory.Exists(pocetna)) d.InitialDirectory = pocetna;
        return d.ShowDialog(Application.Current.MainWindow) == true ? d.FolderName : null;
    }

    public static string? Datoteka(string naslov, string filtar, string? pocetna = null)
    {
        if (TestniOdgovor() is { } t) return t;
        var d = new Microsoft.Win32.OpenFileDialog { Title = naslov, Filter = filtar, CheckFileExists = true };
        if (!string.IsNullOrEmpty(pocetna) && Directory.Exists(pocetna)) d.InitialDirectory = pocetna;
        return d.ShowDialog(Application.Current.MainWindow) == true ? d.FileName : null;
    }

    /// <summary>
    /// Samo u Debug verziji (automatsko testiranje): odgovor dijaloga iz datoteke
    /// %EVIDENCIJA_TEST_MAPA%\dijalog.txt (jednokratno). U izdanju (Release) ovaj kod ne postoji.
    /// </summary>
    internal static string? TestniOdgovor()
    {
#if DEBUG
        var mapa = Environment.GetEnvironmentVariable("EVIDENCIJA_TEST_MAPA");
        if (string.IsNullOrEmpty(mapa)) return null;
        var datoteka = Path.Combine(mapa, "dijalog.txt");
        if (!File.Exists(datoteka)) return null;
        var odgovor = File.ReadAllText(datoteka).Trim();
        File.Delete(datoteka);
        return odgovor;
#else
        return null;
#endif
    }
}
