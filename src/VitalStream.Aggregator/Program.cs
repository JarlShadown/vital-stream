using StackExchange.Redis;
using VitalStream.Aggregator;

var builder = WebApplication.CreateSlimBuilder(args);

var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddSingleton<TimeSeriesWriter>();
builder.Services.AddSingleton<ReadinessTracker>();
builder.Services.AddHostedService<AggregatorWorker>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok());
app.MapGet("/ready",   (ReadinessTracker rt) => rt.IsReady ? Results.Ok() : Results.StatusCode(503));

app.Run();