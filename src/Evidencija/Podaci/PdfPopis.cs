using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Evidencija.Podaci;

/// <summary>Jednostavan PDF popis (tablica) za ispis: članovi, imenik, članarina, bodovi.</summary>
public static class PdfPopis
{
    static PdfPopis() => QuestPDF.Settings.License = LicenseType.Community;

    public sealed record Stupac(string Naziv, float Sirina = 1, bool Desno = false);

    public static byte[] Tablica(string udruga, string naslov, string? podnaslov, IReadOnlyList<Stupac> stupci,
        IEnumerable<string[]> redovi, string? podnozje = null, bool polozeno = false)
    {
        var popis = redovi.ToList();
        return Document.Create(d => d.Page(p =>
        {
            p.Size(polozeno ? PageSizes.A4.Landscape() : PageSizes.A4);
            p.Margin(12, Unit.Millimetre);
            p.DefaultTextStyle(t => t.FontSize(9));

            p.Header().PaddingBottom(4, Unit.Millimetre).Column(z =>
            {
                z.Item().Text(udruga).FontSize(8).FontColor(Colors.Grey.Darken1);
                z.Item().Text(naslov).FontSize(14).Bold().FontColor("#2f5d23");
                if (!string.IsNullOrEmpty(podnaslov)) z.Item().Text(podnaslov).FontSize(9).FontColor(Colors.Grey.Darken2);
            });

            p.Content().Column(c =>
            {
                c.Item().Table(t =>
                {
                    t.ColumnsDefinition(def =>
                    {
                        def.ConstantColumn(22);
                        foreach (var s in stupci) def.RelativeColumn(s.Sirina);
                    });
                    t.Header(h =>
                    {
                        h.Cell().Element(Zaglavlje).Text("#");
                        foreach (var s in stupci)
                        {
                            var tx = h.Cell().Element(Zaglavlje).Text(s.Naziv);
                            if (s.Desno) tx.AlignRight();
                        }
                    });
                    for (var i = 0; i < popis.Count; i++)
                    {
                        var pozadina = i % 2 == 1 ? Colors.Grey.Lighten4 : Colors.White;
                        t.Cell().Background(pozadina).Element(Celija).Text((i + 1).ToString()).FontColor(Colors.Grey.Darken1);
                        for (var j = 0; j < stupci.Count; j++)
                        {
                            var tx = t.Cell().Background(pozadina).Element(Celija).Text(j < popis[i].Length ? popis[i][j] : "");
                            if (stupci[j].Desno) tx.AlignRight();
                        }
                    }
                });
                if (!string.IsNullOrEmpty(podnozje)) c.Item().PaddingTop(3, Unit.Millimetre).Text(podnozje).FontSize(9).SemiBold();
            });

            p.Footer().Row(r =>
            {
                r.RelativeItem().Text($"Lovačka Evidencija · {DateTime.Now:d.M.yyyy. HH:mm}").FontSize(7).FontColor(Colors.Grey.Darken1);
                r.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Darken1));
                    t.Span("Stranica ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();

        static IContainer Zaglavlje(IContainer c) =>
            c.BorderBottom(1).BorderColor("#3d6b2f").PaddingVertical(3).PaddingHorizontal(2).DefaultTextStyle(s => s.Bold().FontSize(8.5f));

        static IContainer Celija(IContainer c) =>
            c.BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2.5f).PaddingHorizontal(2);
    }
}
