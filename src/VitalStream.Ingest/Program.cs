using System.Text.Json.Serialization;
using VitalStream;
using VitalStream.DTOs;
using VitalStream.Shared.Models;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddOpenApi();
builder.Services.AddSingleton<VitalReadingProducer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/healthz", () => Results.Ok());
app.MapGet("/ready",   () => Results.Ok());

IngestEndPoints.AddIngestEndpoints(app);

app.Run();

[JsonSerializable(typeof(VitalReadingDto))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}