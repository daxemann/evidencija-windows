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
