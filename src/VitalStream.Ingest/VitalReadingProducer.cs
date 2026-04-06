using Confluent.Kafka;

namespace VitalStream;

public sealed class VitalReadingProducer : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public VitalReadingProducer(IConfiguration config)
    {
        _topic = config["Kafka:Topic"] ?? "vital-readings";

        var producerConfig = new ProducerConfig     
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092",
            // EnableIdempotence requires Acks.All at the librdkafka level;
            // setting Acks.Leader here will cause a KafkaException on startup.
            // Use Acks.All for idempotent producers, or remove EnableIdempotence.
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public Task PublishAsync(string deviceId, string payload, CancellationToken ct = default) =>
        _producer.ProduceAsync(_topic,
            new Message<string, string> { Key = deviceId, Value = payload }, ct);

    public void Dispose() => _producer.Dispose();
}