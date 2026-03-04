using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using BadgeCraft_Net.Models;
using System.Text.Json;
using System.Globalization;
using QRCoder;
using System.Net.Http;


public class BadgePdfService
{
    private readonly IWebHostEnvironment _env;

    public BadgePdfService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public string GenerateBadgesPdf(
        BadgeTemplate template,
        List<Dictionary<string, string>> records)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var outputFolder = Path.Combine(webRoot, "generated");
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        var fileName = $"Badges_{Guid.NewGuid()}.pdf";
        var filePath = Path.Combine(outputFolder, fileName);

        Console.WriteLine($"DEBUG: Generating PDF for template '{template.Name}' with {records?.Count ?? 0} records.");

        if (records == null || records.Count == 0)
        {
            throw new Exception("No records provided for PDF generation.");
        }

        var badgeWidth = (float)template.BadgeWidth > 10 ? (float)template.BadgeWidth : 85f; // Fallback to CR80 width
        var badgeHeight = (float)template.BadgeHeight > 10 ? (float)template.BadgeHeight : 54f; // Fallback to CR80 height

        Document.Create(container =>
        {
            // Chunk records by 6 to fit on one A4 page (2x3 grid)
            var recordChunks = records
                .Select((r, i) => new { Index = i, Value = r })
                .GroupBy(x => x.Index / 6)
                .Select(g => g.Select(x => x.Value).ToList())
                .ToList();

            foreach (var chunk in recordChunks)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);

                    page.Content().Padding(0).Grid(grid =>
                    {
                        grid.Columns(2); // 2 badges per row

                        foreach (var record in chunk)
                        {
                            if (record == null) continue;

                            // Each badge is a grid item with specific height/width
                            grid.Item()
                                .Border(0.2f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Height(badgeHeight, Unit.Millimetre)
                                .Width(badgeWidth, Unit.Millimetre)
                                .Layers(layers =>
                            {
                                // 0. Primary Layer (Defining the base area)
                                layers.PrimaryLayer().Height(badgeHeight, Unit.Millimetre).Width(badgeWidth, Unit.Millimetre);

                                // 1. Background Layer
                                if (!string.IsNullOrEmpty(template.Background))
                                {
                                    try
                                    {
                                        byte[]? bgData = LoadImageData(template.Background, webRoot);
                                        if (bgData != null)
                                        {
                                            layers.Layer().Image(bgData).FitArea();
                                        }
                                        else
                                        {
                                            // Maybe it's a color hex? 
                                            // QuestPDF handles colors in layers.Layer().Background(color)
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"DEBUG: Error loading background: {ex.Message}");
                                    }
                                }

                                // 2. Field Layers
                                if (template.Fields != null)
                                {
                                    var recordDict = new Dictionary<string, string>(record, StringComparer.OrdinalIgnoreCase);

                                    foreach (var field in template.Fields)
                                    {
                                        if (field == null) continue;

                                        var fieldId = field.Id.ToString();
                                        var fieldKey = field.Key ?? "";

                                        string? value = null;
                                        if (recordDict.ContainsKey(fieldId))
                                            value = recordDict[fieldId];
                                        else if (recordDict.ContainsKey(fieldKey))
                                            value = recordDict[fieldKey];
                                        
                                        value ??= field.DefaultValue ?? "";

                                        if (string.IsNullOrEmpty(value) && field.IsRequired) continue;

                                        var type = (field.Type ?? "Text").ToLower();
                                        
                                        float x = (float)(field.X * (decimal)badgeWidth / 100m);
                                        float y = (float)(field.Y * (decimal)badgeHeight / 100m);
                                        float w = (float)(field.Width * (decimal)badgeWidth / 100m);
                                        float h = (float)(field.Height * (decimal)badgeHeight / 100m);

                                        if (w <= 0) w = 20f;
                                        if (h <= 0) h = 10f;

                                        var layer = layers.Layer()
                                            .PaddingLeft(x, Unit.Millimetre)
                                            .PaddingTop(y, Unit.Millimetre)
                                            .Width(w, Unit.Millimetre)
                                            .Height(h, Unit.Millimetre);

                                        if (type == "text")
                                        {
                                            var fontSize = 12f;
                                            var color = Colors.Black;
                                            var alignment = "left";

                                            if (!string.IsNullOrEmpty(field.StyleJson))
                                            {
                                                try
                                                {
                                                    var style = JsonSerializer.Deserialize<Dictionary<string, object>>(field.StyleJson);
                                                    if (style != null)
                                                    {
                                                        if (style.ContainsKey("fontSize"))
                                                            float.TryParse(style["fontSize"]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
                                                        
                                                        string? cStr = null;
                                                        if (style.ContainsKey("fontColor")) cStr = style["fontColor"]?.ToString();
                                                        else if (style.ContainsKey("color")) cStr = style["color"]?.ToString();

                                                        if (!string.IsNullOrEmpty(cStr)) color = cStr;

                                                        if (style.ContainsKey("textAlign"))
                                                            alignment = style["textAlign"]?.ToString()?.ToLower() ?? "left";
                                                    }
                                                }
                                                catch { }
                                            }

                                            var textLayer = layer;
                                            if (alignment == "center") textLayer = layer.AlignCenter();
                                            else if (alignment == "right") textLayer = layer.AlignRight();

                                            textLayer.Text(text =>
                                            {
                                                text.Span(string.IsNullOrEmpty(value) ? "" : value)
                                                    .FontSize(fontSize <= 0 ? 12 : fontSize)
                                                    .FontColor(color);
                                            });
                                        }
                                        else if (type == "image" && !string.IsNullOrEmpty(value))
                                        {
                                            try
                                            {
                                                byte[]? imageData = LoadImageData(value, webRoot);
                                                if (imageData != null)
                                                {
                                                    layer.Image(imageData).FitArea();
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"DEBUG: Image NOT FOUND or could not be loaded: {value}");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"DEBUG: Error loading image field: {ex.Message}");
                                            }
                                        }
                                        else if (type == "qr" && !string.IsNullOrEmpty(value))
                                        {
                                            try
                                            {
                                                using var qrGenerator = new QRCodeGenerator();
                                                using var qrCodeData = qrGenerator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
                                                using var qrCode = new PngByteQRCode(qrCodeData);
                                                byte[] qrCodeImage = qrCode.GetGraphic(20);
                                                layer.Image(qrCodeImage).FitArea();
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            });
                        }
                    });
                });
            }
        }).GeneratePdf(filePath);

        return filePath;
    }

    private byte[]? LoadImageData(string path, string webRoot)
    {
        if (string.IsNullOrEmpty(path)) return null;

        path = path.Trim('\"', ' ');

        try
        {
            // 1. Handle URLs
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var client = new HttpClient();
                // We use .GetAwaiter().GetResult() because QuestPDF's document generation is typically synchronous
                return client.GetByteArrayAsync(path).GetAwaiter().GetResult();
            }

            // 2. Handle Absolute Paths
            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }

            // 3. Handle Relative Paths (against WebRoot)
            var webRootPath = Path.Combine(webRoot, path.TrimStart('/'));
            if (File.Exists(webRootPath))
            {
                return File.ReadAllBytes(webRootPath);
            }

            // 4. Handle Relative Paths (against CurrentDirectory)
            var localPath = Path.Combine(Directory.GetCurrentDirectory(), path.TrimStart('/'));
            if (File.Exists(localPath))
            {
                return File.ReadAllBytes(localPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in LoadImageData for '{path}': {ex.Message}");
        }

        return null;
    }
}

