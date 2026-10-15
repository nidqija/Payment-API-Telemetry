using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Features.Observability;





public static class ApiTracingService
{
    public const string SourceName = "PaymentGateway.Core";

    public static readonly ActivitySource activitySource = new ActivitySource(SourceName);

    public static readonly Meter meter = new Meter(SourceName);

    public static readonly Counter<long> PaymentCount = 
    meter.CreateCounter<long>("payment.processed.count" , "payments" , "Total Payments Processed");

    public static readonly Histogram<double> PaymentAmount = 
    meter.CreateHistogram<double>("invoice.render.amount_ms" , "ms" , "Duration to generate pdf invoice");
}