using System.Net;
using System.Net.Http;
using System.Text.Json;

using OuterloopLabApi.Services.Internal;

namespace OuterloopLabApi.Services;

public interface IExternalRateProviderClient
{
    Task<NormalizedProviderRate> GetNormalizedRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken);
}

public sealed class ExternalRateProviderClient : IExternalRateProviderClient
{
    private readonly HttpClient _httpClient;

    public ExternalRateProviderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<NormalizedProviderRate> GetNormalizedRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        var baseUrl = Environment.GetEnvironmentVariable("CURRENCY_API_BASE_URL") ?? "https://frankfurter.dev";
        baseUrl = baseUrl.TrimEnd('/');

        // Frankfurter-compatible endpoint; response parsing must remain schema-flexible.
        var requestUri = $"{baseUrl}/latest?from={Uri.EscapeDataString(fromCurrency)}&to={Uri.EscapeDataString(toCurrency)}";

        HttpResponseMessage? response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw new UpstreamProviderException("Currency provider timed out.");
        }
        catch (HttpRequestException)
        {
            throw new UpstreamProviderException("Currency provider is unavailable.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamProviderException("Currency provider returned a non-success response.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            JsonDocument doc;
            try
            {
                doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                throw new UpstreamProviderException("Currency provider returned an unparseable payload.");
            }

            using (doc)
            {
                var root = doc.RootElement;

            DateTime? providerDate = null;
            if (TryGetStringProperty(root, new[] { "date", "provider_date", "effective_date" }, out var dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
            {
                providerDate = parsedDate;
            }

            string? providerSequenceMarker = null;
            if (TryGetStringProperty(root, new[] { "sequence", "sequence_marker", "provider_sequence" }, out var seqStr))
            {
                providerSequenceMarker = seqStr;
            }

                decimal rate = ExtractRate(root, toCurrency);

                return new NormalizedProviderRate(rate, providerDate, providerSequenceMarker);
            }
        }
    }

    private static bool TryGetStringProperty(JsonElement root, string[] propertyNames, out string value)
    {
        foreach (var propName in propertyNames)
        {
            if (root.TryGetProperty(propName, out var el) && el.ValueKind == JsonValueKind.String)
            {
                value = el.GetString() ?? string.Empty;
                return true;
            }
            if (root.TryGetProperty(propName, out el) && (el.ValueKind == JsonValueKind.Number || el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False))
            {
                value = el.ToString();
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static decimal ExtractRate(JsonElement root, string toCurrency)
    {
        // Support multiple shapes without hardcoding a single response schema.
        if (!TryGetObjectProperty(root, new[] { "rates", "conversion_rates" }, out var ratesElement))
        {
            // Fallback: some providers might return the rate directly.
            if (root.TryGetProperty("rate", out var rateEl) && rateEl.TryGetDecimal(out var directRate))
            {
                return directRate;
            }

            if (root.TryGetProperty("conversion_rate", out var rateEl2) && rateEl2.TryGetDecimal(out var directRate2))
            {
                return directRate2;
            }

            throw new UpstreamProviderException("Currency provider payload did not contain rate data.");
        }

        // Try exact currency code, then case-insensitive scan.
        if (ratesElement.ValueKind == JsonValueKind.Object)
        {
            if (ratesElement.TryGetProperty(toCurrency, out var rateEl) && rateEl.TryGetDecimal(out var r))
            {
                return r;
            }

            foreach (var prop in ratesElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, toCurrency, StringComparison.OrdinalIgnoreCase) && prop.Value.TryGetDecimal(out var r2))
                {
                    return r2;
                }
            }
        }

        throw new UpstreamProviderException("Currency provider did not include a rate for the requested target currency.");
    }

    private static bool TryGetObjectProperty(JsonElement root, string[] propertyNames, out JsonElement element)
    {
        foreach (var propName in propertyNames)
        {
            if (root.TryGetProperty(propName, out element) && element.ValueKind == JsonValueKind.Object)
            {
                return true;
            }
        }

        element = default;
        return false;
    }
}
