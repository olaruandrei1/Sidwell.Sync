using Sidwell.Sync.Domain.Helpers;

namespace Sidwell.Sync.UnitTests.Helpers;

public sealed class SymbolNormalizerTests
{
    [Theory]
    [InlineData("AAPL.US", "AAPL")]
    [InlineData("aapl.us", "aapl")]
    [InlineData("TLV.RO", "TLV.RO")]
    [InlineData("AAPL", "AAPL")]
    public void ForExternalApi_strips_us_suffix_only(string input, string expected)
    {
        Assert.Equal(expected, SymbolNormalizer.ForExternalApi(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForExternalApi_returns_input_unchanged_for_null_or_whitespace(string? input)
    {
        Assert.Equal(input, SymbolNormalizer.ForExternalApi(input!));
    }
}
