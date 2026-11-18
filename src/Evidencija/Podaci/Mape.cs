namespace Evidencija.Podaci;

/// <summary>Gdje program sprema podatke: %LOCALAPPDATA%\LovackaEvidencija (nikad u mapi programa).</summary>
public sealed class Mape
{
    public Mape(string? korijen = null)
    {
        Korijen = korijen ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LovackaEvidencija");
        Directory.CreateDirectory(Korijen);
        Directory.CreateDirectory(Foto);
    }

    public string Korijen { get; }
    public string Baza => Path.Combine(Korijen, "evidencija.db");
    public string Kljucevi => Path.Combine(Korijen, "kljucevi.json");
    public string Foto => Path.Combine(Korijen, "foto");
    public string WebView => Path.Combine(Korijen, "webview");
}
