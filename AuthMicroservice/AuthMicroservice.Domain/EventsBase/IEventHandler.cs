using Calabonga.OperationResults;
using Confluent.Kafka;

namespace AuthMicroservice.Domain.EventsBase;

public interface IEventHandler<TKey, TValue>
{
    void Process(Message<TKey, TValue> message);

    Task<OperationResult<bool>> ProcessAsync(Message<TKey, TValue> message);
}