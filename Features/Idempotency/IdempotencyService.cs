namespace Features.Idempotency;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;


public class IdempotencyService(NpgsqlDataSource dataSource)
{


    // public record IdempotencyRecord
    public abstract record IdempotencyResult
    {
        public sealed record Acquired(string RequestHash) : IdempotencyResult;
        public sealed record Replay(string RequestHash, string ResponseBody) : IdempotencyResult;
        public sealed record PayloadMismatch : IdempotencyResult;
        public sealed record Timeout : IdempotencyResult;


    }


    private static readonly TimeSpan PollingTimeout = TimeSpan.FromSeconds(5); // maximum time to wait for an existing request to complete
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(100); // interval between polling attempts


    // helper function to compute SHA256 hash of a string for request hashing
    private static string ComputeSha256(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }



    public async Task<IdempotencyResult> BeginOrWaitAsync(
        Guid merchantId, 
        string IdempotencyKey, 
        string requestPayload, 
        CancellationToken ct // ct is used to cancel the operation if it takes too long
        )
    {

        // compute the hash request code
        string requestHash = ComputeSha256(requestPayload);

        await using var conn = await dataSource.OpenConnectionAsync(ct);

        // log the action of attempting to insert a new idempotency record
        Console.WriteLine($"Attempting to insert new idempotency record for merchant {merchantId} with key {IdempotencyKey} and hash {requestHash}");

        const string insertSql = """
        
            INSERT INTO idempotency_records(
                merchant_id,
                idempotency_key,
                request_hash,
                status,
                created_at,
                expires_at
                )

                VALUES(
                    $1,
                    $2,
                    $3,
                    'PENDING',
                    NOW(),
                    NOW() + INTERVAL '1 hour'
                )
        """;

        // attempt to insert a new record for the idempotency key
        await using (var insertCmd = new NpgsqlCommand(insertSql , conn))
        {
            // parse the parameters for the SQL command to prevent SQL injection
            insertCmd.Parameters.AddWithValue(merchantId);
            insertCmd.Parameters.AddWithValue(IdempotencyKey);
            insertCmd.Parameters.AddWithValue(requestHash);

            // execute the command and check if a new row was inserted
            int rowsAffected = await insertCmd.ExecuteNonQueryAsync(ct);

            // if a new row was inserted, return an acquired result with the request hash
            if (rowsAffected > 0)
            {
                return new IdempotencyResult.Acquired(requestHash);
            }
        }


        // set the deadline for the polling operation to wait for an existing request to complete
        var deadline = DateTime.UtcNow + PollingTimeout;

        // loop until the deadline is reached or a result is found
        while(DateTime.UtcNow < deadline)
        {
            // check if the cancellation token has been triggered to cancel the operation
            ct.ThrowIfCancellationRequested();

            const string selectSql = """
              
                SELECT request_hash, response_body
                FROM idempotency_records
                WHERE merchant_id = $1 AND idempotency_key = $2
                LIMIT 1;
            """;

            // open a new command to select the existing idempotency record for the given merchant and key
            await using var selectCmd = new NpgsqlCommand(selectSql, conn);

            // parse the parameters for the SQL command to prevent SQL injection
            selectCmd.Parameters.AddWithValue(merchantId);
            selectCmd.Parameters.AddWithValue(IdempotencyKey);


            // execute the command and read the result
            await using var reader = await selectCmd.ExecuteReaderAsync(ct);


            // if a record is found , check if the request hash matches and if the status is completed
            if (await reader.ReadAsync(ct))
            {
                string existingHash = reader.GetString(0);
                string status = reader.GetString(1);

                // if the request hash does not match the existing hash , return a payload mismatch result to 
                // indicate that the request payload is different from the previous request
                // if its mismatched, the client should not retry the request with the same idempotency key
                // this will be treated as a new request and the client should generate a new idempotency key for the new request
                if(!string.Equals(existingHash, requestHash , StringComparison.OrdinalIgnoreCase))
                {
                    return new IdempotencyResult.PayloadMismatch();
                }

                // if the status is completed, return a replay result with the response code and body
                // this is used to replay the response of the previous request without executing the operation again
                // this is important for idempotency to ensure that the same request does not produce different results
                if(status == "COMPLETED")
                {
                    int code = reader.GetInt32(2);
                    string body = reader.GetString(3);
                    return new IdempotencyResult.Replay(code.ToString(), body);
                }
            }

    
            await reader.CloseAsync();

            await Task.Delay(PollingInterval, ct);


        }



        return new IdempotencyResult.Timeout();
    }


    


    

}