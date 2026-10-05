using Features.Idempotency;
using Features.Payment;
using Npgsql;
using dotenv.net;

DotEnv.Load();


var builder = WebApplication.CreateBuilder(args);

var app_database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "app_db";
var app_user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
var app_password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "password";

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? $"Host=localhost;Port=5431;Database={app_database};Username={app_user};Password={app_password};SslMode=Disable;Include Error Detail=true;";


builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

builder.Services.AddScoped<IdempotencyService>();

var app = builder.Build();

app.MapGet("/", () => "Payment Gateway API is running!");

app.MapPaymentEndpoints();

app.Run();