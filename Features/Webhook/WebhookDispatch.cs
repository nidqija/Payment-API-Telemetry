using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Features.Webhook;




// create a webhook payload to be sent to merchants
public record WebhookPayload(
    Guid PaymentId,
    string EventType,
    string Payload,
    string TargetUrl,
    DateTime Time,
    string Duration
);


// class to dispatch webhook to merchants
public class WebhookDispatch ( HttpClient httpClient , NpgsqlDataSource npgsql )
{

    // async function to deliver webhook back to merchant 
    
    // define parameters for webhook

    public async Task DeliverAsync(
        Guid PaymentId,
        string TargetUrl,
        string MerchantSecret,
        WebhookPayload webhookPayload,
        CancellationToken ct = default
    )
    {

        // convert payload into json to be sent to merchant backend
        string json = JsonSerializer.Serialize(webhookPayload);

        // create a valid signature to be sent to merchant
        // this is to ensure the webhook is sent by an authenticated merchant
        using var hmc = new HMACSHA256 (Encoding.UTF8.GetBytes(MerchantSecret));

        // create a cryptographic hash of the payload
        // this is to ensure that the payload is not tampered with during transmission
        string Signature = Convert.ToHexString(hmc.ComputeHash(Encoding.UTF8.GetBytes(json)));

        // create webhook request
        using var request = new HttpRequestMessage(HttpMethod.Post , TargetUrl);

        // add payload to webhook request
        request.Content = new StringContent(json , Encoding.UTF8 , "application/json");

        // add signature and event type to webhook request headers
        request.Headers.Add("X-Signature" , Signature);
        request.Headers.Add("X-Event-Type" , webhookPayload.EventType);


        // create dictionary to store headers as a property to be sent to merchant
        var headerDistrict = new Dictionary<string , string>
        {
            ["X-Signature"] = Signature,
            ["X-Event-Type"] = webhookPayload.EventType,
            ["Content-Type"] = "application/json"
        };

        // convert headers dictionary into json to be stored in the database
        string headerJson = JsonSerializer.Serialize(headerDistrict);

        // init a stopwatch counter , status code and responsebody
        var sw = Stopwatch.StartNew();
        int statusCode = 0;
        string responseBody = string.Empty;

        try
        {
            // start sending webhook to merchant endpoint
            using var response = await httpClient.SendAsync(request , ct);

            // get status code and response body
            statusCode = (int)response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync(ct);

            // if response body is too long , truncate it to 1000 characters
            if (responseBody.Length > 1000)
            {
                responseBody = responseBody[..1000];
            }
        } catch (Exception ex)
        {
            // if any exception occured , set status code to 504 and store the error message
            statusCode = 504;
            responseBody = $"Delivery failed:{ex.Message}";
        }

        finally
        {
            // stop the stopwatch and record the duration
            sw.Stop();
        }


        // init a sql command to perform database transaction
        const string insertSql = """
            INSERT INTO webhook_callback (
                payment_id,
                target_url,
                event_type,
                payload,
                http_status_code,
                request_headers,
                response_body,
                duration_ms,
                delivered_at
            ) VALUES (
                $1, $2, $3, $4::jsonb, $5, $6::jsonb, $7, $8, NOW()
            );
        """;

        // init a connection to postgres to perform transaction
        await using var conn = await npgsql.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(insertSql , conn);

        // add parameters to sql command
        cmd.Parameters.AddWithValue(PaymentId);
        cmd.Parameters.AddWithValue(TargetUrl);
        cmd.Parameters.AddWithValue(webhookPayload.EventType);
        cmd.Parameters.AddWithValue(json);
        cmd.Parameters.AddWithValue(statusCode);
        cmd.Parameters.AddWithValue(headerJson);
        cmd.Parameters.AddWithValue(responseBody);
        cmd.Parameters.AddWithValue(sw.ElapsedMilliseconds);

        // execute the sql command to insert webhook callback into database
        await cmd.ExecuteNonQueryAsync(ct);
    }
}