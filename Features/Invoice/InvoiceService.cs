using System.Security.Cryptography;
using System.Text;
using Npgsql;
using StackExchange.Redis;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace Features.Invoice;


public record InvoiceOptions {
    public string? AccentColor = "#000000";
    public bool ShowTax = true;
    public string? Watermark = "PAID";
    
}

public class InvoiceService(IConnectionMultiplexer redis , NpgsqlDataSource dataSource)
{
    private readonly IDatabase _redis = redis.GetDatabase();

    private string ComputeOptionsHash(InvoiceOptions options)
    {
        string raw = $"{options.AccentColor}:{options.ShowTax}:{options.Watermark}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private sealed record PaymentInvoiceData(
        Guid Id,
        long Amount,
        string Currency,
        DateTimeOffset CreatedAt,
        string? CustomerEmail,
        string MerchantName,
        string MerchantEmail
    );


    private static byte[] GeneratePdfDocument(PaymentInvoiceData data , InvoiceOptions options)
    {
        var accentColor = options.AccentColor ?? "#000000";
        decimal displayAmount = data.Amount / 100m;
        decimal tax = options.ShowTax ? Math.Round(displayAmount * 0.06m, 2) : 0m;
        decimal total = displayAmount + tax;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("PAYMENT RECEIPT")
                            .FontSize(22)
                            .Bold()
                            .FontColor(accentColor);

                        col.Item().Text($"Receipt #: {data.Id.ToString()[..8].ToUpperInvariant()}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(180).Column(col =>
                    {
                        col.Item().AlignRight().Text(data.MerchantName).Bold();
                        col.Item().AlignRight().Text(data.MerchantEmail).FontSize(9);
                        col.Item().AlignRight().Text($"Date: {data.CreatedAt:yyyy-MM-dd HH:mm UTC}").FontSize(9);
                    });
                });

                // Content Body
                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    if (!string.IsNullOrEmpty(options.Watermark))
                    {
                        col.Item().AlignCenter().Text(options.Watermark)
                            .FontSize(36)
                            .Bold()
                            .FontColor(Colors.Green.Lighten3);
                    }

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1);
                        });

                        // Table header
                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Description").Bold();
                            header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Amount").Bold();
                        });

                        // Line items
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"Payment charge for {data.CustomerEmail ?? "Customer"}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"{data.Currency} {displayAmount:N2}");

                        if (options.ShowTax)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text("Service Tax (6%)");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"{data.Currency} {tax:N2}");
                        }

                        // Total row
                        table.Cell().Padding(5).Text("Total Paid").Bold().FontSize(12);
                        table.Cell().Padding(5).AlignRight().Text($"{data.Currency} {total:N2}").Bold().FontSize(12).FontColor(accentColor);
                    });
                });

                // Footer
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Thank you for your business. Generated by ");
                    text.Span("IdemGate Payment Gateway").Bold();
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]?> GetOrGenerateInvoicePdf(
        Guid paymentId,
        InvoiceOptions invoiceOptions,
        CancellationToken ct = default
    )
    {
        string optionHash = ComputeOptionsHash(invoiceOptions);
        string cachedKey = $"cache:pdf:{paymentId}:{optionHash}";

        byte[]? cachePdf = await _redis.StringGetAsync(cachedKey);
        if(cachePdf is not null && cachePdf.Length > 0)
        {
            return cachePdf;
        }

        const string query = """

            SELECT 
                p.id,
                p.amount,
                p.currency,
                p.status,
                p.created_at,
                p.customer_email,
                m.name as merchant_name,
                m.email as merchant_email
            FROM payments p
                JOIN merchants m ON p.merchant_id = m.id
            WHERE p.id = $1 AND p.status = 'SUCCESS'
            LIMIT 1;
                
        """;


        PaymentInvoiceData? data = null;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(query, conn);

        cmd.Parameters.AddWithValue(paymentId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if ( await reader.ReadAsync(ct))
        {
            data = new PaymentInvoiceData(
                Id: reader.GetGuid(0),
                Amount: reader.GetInt64(1),
                Currency: reader.GetString(2),
                CreatedAt: reader.GetDateTime(4),
                CustomerEmail: reader.IsDBNull(5) ? null : reader.GetString(5),
                MerchantName: reader.GetString(6),
                MerchantEmail: reader.GetString(7)
            );
        }

        if(data is null)
        {
            return null;
        }

        byte[] pdfBytes = GeneratePdfDocument(data , invoiceOptions);

        await _redis.StringSetAsync(cachedKey, pdfBytes, TimeSpan.FromDays(7));
        return pdfBytes;

    }
}





