using Codevoid.Utilities.OAuth;

namespace Codevoid.Test.Utilities.OAuth;

public sealed class ParameterEncoderTests
{
    [Fact]
    public void OneParameterAndValueEncodes()
    {
        var sampleData = new Dictionary<string, string>
        {
            { "a", "b" }
        };

        var result = ParameterEncoder.FormEncodeValues(sampleData);
        Assert.Equal("a=b", result); // Encoding string didn't match
    }

    [Fact]
    public void TwoParametersAndValueEncodesWithCorrectOrder()
    {
        var sampleData = new Dictionary<string, string>
        {
            { "b", "c%jkt" },
            { "a", "b" }
        };

        var result = ParameterEncoder.FormEncodeValues(sampleData);
        Assert.Equal("a=b&b=c%25jkt", result); // Encoding string didn't match
    }

    [Fact]
    public void ValuesAreEncodedAccordingToRFC3986()
    {
        var sampleData = new Dictionary<string, string>
        {
            { "!'()*", "*)('!" }
        };

        var result = ParameterEncoder.FormEncodeValues(sampleData);
        Assert.Equal("%21%27%28%29%2A=%2A%29%28%27%21", result); // Encoding string didn't match
    }

    [Fact]
    public void CustomDelimiterIsRespectedIfProvided()
    {
        var sampleData = new Dictionary<string, string>
        {
            { "b", "c%jkt" },
            { "a", "b" }
        };

        var result = ParameterEncoder.FormEncodeValues(sampleData, delimiter: ", ");
        Assert.Equal("a=b, b=c%25jkt", result); // Encoding string didn't match
    }

    [Fact]
    public void ValuesAreQuotedWhenRequested()
    {
        var sampleData = new Dictionary<string, string>
        {
            { "b", "c%jkt" },
            { "a", "b" }
        };

        var result = ParameterEncoder.FormEncodeValues(sampleData, shouldQuoteValues: true);
        Assert.Equal("a=\"b\"&b=\"c%25jkt\"", result); // Encoding string didn't match
    }
}