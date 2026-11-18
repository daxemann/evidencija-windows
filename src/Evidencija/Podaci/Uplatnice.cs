using System.Text;
using Evidencija.Pomoc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZXing;
using ZXing.PDF417.Internal;

namespace Evidencija.Podaci;

/// <summary>Jedna uplatnica HUB-3A.</summary>
public sealed record Uplatnica(
    string Naslov, string Platitelj, string PlatiteljAdresa, string PlatiteljMjesto,
    string Primatelj, string PrimateljAdresa, string PrimateljMjesto,
    string Iban, long Iznos, string Model, string Poziv, string Sifra, string Opis,
    string? Dospijece, bool Djelomicno);

/// <summary>Uplatnice HUB-3A s 2D kodom (PDF417) – isti sadržaj koda kao u online Evidenciji.</summary>
public static class Uplatnice
{
    private static readonly Encoding Iso88592;

    static Uplatnice()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Iso88592 = Encoding.GetEncoding("ISO-8859-2");
        QuestPDF.Settings.License = LicenseType.Community; // besplatno za udruge i neprofitne
    }

    /// <summary>Uplatnice za sve otvorene rate (ili jedna za cijeli ostatak ako nema rata).</summary>
    public static List<Uplatnica> ZaClana(Clan clan, Clanarina c, Udruga u)
    {
        var rate = c.Rate.Count > 0
            ? c.Rate.Select(r => (r.RedniBroj, r.Iznos, r.Preostalo, (string?)r.Dospijece)).ToList()
            : [(0, c.Iznos, c.Preostalo, null)];
        var n = rate.Count;
        return rate.Where(r => r.Preostalo > 0).Select(r =>
        {
            var opisRate = n > 1 ? $"{r.RedniBroj}. rata" : "";
            string Zamijeni(string s) => s.Replace("{GODINA}", c.Godina.ToString()).Replace("{RATA_OPIS}", opisRate)
                .Replace("{RATA}", Math.Max(1, r.RedniBroj).ToString());
            return new Uplatnica(
                Naslov: (n > 1 ? $"{r.RedniBroj}. rata · " : "Članarina ") + c.Godina + ".",
                Platitelj: $"{clan.Ime} {clan.Prezime}",
                PlatiteljAdresa: $"{clan.Ulica} {clan.KucniBroj}".Trim(),
                PlatiteljMjesto: $"{clan.PostanskiBroj} {clan.Mjesto}".Trim(),
                Primatelj: u.Naziv, PrimateljAdresa: u.Adresa, PrimateljMjesto: u.Mjesto,
                Iban: UdrugaServis.BezRazmaka(u.Iban).ToUpperInvariant(),
                Iznos: r.Preostalo, Model: u.Model,
                Poziv: UdrugaServis.Poziv(u.Poziv, c.Godina, clan.ClanskiBroj, r.RedniBroj),
                Sifra: u.Sifra.ToUpperInvariant(),
                Opis: string.Join(' ', Zamijeni(u.Opis).Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                Dospijece: r.Item4, Djelomicno: r.Preostalo < r.Iznos);
        }).ToList();
    }

    /// <summary>Sadržaj 2D koda prema HUB-3 (polja skraćena na propisanu duljinu, svako završava s LF).</summary>
    public static string HubPodaci(Uplatnica u)
    {
        static string P(string s, int max)
        {
            var t = string.Join(' ', (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return t.Length > max ? t[..max] : t;
        }
        return string.Join('\n',
            "HRVHUB30", "EUR", u.Iznos.ToString().PadLeft(15, '0'),
            P(u.Platitelj, 30), P(u.PlatiteljAdresa, 27), P(u.PlatiteljMjesto, 27),
            P(u.Primatelj, 25), P(u.PrimateljAdresa, 25), P(u.PrimateljMjesto, 27),
            P(u.Iban, 21), P(u.Model, 4), P(u.Poziv, 22), P(u.Sifra, 4), P(u.Opis, 35)) + "\n";
    }

    /// <summary>PDF417 (razina korekcije 4, 9 stupaca, visina retka 3 modula) kao SVG.</summary>
    public static string Pdf417Svg(string podaci)
    {
        var m = Pdf417Matrica(podaci);
        const int tiho = 2;
        var put = new StringBuilder();
        for (var y = 0; y < m.Height; y++)
        {
            var x = 0;
            while (x < m.Width)
            {
                if (!m[x, y]) { x++; continue; }
                var poc = x;
                while (x < m.Width && m[x, y]) x++;
                put.Append($"M{poc + tiho} {y + tiho}h{x - poc}v1h-{x - poc}z");
            }
        }
        var w = m.Width + 2 * tiho;
        var h = m.Height + 2 * tiho;
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" width=\"{w}\" height=\"{h}\" shape-rendering=\"crispEdges\">" +
               $"<rect width=\"{w}\" height=\"{h}\" fill=\"#fff\"/><path d=\"{put}\" fill=\"#000\"/></svg>";
    }

    internal static ZXing.Common.BitMatrix Pdf417Matrica(string podaci)
    {
        // HUB-3 traži ISO-8859-2; bajtove predajemo koderu kao Latin-1 znakove (bez ECI oznake)
        var latin1 = Encoding.Latin1.GetString(Iso88592.GetBytes(podaci));
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.ERROR_CORRECTION] = PDF417ErrorCorrectionLevel.L4,
            [EncodeHintType.PDF417_DIMENSIONS] = new Dimensions(9, 9, 3, 90),
            [EncodeHintType.PDF417_ASPECT_RATIO] = PDF417AspectRatio.A3,
            [EncodeHintType.MARGIN] = 0,
            [EncodeHintType.DISABLE_ECI] = true,
        };
        return new ZXing.PDF417.PDF417Writer().encode(latin1, BarcodeFormat.PDF_417, 0, 0, hints);
    }

    /// <summary>PDF s uplatnicama: 3 na A4 stranicu.</summary>
    public static byte[] Pdf(IReadOnlyList<Uplatnica> uplatnice)
    {
        const string Crvena = "#c8102e";
        return Document.Create(d => d.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.MarginVertical(6, Unit.Millimetre);
            p.MarginHorizontal(8, Unit.Millimetre);
            p.DefaultTextStyle(t => t.FontSize(8).FontColor("#222")); // ugrađeni font (Lato) – ima č, ć, đ, š, ž
            p.Content().Column(stupac =>
            {
                for (var i = 0; i < uplatnice.Count; i++)
                {
                    if (i > 0 && i % 3 == 0) stupac.Item().PageBreak();
                    var u = uplatnice[i];
                    stupac.Item().Height(89, Unit.Millimetre).PaddingTop(2, Unit.Millimetre)
                        .BorderBottom(0.6f).BorderColor(Colors.Grey.Lighten1).Row(red =>
                        {
                            red.RelativeItem(47).BorderRight(0.6f).BorderColor(Crvena).PaddingRight(4, Unit.Millimetre).Column(l =>
                            {
                                l.Item().Text("NALOG ZA PLAĆANJE").Bold().FontColor(Crvena).FontSize(9);
                                Polje(l, "Platitelj (naziv/ime i adresa)", $"{u.Platitelj}\n{u.PlatiteljAdresa}\n{u.PlatiteljMjesto}", 15);
                                Polje(l, "Primatelj (naziv/ime i adresa)", $"{u.Primatelj}\n{u.PrimateljAdresa}\n{u.PrimateljMjesto}", 15);
                                l.Item().PaddingTop(2.5f, Unit.Millimetre).Width(58, Unit.Millimetre).Svg(Pdf417Svg(HubPodaci(u)));
                            });
                            red.RelativeItem(53).PaddingLeft(4, Unit.Millimetre).Column(r =>
                            {
                                r.Item().AlignRight().Text("HUB-3A").Bold().FontColor(Crvena).FontSize(9);
                                r.Item().Row(x =>
                                {
                                    x.ConstantItem(16, Unit.Millimetre).PaddingRight(1, Unit.Millimetre).Column(c => Polje(c, "Valuta", "EUR"));
                                    x.RelativeItem().Column(c => Polje(c, "Iznos", "=" + Novac.Unos(u.Iznos), desno: true));
                                });
                                Polje(r, "IBAN ili broj računa primatelja", UdrugaServis.IbanRazmaci(u.Iban));
                                r.Item().Row(x =>
                                {
                                    x.ConstantItem(16, Unit.Millimetre).PaddingRight(1, Unit.Millimetre).Column(c => Polje(c, "Model", u.Model));
                                    x.RelativeItem().Column(c => Polje(c, "Poziv na broj primatelja", u.Poziv));
                                });
                                r.Item().Row(x =>
                                {
                                    x.ConstantItem(16, Unit.Millimetre).PaddingRight(1, Unit.Millimetre).Column(c => Polje(c, "Šifra namj.", u.Sifra));
                                    x.RelativeItem().Column(c => Polje(c, "Opis plaćanja", u.Opis));
                                });
                                Polje(r, "Datum izvršenja", "");
                                r.Item().PaddingTop(2, Unit.Millimetre).Text(t =>
                                {
                                    t.DefaultTextStyle(s => s.FontSize(6.5f).FontColor("#666"));
                                    if (u.Dospijece != null) { t.Span("Rok uplate: "); t.Span(Tekst.Datum(u.Dospijece)).Bold(); }
                                    if (u.Djelomicno) t.Span(" · preostali iznos rate");
                                    t.Span("\nSkenirajte 2D kod u mobilnom bankarstvu ili predajte u banci / pošti / FINA-i.");
                                });
                            });
                        });
                }
            });
        })).GeneratePdf();

        static void Polje(ColumnDescriptor c, string naziv, string vrijednost, float visinaMm = 0, bool desno = false)
        {
            c.Item().PaddingTop(1.2f, Unit.Millimetre).Text(naziv.ToUpperInvariant()).FontSize(6).FontColor(Crvena);
            var okvir = c.Item().Border(0.6f).BorderColor(Crvena).Background("#fff6f6").PaddingVertical(1, Unit.Millimetre).PaddingHorizontal(1.5f, Unit.Millimetre);
            if (visinaMm > 0) okvir = okvir.Height(visinaMm, Unit.Millimetre);
            var tekst = okvir.Text(vrijednost.Length == 0 ? " " : vrijednost).FontSize(8.5f);
            if (desno) tekst.AlignRight().Bold();
        }
    }
}
