using OuterloopLabApi.Models;
using OuterloopLabApi.Services;
using OuterloopLabApi.Services.Internal;
using Xunit;

namespace Tests;

public sealed class CurrencyConversionServiceTests
{
    [Fact]
    public async Task CreatesImmutableAuditRecord_AndRoundsConvertedAmount()
    {
        var fixedNow = new DateTime(2026, 08, 01, 12, 34, 56, DateTimeKind.Utc);
        var provider = new FakeRateProvider(0.905m, new DateTime(2026, 08, 01, 0, 0, 0, DateTimeKind.Utc), "seq-1");
        var repo = new InMemoryAuditRepository();
        var service = new CurrencyConversionService(provider, repo, () => fixedNow);

        ConversionResponse response = await service.ConvertAndCreateAuditAsync(100m, "USD", "EUR", CancellationToken.None);

        Assert.NotEqual(Guid.Empty.ToString("D"), response.ConversionId);
        Assert.Equal(0.905m, response.Rate);
        Assert.Equal(90.5000m, response.ConvertedAmount);
        Assert.Equal(fixedNow, response.ExecutedAtUtc);
        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
        Assert.Equal(new DateTime(2026, 08, 01, 0, 0, 0, DateTimeKind.Utc), response.ProviderDate);
        Assert.Equal("seq-1", response.ProviderSequenceMarker);

        var stored = await repo.GetAsync(response.ConversionId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(response.ConversionId, stored!.ConversionId);
        Assert.Equal(90.5000m, stored.ConvertedAmount);
    }

    [Fact]
    public async Task GetAudit_ReturnsNull_WhenMissing()
    {
        var provider = new FakeRateProvider(0.9m, null, null);
        var repo = new InMemoryAuditRepository();
        var service = new CurrencyConversionService(provider, repo, () => DateTime.UtcNow);

        var record = await service.GetAuditAsync(Guid.NewGuid().ToString("D"), CancellationToken.None);
        Assert.Null(record);
    }

    private sealed class FakeRateProvider : IExternalRateProviderClient
    {
        private readonly decimal _rate;
        private readonly DateTime? _providerDate;
        private readonly string? _providerSequence;

        public FakeRateProvider(decimal rate, DateTime? providerDate, string? providerSequence)
        {
            _rate = rate;
            _providerDate = providerDate;
            _providerSequence = providerSequence;
        }

        public Task<NormalizedProviderRate> GetNormalizedRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NormalizedProviderRate(_rate, _providerDate, _providerSequence));
        }
    }

    private sealed class InMemoryAuditRepository : IConversionAuditRepository
    {
        private readonly Dictionary<string, ConversionAuditRecord> _store = new();

        public Task SaveAsync(ConversionAuditRecord record, CancellationToken cancellationToken)
        {
            _store[record.ConversionId] = record;
            return Task.CompletedTask;
        }

        public Task<ConversionAuditRecord?> GetAsync(string conversionId, CancellationToken cancellationToken)
        {
            _store.TryGetValue(conversionId, out var record);
            return Task.FromResult(record);
        }
    }
}
