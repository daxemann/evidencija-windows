using System.Windows;
using Evidencija.Podaci;
using Evidencija.Sigurnost;
using Microsoft.Extensions.DependencyInjection;

namespace Evidencija;

public partial class App : Application
{
    public static IServiceProvider Usluge { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
#if DEBUG
        // samo za testiranje: podaci u zasebnoj mapi, nikad u izdanju
        var mape = new Mape(Environment.GetEnvironmentVariable("EVIDENCIJA_TEST_MAPA"));
#else
        var mape = new Mape();
#endif
        // WebView2 sprema svoje datoteke kod nas, ne u mapu programa (Program Files je samo za čitanje)
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", mape.WebView);

        var baza = new Baza(mape);
        Shema.Nadogradi(baza);

        var usluge = new ServiceCollection();
        usluge.AddWpfBlazorWebView();
#if DEBUG
        usluge.AddBlazorWebViewDeveloperTools();
#endif
        usluge.AddSingleton(mape);
        usluge.AddSingleton(baza);
        usluge.AddSingleton(new Kljucevi(mape.Kljucevi));
        usluge.AddSingleton<Sjednica>();
        Usluge = usluge.BuildServiceProvider();

        base.OnStartup(e);
    }
}
