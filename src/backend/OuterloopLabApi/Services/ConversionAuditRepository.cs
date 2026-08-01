using Microsoft.Azure.Cosmos;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Services;

public sealed class ConversionAuditRepository : IConversionAuditRepository
{
    private readonly Container _container;

    public ConversionAuditRepository(Container container)
    {
        _container = container;
    }

    public Task SaveAsync(ConversionAuditRecord record, CancellationToken cancellationToken)
    {
        return _container.CreateItemAsync(record, new PartitionKey(record.ConversionId), cancellationToken: cancellationToken);
    }

    public async Task<ConversionAuditRecord?> GetAsync(string conversionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<ConversionAuditRecord>(conversionId, new PartitionKey(conversionId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
