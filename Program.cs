using TelephoneID.Services;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add controllers (Web API)
builder.Services.AddControllers();
// Register application services for dependency injection
builder.Services.AddSingleton<CallStreamService>();
builder.Services.AddSingleton<TranscriptionService>();
builder.Services.AddSingleton<FraudDetectionService>();
builder.Services.AddSingleton<TwilioCallService>();
builder.Services.AddSingleton<StorageService>();

// Optionally, configure HTTP client factory if FraudDetectionService will use it
builder.Services.AddHttpClient();

// Enable Application Insights (for minimal logging/monitoring, if instrumentation key is provided in config)
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Enable WebSocket support
app.UseWebSockets();

// Use routing and map controller endpoints
app.MapControllers();

app.Run();
