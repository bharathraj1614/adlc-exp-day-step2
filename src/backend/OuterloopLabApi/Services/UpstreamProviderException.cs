namespace OuterloopLabApi.Services;

public sealed class UpstreamProviderException : Exception
{
    public UpstreamProviderException(string message) : base(message)
    {
    }
}
