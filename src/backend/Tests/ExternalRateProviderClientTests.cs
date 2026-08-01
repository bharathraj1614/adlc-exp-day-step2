using System.Net;
using System.Text;
using Xunit;
using OuterloopLabApi.Services;
using OuterloopLabApi.Services.Internal;

namespace Tests;

public sealed class ExternalRateProviderClientTests
{
    [Fact]
    public async Task Normalizes_RatesProperty()
    {
        Environment.SetEnvironmentVariable("CURRENCY_API_BASE_URL", "https://example.test");

        var handler = new FakeHttpMessageHandler(async () =>
        {
            var json = "{\"date\":\"2026-08-01\",\"rates\":{\"EUR\":0.92}}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var client = new ExternalRateProviderClient(httpClient);

        NormalizedProviderRate result = await client.GetNormalizedRateAsync("USD", "EUR", CancellationToken.None);

        Assert.Equal(0.92m, result.Rate);
        Assert.NotNull(result.ProviderDate);
        Assert.Null(result.ProviderSequenceMarker);
    }

    [Fact]
    public async Task Normalizes_ConversionRatesProperty_CaseInsensitive_WithSequence()
    {
        Environment.SetEnvironmentVariable("CURRENCY_API_BASE_URL", "https://example.test");

        var handler = new FakeHttpMessageHandler(async () =>
        {
            var json = "{\"sequence\":\"abc\",\"conversion_rates\":{\"eur\":0.77}}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var client = new ExternalRateProviderClient(httpClient);

        NormalizedProviderRate result = await client.GetNormalizedRateAsync("USD", "EUR", CancellationToken.None);

        Assert.Equal(0.77m, result.Rate);
        Assert.Null(result.ProviderDate);
        Assert.Equal("abc", result.ProviderSequenceMarker);
    }

    [Fact]
    public async Task Throws_OnUnparseablePayload()
    {
        Environment.SetEnvironmentVariable("CURRENCY_API_BASE_URL", "https://example.test");

        var handler = new FakeHttpMessageHandler(async () =>
        {
            var json = "not-json";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var client = new ExternalRateProviderClient(httpClient);

        await Assert.ThrowsAsync<UpstreamProviderException>(() => client.GetNormalizedRateAsync("USD", "EUR", CancellationToken.None));
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<Task<HttpResponseMessage>> _responseFactory;

        public FakeHttpMessageHandler(Func<Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await _responseFactory();
        }
    }
}
