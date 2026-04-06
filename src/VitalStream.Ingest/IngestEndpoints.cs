using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using VitalStream.DTOs;
using VitalStream.Shared.Models;
using VitalStream.Shared.Security;

namespace VitalStream;

public static class IngestEndPoints
{
    public static void AddIngestEndpoints(WebApplication app)
    {
        var apiGroup = app.MapGroup("/ingest");

        apiGroup.MapPost("/{deviceId}", async Task<Results<Ok<string>, UnprocessableEntity>> (
            string deviceId,
            HttpContext ctx,
            VitalReadingProducer producer,
            IConfiguration config,
            VitalReadingDto readingDto,
            CancellationToken ct) =>
        {
            // Read raw body before any deserialization so HMAC covers the exact bytes received
            ctx.Request.EnableBuffering();
            using var ms = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(ms, ct);
            var body = ms.ToArray();
            ctx.Request.Body.Position = 0;

            // 1. Validate HMAC
            var signature = ctx.Request.Headers["X-Signature-256"].FirstOrDefault();
            var deviceSecret = config[$"DeviceSecrets:{deviceId}"] ?? "";
            //TODO for dev purposes just ignore Signature
            // if (!HmacValidator.Validate(body, signature, deviceSecret))
            //     return TypedResults.UnprocessableEntity();

                

            // 3. ClinicalThresholds.Evaluate is called implicitly via VitalReading.Severity
            // 4. Publish enriched reading (includes computed Severity) to Kafka
            var payload = JsonSerializer.Serialize(readingDto, AppJsonSerializerContext.Default.VitalReadingDto);
            await producer.PublishAsync(deviceId, payload, ct);

            return TypedResults.Ok("Success");
        });

        apiGroup.MapPost("/batch/{deviceId}", async Task<Results<Ok<string>, UnprocessableEntity>> (
            string deviceId,
            HttpContext ctx,
            VitalReadingProducer producer,
            IConfiguration config,
            CancellationToken ct) =>
        {
            // Read raw body before any deserialization so HMAC covers the exact bytes received
            ctx.Request.EnableBuffering();
            using var ms = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(ms, ct);
            var body = ms.ToArray();
            ctx.Request.Body.Position = 0;

            // 1. Validate HMAC
            var signature = ctx.Request.Headers["X-Signature-256"].FirstOrDefault();
            var deviceSecret = config[$"DeviceSecrets:{deviceId}"] ?? "";
            if (!HmacValidator.Validate(body, signature, deviceSecret))
                return TypedResults.UnprocessableEntity();

            //2. Parse Payload 
            List<VitalReadingDto?>? reading;
            try
            {
                reading = [JsonSerializer.Deserialize(body, AppJsonSerializerContext.Default.VitalReadingDto)];
            }
            catch (JsonException)
            {
                return TypedResults.UnprocessableEntity();
            }

            if (reading.FirstOrDefault() is null || reading.FirstOrDefault()!.DeviceId != deviceId)
                return TypedResults.UnprocessableEntity();


            reading.ForEach(async read =>
            {
                var payload = JsonSerializer.Serialize(read, AppJsonSerializerContext.Default.VitalReadingDto);
                await producer.PublishAsync(deviceId, payload, ct);
            });
            return TypedResults.Ok("Success");
        });
    }
}