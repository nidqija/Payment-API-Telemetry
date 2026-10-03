using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Features.Idempotency;

namespace Features.Payment;




public static class PaymentEndpoints
{
    

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/payments")
        .WithTags("Payments");

        group.MapPost("" , async(
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromBody] CreateChargeRequest request,
            IdempotencyService idempotencyService,
            NpgsqlDataSource dataSource,
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
                ct
            );

            if ( result is IdempotencyService.IdempotencyResult.PayloadMismatch)
            {
                return Result.UnprocessableEntity(new {error = "Payload mismatch for the given Idempotency Key"});
            }


            if (result is IdempotencyService.IdempotencyResult.Timeout)
            {
                return Results.Conflict(new {error = "Request has been timed out , please try again"});
            }

            if ( result is IdempotencyService.IdempotencyResult.Replay replay)
            {
                return Results.Content(replay.ResponseBody , "application/json" , statusCode: replay.StatusCode);
            }

            try
            {
                 

                // ======================= TBC
                var chargeId = Guid.NewGuid();

                const string insertChargeSql = """

                    INSERT INTO 
                
                """;

                Console.WriteLine("Inserting charge into database...");
                Console.WriteLine($"Charge ID: {insertChargeSql}");
                // ===================================================
                
                
                
            } catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }

            Console.WriteLine("Charge created successfully.");

            
        }
        );
    }
}