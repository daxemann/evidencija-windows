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
        Closed += (_, _) => App.Usluge.GetRequiredService<Sjednica>().Zakljucaj();
    }
}
