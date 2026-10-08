using Features.Idempotency;
using Features.Payment;
using Npgsql;
using dotenv.net;
using QuestPDF.Infrastructure;
using StackExchange.Redis;
using Features.Invoice;

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


builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString)
);

builder.Services.AddScoped<IdempotencyService>();
builder.Services.AddScoped<InvoiceService>();

var app = builder.Build();


app.MapGet("/", () => "Hello World!");


app.MapPaymentEndpoints();
app.MapInvoiceEndpoints();

app.Run();