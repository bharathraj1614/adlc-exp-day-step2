using System.Text.Json.Serialization;

namespace OuterloopLabApi.Models;

public sealed class ConversionAuditRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // Partition key.
    public string ConversionId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;

    public decimal Rate { get; set; }
    public decimal ConvertedAmount { get; set; }

    public DateTime? ProviderDate { get; set; }
    public string? ProviderSequenceMarker { get; set; }

    public DateTime ExecutedAtUtc { get; set; }
}
