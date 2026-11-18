using System.Windows;
using Evidencija.Podaci;
using Evidencija.Sigurnost;
using Microsoft.Extensions.DependencyInjection;

namespace Evidencija;

public partial class App : Application
{
    public static IServiceProvider Usluge { get; private set; } = null!;

    /// <summary>Samo jedan pokrenut program – dva bi istovremeno pisala u istu bazu i kopije.</summary>
    private static Mutex? _jedanProgram;

    protected override void OnStartup(StartupEventArgs e)
    {
        _jedanProgram = new Mutex(true, @"Local\LovackaEvidencijaWindows", out var prvi);
        if (!prvi)
        {
            MessageBox.Show("Lovačka Evidencija je već otvorena.", "Lovačka Evidencija", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

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
        usluge.AddSingleton<ClanoviServis>();
        usluge.AddSingleton<SekcijeServis>();
        usluge.AddSingleton<FotoServis>();
        usluge.AddSingleton<VrsteClanstvaServis>();
        usluge.AddSingleton<ClanarinaServis>();
        usluge.AddSingleton<UdrugaServis>();
        usluge.AddSingleton<VrsteAkcijaServis>();
        usluge.AddSingleton<DodaciAkcijaServis>();
        usluge.AddSingleton<BodoviServis>();
        usluge.AddSingleton<KopijeServis>();
        usluge.AddSingleton<UvozServis>();
        Usluge = usluge.BuildServiceProvider();

        base.OnStartup(e);
    }
}
