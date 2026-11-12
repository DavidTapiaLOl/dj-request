using DjRequests.Api.Modules.Retention.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Retention;

public class RetentionOptionsValidationTests
{
    private static ValidateOptionsResult Validate(RetentionOptions options)
        => new RetentionOptionsValidator().Validate(Options.DefaultName, options);

    [Fact]
    public void Defaults_AreValid()
    {
        var result = Validate(new RetentionOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IntervalMinutes_NotPositive_Fails(int intervalMinutes)
    {
        var result = Validate(new RetentionOptions { IntervalMinutes = intervalMinutes });

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public void BatchSize_OutOfRange_Fails(int batchSize)
    {
        var result = Validate(new RetentionOptions { BatchSize = batchSize });

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void BatchSize_WithinRange_Succeeds(int batchSize)
    {
        var result = Validate(new RetentionOptions { BatchSize = batchSize });

        Assert.True(result.Succeeded);
    }
}
