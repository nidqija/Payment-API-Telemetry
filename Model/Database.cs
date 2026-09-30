namespace Model;

public record Merchant
{
    public Guid Id { get; init;}
    public string Name { get; init;} =  default!;
    public string Email {get; init;} = default!;
    public string WebhookUrl {get; init;} = default!;
    public string WebhookSecret {get; init;} = default!;
    public DateTime CreatedAt {get; init;} 

}


public record ApiKey
{
    public Guid Id {get ; init;}
    public Guid MerchantId {get; init;}
    public string KeyPrefix {get; init;} = default!;
    public string ShortId {get; init;} = default!;
    public string HashedSecret {get; init;} = default!;
    public string Environment {get; init;} = default!;
    public bool IsActive {get; init;}
    public DateTime CreatedAt {get; init;}

    public DateTime? LastUsedAt {get; init;}

}



