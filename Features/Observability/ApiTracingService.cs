using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Features.Observability;





public static class ApiTracingService
{

    // define the source name for the activity source and meter
    public const string SourceName = "PaymentGateway.Core";

    // create an activity source for tracing
    public static readonly ActivitySource ActivitySource = new ActivitySource(SourceName);

    // init a new meter for metrics collection
    // this is used to create counters and histograms for various metrics related to payments and invoices
    public static readonly Meter Meter = new Meter(SourceName);

    // define payment and invoice related metrics using counters and histograms
    // we use counter for payment as it is a discrete event that can be counted, 
    public static readonly Counter<long> PaymentCount = 
    Meter.CreateCounter<long>("payment.processed.count" , "payments" , "Total Payments Processed");


    // while we use histogram for invoice rendering duration as it is a continuous measurement that can vary over time
    public static readonly Histogram<double> PaymentInvoiceDuration = 
    Meter.CreateHistogram<double>("invoice.render.amount_ms" , "ms" , "Duration to generate pdf invoice");

    // something that is counted , we use counter
    public static readonly Counter<long> InvoiceCacheHitCount = 
    Meter.CreateCounter<long>("invoice.cache.hit.count" , "hits" , "Total Invoice Cache Hits");

    public static readonly Counter<long> InvoiceCacheMissCount =
    Meter.CreateCounter<long>("invoice.cache.miss.count" , "misses" , "Total Invoice Cache Misses");

    // something that is measured ( such as time , latency , size , etc) we use histogram
    public static readonly Histogram<double> InvoiceRenderDuration =
    Meter.CreateHistogram<double>("invoice.render.duration_ms" , "ms" , "Duration to render pdf invoice");
}