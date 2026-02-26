using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using BadgeCraft_Net.Models;

public class BadgePdfService
{
    public byte[] GenerateBadgesPdf(List<Badge> badges)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);

                page.Content().Grid(grid =>
                {
                    grid.Columns(3); // 3 columns = 6 per page

                    foreach (var badge in badges)
                    {
                        grid.Item().Border(1).Padding(10).Column(col =>
                        {
                            col.Item().Background(badge.BgColor ?? "#FFFFFF")
                                .Padding(10)
                                .Column(inner =>
                                {
                                    inner.Item()
                                        .Text(badge.Title)
                                        .FontSize(16)
                                        .Bold()
                                        .FontColor(badge.TextColor ?? "#000000");

                                    inner.Item()
                                        .Text(badge.Subtitle)
                                        .FontSize(12)
                                        .FontColor(badge.TextColor ?? "#000000");

                                    if (!string.IsNullOrEmpty(badge.ImageUrl))
                                    {
                                        try
                                        {
                                            var extension = Path.GetExtension(badge.ImageUrl).ToLower();

                                            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                                            if (allowed.Contains(extension))
                                            {
                                                var fullPath = Path.Combine(
                                                    Directory.GetCurrentDirectory(),
                                                    "wwwroot",
                                                    badge.ImageUrl.TrimStart('/')
                                                );

                                                if (File.Exists(fullPath))
                                                {
                                                    inner.Item().Image(fullPath, ImageScaling.FitArea);
                                                }
                                            }
                                        }
                                        catch
                                        {
                                           
                                        }
                                    }
                                });
                        });
                    }
                });
            });
        }).GeneratePdf();
    }
}