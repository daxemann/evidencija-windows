using System.Windows;
using Evidencija.Sigurnost;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;

namespace Evidencija;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Preglednik.Services = App.Usluge;
        // hrvatski i kad je Windows na drugom jeziku (datumi, provjera pravopisa)
        Preglednik.BlazorWebViewInitializing += (_, e) =>
            e.EnvironmentOptions = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions { Language = "hr-HR" };
        // Program je bez interneta: vanjske poveznice se ne otvaraju
        Preglednik.UrlLoading += (_, e) =>
        {
            if (e.UrlLoadingStrategy != UrlLoadingStrategy.OpenInWebView)
            {
                e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
            }
        };
        Closing += (_, _) => KopijaPriZatvaranju();
        Closed += (_, _) => App.Usluge.GetRequiredService<Sjednica>().Zakljucaj();
    }

    /// <summary>Pri zatvaranju šifrirana kopija (ako je mapa postavljena i bilo je promjena). Nikad ne sprječava zatvaranje.</summary>
    private static void KopijaPriZatvaranju()
    {
        if (!App.Usluge.GetRequiredService<Sjednica>().Otkljucano) return;
        var ishod = App.Usluge.GetRequiredService<Evidencija.Podaci.KopijeServis>().Automatska();
        var poruka = ishod switch
        {
            Evidencija.Podaci.IshodKopije.MapaNedostupna => "Mapa za sigurnosne kopije nije dostupna (je li USB utaknut?).\nKopija ovaj put NIJE spremljena.",
            Evidencija.Podaci.IshodKopije.Greska => "Sigurnosna kopija nije spremljena (greška pri pisanju).\nProvjerite mapu u Postavke → Sigurnosne kopije.",
            _ => null,
        };
        if (poruka != null) MessageBox.Show(poruka, "Lovačka Evidencija", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
