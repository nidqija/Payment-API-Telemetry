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
        request.Headers.Add("X-Signature" , signature);
        request.Headers.Add("X-Event-Type" , webhookPayload.EventType);


        // create dictionary to store headers as a property to be sent to merchant
        var headerDistrict = new Dictionary<string , string>
        {
            ["X-Signature"] = Signature,
            ["X-Event-Type"] = webhookPayload.EventType,
            ["Content-Type"] = "application/json"
        };


// TODO : ADD DELAY IN BETWEEN REQUESTS
       return headerDistrict;
    }
}