using OuterloopLabApi.Models;

namespace OuterloopLabApi.Services;

public interface ICurrencyConversionService
{
    Task<ConversionResponse> ConvertAndCreateAuditAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken);
    Task<ConversionResponse?> GetAuditAsync(string conversionId, CancellationToken cancellationToken);
}
