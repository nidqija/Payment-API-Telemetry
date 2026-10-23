using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Features.Webhook;




public record WebhookPayload(
    Guid PaymentId,
    string EventType,
    string Payload,
    string TargetUrl,
    DateTime Time,
    string Duration
);


public class WebhookDispatch ( HttpClient httpClient , NpgsqlDataSource npgsql )
{
    
    public async Task DeliverAsync(
        Guid PaymentId,
        string TargetUrl,
        string MerchantSecret,
        WebhookPayload webhookPayload,
        CancellationToken ct = default
    )
    {
        string json = JsonSerializer.Serialize(webhookPayload);

        using var hmc = new HMACSHA256 (Encoding.UTF8.GetBytes(MerchantSecret));

        string Signature = Convert.ToHexString(hmc.ComputeHash(Encoding.UTF8.GetBytes(json)));

        using var request = new HttpRequestMessage(HttpMethod.Post , TargetUrl);

        request.Content = new StringContent(json , Encoding.UTF8 , "application/json");

        request.Headers.Add("X-Signature" , signature);
        request.Headers.Add("X-Event-Type" , webhookPayload.EventType);


        var headerDistrict = new Dictionary<string , string>
        {
            ["X-Signature"] = Signature,
            ["X-Event-Type"] = webhookPayload.EventType,
            ["Content-Type"] = "application/json"
        };


// TBA
       return headerDistrict;
    }
}