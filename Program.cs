using Features.Idempotency;
using Features.Payment;
using Npgsql;
using dotenv.net;
using QuestPDF.Infrastructure;
using StackExchange.Redis;
using Features.Invoice;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Features.Webhook;

DotEnv.Load();


QuestPDF.Settings.License = LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);

var app_database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "app_db";
var app_user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
var app_password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "password";

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? $"Host=localhost;Port=5431;Database={app_database};Username={app_user};Password={app_password};SslMode=Disable;Include Error Detail=true;";


var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? "127.0.0.1:6379,abortConnect=false";


// define service name and version of app for OpenTelemetry
var serviceName = "PaymentGateway.Api";
var serviceVersion = "1.0.0";


builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString)
);

// register webhook dispatch service to program
builder.Services.AddHttpClient<WebhookDispatch>(client =>
   client.Timeout = TimeSpan.FromSeconds(10)
);


// add opentelemetry tracing and metrics
builder.Services.AddOpenTelemetry()

// configure resource with service name and version
.ConfigureResource(resource => resource
    .AddService(serviceName: serviceName , serviceVersion: serviceVersion))
    .WithTracing(tracing =>
    {

        // add the source name to perform tracing for the application
        tracing.AddSource("PaymentGateway.Core")
        .AddAspNetCoreInstrumentation(

            // filter out health check and dashboard endpoints from tracing
            // this is important to avoid noise in the traces and to focus on the actual output of the application
            // in this context , we need to filter out the health check and 
            // dashboard endpoints from tracing because they are not relevant to the actual output of the application and can create noise in the traces.
            options =>
            {
                options.RecordException= true;
                options.Filter = httpContext => 
                  !httpContext.Request.Path.StartsWithSegments("/health") &&
                  !httpContext.Request.Path.StartsWithSegments("/dashboard");
            })

            // add http client instrumentation to trace outgoing http requests
            .AddHttpClientInstrumentation()
            .AddNpgsql()

            // add the otlp exporter to export traces to the configured endpoint
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317");
            });
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("PaymentGateway.Core")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317");
        });
    });


builder.Services.AddScoped<IdempotencyService>();
builder.Services.AddScoped<InvoiceService>();



 

var app = builder.Build();


app.MapGet("/", () => "Hello World!");


app.MapPaymentEndpoints();
app.MapInvoiceEndpoints();

app.Run();