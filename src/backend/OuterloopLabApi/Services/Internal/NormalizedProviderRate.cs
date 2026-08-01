namespace OuterloopLabApi.Services.Internal;

public sealed record NormalizedProviderRate(decimal Rate, DateTime? ProviderDate, string? ProviderSequenceMarker);
