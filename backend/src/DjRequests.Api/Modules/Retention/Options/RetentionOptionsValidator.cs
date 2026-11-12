using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Retention.Options;

public sealed class RetentionOptionsValidator : IValidateOptions<RetentionOptions>
{
    public const int MaxBatchSize = 1000;

    public ValidateOptionsResult Validate(string? name, RetentionOptions options)
    {
        if (options.IntervalMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("Retention:IntervalMinutes debe ser mayor que 0.");
        }

        if (options.BatchSize <= 0 || options.BatchSize > MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"Retention:BatchSize debe estar entre 1 y {MaxBatchSize}.");
        }

        return ValidateOptionsResult.Success;
    }
}
