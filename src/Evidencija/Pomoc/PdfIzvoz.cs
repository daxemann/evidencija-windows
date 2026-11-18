using System.Diagnostics;
using System.Windows;

namespace Evidencija.Pomoc;

/// <summary>PDF: korisnik bira gdje se sprema (nikad automatski u privremenu mapu), zatim se otvara.</summary>
public static class PdfIzvoz
{
    /// <returns>Putanja spremljene datoteke ili null ako je korisnik odustao.</returns>
    public static string? SpremiIOtvori(byte[] pdf, string predlozenoIme)
    {
        var dijalog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Spremi PDF",
            FileName = OcistiIme(predlozenoIme),
            DefaultExt = ".pdf",
            Filter = "PDF (*.pdf)|*.pdf",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dijalog.ShowDialog(Application.Current.MainWindow) != true) return null;
        File.WriteAllBytes(dijalog.FileName, pdf);
        Process.Start(new ProcessStartInfo(dijalog.FileName) { UseShellExecute = true });
        return dijalog.FileName;
    }

    private static string OcistiIme(string ime) =>
        string.Concat(ime.Select(z => Path.GetInvalidFileNameChars().Contains(z) ? '-' : z));
}
