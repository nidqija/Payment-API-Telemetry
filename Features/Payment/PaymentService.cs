namespace Features.Payment;


public class PaymentService
{
    public void Execute(String userID , decimal amount)
    {
        Console.WriteLine($"Generating payment for user {userID} with amount {amount}");
    }
}