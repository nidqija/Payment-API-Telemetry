using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Features.Idempotency;

namespace Features.Payment;

public record CreateChargeRequest(
    long Amount,
    string? Currency,
    string? CustomerEmail,
    string? Description
    
);


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
                cancellationToken
            );

            if ( result is IdempotencyService.IdempotencyResult.PayloadMismatch)
            {
                return Results.UnprocessableEntity(new {error = "Payload mismatch for the given Idempotency Key"});
            }


            if (result is IdempotencyService.IdempotencyResult.Timeout)
            {
                return Results.Conflict(new {error = "Request has been timed out , please try again"});
            }

            if ( result is IdempotencyService.IdempotencyResult.Replay replay)
            {
                return Results.Content(replay.ResponseBody , "application/json" , statusCode: int.Parse(replay.RequestHash));
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


                var response = new
                {
                   id = chargeId,
                   status = "SUCCESSS",
                   amount = request.Amount,
                   currency = request.Currency,
                   customer_email = request.CustomerEmail
                };


                await idempotencyService.CompleteAsync(
                    merchantId,
                    idempotencyKey,
                    200,
                    JsonSerializer.Serialize(response),
                    cancellationToken
                );

                return Results.Created($"/v1/payments/{chargeId}", response);

                
                
            } catch (Exception ex)
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