using System;
using System.Linq;
using Microsoft.Extensions.Options;

namespace ProjectOrigin.Electricity.Options;

public class NetworkOptionsValidator : IValidateOptions<NetworkOptions>
{
    public ValidateOptionsResult Validate(string? name, NetworkOptions options)
    {
        if (options.Areas is null || options.Areas.Count == 0)
            return ValidateOptionsResult.Fail("No Issuer areas configured.");

        if (options.Areas.Any(x => x.Value.IssuerKeys is null || x.Value.IssuerKeys.Count == 0))
            return ValidateOptionsResult.Fail("No Issuer keys configured.");

        if (options.Registries is null || options.Registries.Count == 0)
            return ValidateOptionsResult.Fail("No registries configured.");

        foreach (var registry in options.Registries)
        {
            if (!(Uri.TryCreate(registry.Value.Url, UriKind.Absolute, out var uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)))
            {
                return ValidateOptionsResult.Fail($"Invalid URL address specified for registry ”{registry.Key}”");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
