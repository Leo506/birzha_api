using Calabonga.OperationResults;

namespace LightMicroserviceModule.EventsBase;

public interface IEventProducer<TKey, TValue>
{
    Task<OperationResult<bool>> ProduceAsync(TKey key, TValue value);
}