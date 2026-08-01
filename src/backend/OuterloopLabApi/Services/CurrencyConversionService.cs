using OuterloopLabApi.Models;
using OuterloopLabApi.Services.Internal;

namespace OuterloopLabApi.Services;

public interface IConversionAuditRepository
{
    Task SaveAsync(ConversionAuditRecord record, CancellationToken cancellationToken);
    Task<ConversionAuditRecord?> GetAsync(string conversionId, CancellationToken cancellationToken);
}

public sealed class CurrencyConversionService : ICurrencyConversionService
{
    private readonly IExternalRateProviderClient _rateProvider;
    private readonly IConversionAuditRepository _auditRepository;
    private readonly Func<DateTime> _utcNow;

    public CurrencyConversionService(IExternalRateProviderClient rateProvider, IConversionAuditRepository auditRepository, Func<DateTime> utcNow)
    {
        _rateProvider = rateProvider;
        _auditRepository = auditRepository;
        _utcNow = utcNow;
    }

    public async Task<ConversionResponse> ConvertAndCreateAuditAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        var normalized = await _rateProvider.GetNormalizedRateAsync(fromCurrency, toCurrency, cancellationToken);
        var conversionId = Guid.NewGuid().ToString("D");
        var executedAtUtc = _utcNow();

        var convertedAmount = Round4(amount * normalized.Rate);
        var rate = normalized.Rate;

        var record = new ConversionAuditRecord
        {
            Id = conversionId,
            ConversionId = conversionId,
            Amount = amount,
            FromCurrency = fromCurrency,
            ToCurrency = toCurrency,
            Rate = rate,
            ConvertedAmount = convertedAmount,
            ProviderDate = normalized.ProviderDate,
            ProviderSequenceMarker = normalized.ProviderSequenceMarker,
            ExecutedAtUtc = executedAtUtc
        };

        await _auditRepository.SaveAsync(record, cancellationToken);

        return ToResponse(record);
    }

    public async Task<ConversionResponse?> GetAuditAsync(string conversionId, CancellationToken cancellationToken)
    {
        var record = await _auditRepository.GetAsync(conversionId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        return ToResponse(record);
    }

    private static ConversionResponse ToResponse(ConversionAuditRecord record)
    {
        return new ConversionResponse
        {
            ConversionId = record.ConversionId,
            Amount = record.Amount,
            FromCurrency = record.FromCurrency,
            ToCurrency = record.ToCurrency,
            Rate = record.Rate,
            ConvertedAmount = record.ConvertedAmount,
            ProviderDate = record.ProviderDate,
            ProviderSequenceMarker = record.ProviderSequenceMarker,
            ExecutedAtUtc = record.ExecutedAtUtc
        };
    }

    private static decimal Round4(decimal value)
    {
        return Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
