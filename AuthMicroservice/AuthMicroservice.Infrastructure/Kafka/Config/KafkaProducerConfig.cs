using Confluent.Kafka;
using System.Net;

namespace AuthMicroservice.Infrastructure.Kafka.Config;

public class KafkaProducerConfig : KafkaConfig
{
    public ProducerConfig ProducerConfig =>
        new ProducerConfig()
        {
            BootstrapServers = KafkaHost,
            ClientId = Dns.GetHostName()
        };
}