using Npgsql;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;


namespace Features.Webhook;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {

        // create a endpoint to retrieve list of webhook callbacks in logs_page.html
        app.MapGet("/v1/webhooks" , async (NpgsqlDataSource dataSource , CancellationToken ct) =>
        {
            const string sql = """
                SELECT * FROM webhook_callback
                ORDER BY delivered_at DESC
                LIMIT 50
                """;

                await using var conn = await dataSource.OpenConnectionAsync(ct);
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(ct);

                var items = new List<object>();
                while(await reader.ReadAsync())
                {
                        items.Add(new
                    {
                        id = reader.GetGuid(reader.GetOrdinal("id")),
                        event_type = reader.GetString(reader.GetOrdinal("event_type")),
                        payload = reader.GetString(reader.GetOrdinal("payload")),
                        target_url = reader.GetString(reader.GetOrdinal("target_url")),
                        http_status_code = reader.GetString(reader.GetOrdinal("http_status_code")),
                        delivered_at = reader.GetDateTime(reader.GetOrdinal("delivered_at")),
                        duration_ms = reader.IsDBNull(reader.GetOrdinal("duration_ms")) ? 0 : reader.GetInt64(reader.GetOrdinal("duration_ms"))
                    });
                }

                return Results.Ok(items);

               
        })
        .WithTags("webhooks");


        // add endpoint to test webhook delivery
        // this will ensure that the cloudflare tunnel url endpoint is publicly accessible
        // and successfully receiving the webhook call
        app.MapPost("/test-webhook" , async (HttpContext context) =>
        {
            // read the webhook call from request body
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            // print the webhook call to console
            Console.WriteLine($"[Webhook Receiver] Received successfully: {body} ");

            // return a response to the merchant
            return Results.Ok(new {message = "Webhook received successfully"});
        })
        .WithTags("webhooks");
       
        return app;
    }
}