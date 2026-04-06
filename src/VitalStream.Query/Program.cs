using System.Text.Json.Serialization;
using StackExchange.Redis;
using VitalStream.Query;
using VitalStream.Shared.Enums;
using VitalStream.Shared.Models;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QueryJsonContext.Default));

builder.Services.AddOpenApi();

var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

builder.Services.AddSingleton<VitalStore>();
builder.Services.AddSingleton<SseManager>();
builder.Services.AddHostedService<RedisSubscriber>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/healthz", () => Results.Ok());
app.MapGet("/ready",   () => Results.Ok());

// Prometheus-style gauge for SSE connections — consumed by HPA custom metric adapter
app.MapGet("/metrics", (SseManager sse) =>
    Results.Text(
        $"# HELP sse_active_connections Current number of active SSE connections\n" +
        $"# TYPE sse_active_connections gauge\n" +
        $"sse_active_connections {sse.ActiveConnections}\n",
        "text/plain; version=0.0.4"));

QueryEndpoints.Map(app);

app.Run();

[JsonSerializable(typeof(VitalReading))]
[JsonSerializable(typeof(VitalSummary))]
[JsonSerializable(typeof(List<VitalTypeSummary>))]
[JsonSerializable(typeof(AlertAck))]
[JsonSerializable(typeof(VitalType))]
[JsonSerializable(typeof(VitalSeverity))]
internal partial class QueryJsonContext : JsonSerializerContext { }