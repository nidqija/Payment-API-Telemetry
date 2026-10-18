using System.Security.Cryptography;
using System.Text;
using Npgsql;
using StackExchange.Redis;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Features.Observability;
using System.Diagnostics;



namespace Features.Invoice;

// init the default state of invoice template 
public record InvoiceOptions {
    public string? AccentColor = "#000000";
    public bool ShowTax = true;
    public string? Watermark = "PAID";
    
}

public class InvoiceService(IConnectionMultiplexer redis , NpgsqlDataSource dataSource)
{
    // init redis db to be used for caching
    private readonly IDatabase _redis = redis.GetDatabase();

    // helper function to compute option hash 
    // this is required for generating unique key in redis for different options
    // ensure that the cache will not overwrite each other 
    private string ComputeOptionsHash(InvoiceOptions options)
    {
        string raw = $"{options.AccentColor}:{options.ShowTax}:{options.Watermark}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    // record for payment invoice data
    // act as a data class for the payment invoice data
    private sealed record PaymentInvoiceData(
        Guid Id,
        long Amount,
        string Currency,
        DateTime CreatedAt,
        string? CustomerEmail,
        string MerchantName,
        string MerchantEmail
    );

    // helper function that receives the paymentinvoice data payload and generate the pdf in byte code 
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

    // main function to get or generate the pdf to be returned back to client
    public async Task<byte[]?> GetOrGenerateInvoicePdf(
        Guid paymentId,
        InvoiceOptions invoiceOptions,
        CancellationToken ct = default
    )
    {

        // we use activity source to trace the execution of this function
        // this is useful for debugging and performance monitoring
        // we set the payment id as a tag to the activity source for easy identification of the trace
        using var activity = ApiTracingService.ActivitySource.StartActivity("GetOrGenerateInvoicePdf");

        activity?.SetTag("payment.id" , paymentId.ToString());



        // compute the option hash code
        string optionHash = ComputeOptionsHash(invoiceOptions);

        // create the cache key for redis 
        string cachedKey = $"cache:pdf:{paymentId}:{optionHash}";

        // ========== REDIS CACHE LOOKUP ========== OPERATION 1 
        // we add an activiy event to indicate that we are starting to look up the redis cache for the pdf
        activity?.AddEvent(new ActivityEvent("redis_cache_lookup_started"));


        // retrieve the pdf from redis cache first
        byte[]? cachePdf = await _redis.StringGetAsync(cachedKey);

        


        // if pdf is found , return it immediately 
        // this is to ensure fast retrieval of the pdf 
        // and reduce the constant poll on the database
        if(cachePdf is not null && cachePdf.Length > 0)
        {

            // set the activity tag to indicate that the pdf was found in cache
            activity?.SetTag("cache.hit" , true);

            // set an activity tag to indicate the size of the pdf in bytes
            activity?.SetTag("invoice.size_bytes" , cachePdf.Length);

            // set the activity status to ok to indicate that the operation was successful
            // write back to the console that the pdf was retrieved from cache
            activity?.SetStatus(ActivityStatusCode.Ok);

            Console.WriteLine("Retrieved PDF from cache!");
            return cachePdf;
        }

        // set an activity tag to indicate that the pdf was not found in cache
        activity?.SetTag("cache.hit" , false);

        // if pdf is not found in cache , retrieve from db instead
        // init query for payment invoice payload
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

        // open connection to db
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(query, conn);

        // add parameter for payment id
        cmd.Parameters.AddWithValue(paymentId);


        // execute the query and get the result
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        // check if there is a record returned from the db
        // then map the record to the PaymentInvoiceData object
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

        // if the data record is empty , return null back to the client
        if(data is null)
        {
            // set the activity status to error to indicate that the operation failed
            activity?.SetStatus(ActivityStatusCode.Error, "Payment record not found or not successful");
            return null;
        }


        // ========== PDF GENERATION ========== OPERATION 2
        // add an activity event to indicate that we are starting to generate the pdf
        activity?.AddEvent(new ActivityEvent("pdf_generation_started"));
        var stopwatch = Stopwatch.StartNew();

        // call the pdf helper function to retrieve the pdf generated
        byte[] pdfBytes = GeneratePdfDocument(data , invoiceOptions);

        // add an activity event to indicate that we have completed the pdf generation
        stopwatch.Stop();

        ApiTracingService.InvoiceRenderDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

        // set the activity tags to indicate the duration of the pdf generation 
        // and the size of the pdf in bytes
        activity?.SetTag("pdf.render.duration_ms" , stopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("invoice.size_bytes" , pdfBytes.Length);

        // store the generated pdf in redis cache
        // can be used for future requests
        await _redis.StringSetAsync(cachedKey, pdfBytes, TimeSpan.FromDays(7));

        // return the pdfbyte code
        // set the status of the activity to ok to indicate that the operation was successful
        activity?.SetStatus(ActivityStatusCode.Ok);
        return pdfBytes;

    }
}





