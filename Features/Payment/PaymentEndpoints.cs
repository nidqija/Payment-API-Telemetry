using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Features.Idempotency;
using Features.Invoice;
using Features.Webhook;
using OpenTelemetry.Trace;

namespace Features.Payment;

public record CreateChargeRequest(
    long Amount,
    string? Currency,
    string? CustomerEmail,
    string? Description,
    InvoiceOptions? invoiceOptions = null,
    bool GenerateInvoice = false

);


public static class PaymentEndpoints
{


    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/payments")
        .WithTags("Payments");

        group.MapPost("", async (
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromBody] CreateChargeRequest request,
            IdempotencyService idempotencyService,
            NpgsqlDataSource dataSource,
            WebhookDispatch webhookDispatcher,
            InvoiceService invoiceService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.BadRequest(new { error = "Idempotency-Key header is required." });
            }


            var merchantId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Replace with actual merchant ID retrieval logic
            string payloadJson = JsonSerializer.Serialize(request);



            var result = await idempotencyService.BeginOrWaitAsync(
                merchantId,
                idempotencyKey,
                payloadJson,
                cancellationToken
            );

            if (result is IdempotencyService.IdempotencyResult.PayloadMismatch)
            {
                return Results.UnprocessableEntity(new { error = "Payload mismatch for the given Idempotency Key" });
            }


            if (result is IdempotencyService.IdempotencyResult.Timeout)
            {
                return Results.Conflict(new { error = "Request has been timed out , please try again" });
            }

            if (result is IdempotencyService.IdempotencyResult.Replay replay)
            {
                return Results.Content(replay.ResponseBody, "application/json", statusCode: int.Parse(replay.RequestHash));
            }

            try
            {



                var chargeId = Guid.NewGuid();

                const string insertChargeSql = """

                    INSERT INTO payments ( id , merchant_id , idempotency_key , amount , currency , customer_email, status , created_at , updated_at )
                    VALUES ( $1 , $2 , $3 , $4 , $5 , $6 , 'SUCCESS' , NOW() , NOW() )
                
                """;



                Console.WriteLine("Inserting charge into database...");
                Console.WriteLine($"Charge ID: {chargeId}");


                await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
                await using var cmd = new NpgsqlCommand(insertChargeSql, conn);
                cmd.Parameters.AddWithValue(chargeId);
                cmd.Parameters.AddWithValue(merchantId);
                cmd.Parameters.AddWithValue(idempotencyKey);
                cmd.Parameters.AddWithValue(request.Amount);
                cmd.Parameters.AddWithValue(request.Currency ?? "USD");
                cmd.Parameters.AddWithValue(request.CustomerEmail);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                
                const string merchantSql = """
                 SELECT webhook_url, webhook_secret 
                 FROM merchants 
                 WHERE id = @merchantId;
                """;

                await using var merchantCmd = new NpgsqlCommand(merchantSql, conn);
                merchantCmd.Parameters.AddWithValue("merchantId", merchantId);

                await using var reader = await merchantCmd.ExecuteReaderAsync(cancellationToken);
                reader.Read();
                var webhookUrl = reader.GetString(0);
                var webhookSecret = reader.GetString(1);
                reader.Close();


                object? invoiceResponse = null;
                var publicBaseUrl = (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost").TrimEnd('/');
                var invoiceDownloadUrl = $"{publicBaseUrl}/v1/payments/{chargeId}/invoice";

                if (request.GenerateInvoice)
                {
                    var options = request.invoiceOptions ?? new InvoiceOptions();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await invoiceService.GetOrGenerateInvoicePdf(chargeId, options, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error Generating Invoice for Payment Id {chargeId} : {ex.Message}");
                        }
                    }


                    );

                    invoiceResponse = new
                    {
                        download_url = invoiceDownloadUrl
                    };
                }


                var response = new
                {
                    id = chargeId,
                    status = "SUCCESS",
                    amount = request.Amount,
                    currency = request.Currency,
                    customer_email = request.CustomerEmail,
                    invoice = invoiceResponse
                };

                if (!string.IsNullOrEmpty(webhookUrl) && !string.IsNullOrEmpty(webhookSecret))
                {
                    var webhookPayload = new WebhookPayload(
                        PaymentId: chargeId,
                        EventType: "charge.succeeded",
                        Payload: JsonSerializer.Serialize(response),
                        TargetUrl: webhookUrl,
                        Time: DateTime.UtcNow,
                        Duration: "0ms"
                    );

                    _ = Task.Run(async() =>
                    {
                        try
                        {
                            await webhookDispatcher.DeliverAsync(
                                chargeId,
                                webhookUrl,
                                webhookSecret,
                                webhookPayload,
                                CancellationToken.None);
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine($"Error Sending Webhook for Payment Id {chargeId} : {ex.Message}");
                        }
                    });
                    
                }



                await idempotencyService.CompleteAsync(
                    merchantId,
                    idempotencyKey,
                    200,
                    JsonSerializer.Serialize(response),
                    cancellationToken
                );

                return Results.Created($"/v1/payments/{chargeId}", response);



            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing payment: {ex.Message}");
                await idempotencyService.FailAsync(
                    merchantId,
                    idempotencyKey,
                    cancellationToken
                );

                throw;
            }



        }
        );

        return app;
    }
}