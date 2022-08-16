using AuthMicroservice.Infrastructure.Kafka.Config;
using AuthMicroservice.Web.Definitions.Base;
using Confluent.Kafka;
using LightMicroserviceModule.EventsBase;
using Registration;

namespace AuthMicroservice.Web.Definitions.Kafka;

public class KafkaDefinition : AppDefinition
{
    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var isEnableKafka = bool.Parse(configuration["Kafka:IsEnable"]);
        if (!isEnableKafka)
        {
            return;
        }

        var config = configuration.GetSection("Kafka:Producer").Get<KafkaProducerConfig>();

        services.AddKafkaProducer<Null, RegistrationEvent>(config);
    }
}