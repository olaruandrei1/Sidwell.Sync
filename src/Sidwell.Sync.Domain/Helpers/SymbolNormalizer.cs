namespace Sidwell.Sync.Domain.Helpers;

public static class SymbolNormalizer
{
    public static string ForExternalApi(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return symbol;

        return symbol.EndsWith(".US", StringComparison.OrdinalIgnoreCase)
            ? symbol[..^3]
            : symbol;
    }
}
