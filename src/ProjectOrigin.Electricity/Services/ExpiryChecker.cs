using System;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Options;

namespace ProjectOrigin.Electricity.Services;

public class ExpiryChecker : IExpiryChecker
{
    private readonly IOptions<NetworkOptions> _options;

    public ExpiryChecker(IOptions<NetworkOptions> options)
    {
        _options = options;
    }

    public bool IsExpired(GranularCertificate certificate)
    {
        if (_options.Value.DaysBeforeCertificatesExpire == null)
        {
            return false;
        }
        var expiryDate = certificate.Period.End.ToDateTimeOffset().AddDays(_options.Value.DaysBeforeCertificatesExpire.Value);

        return DateTimeOffset.UtcNow > expiryDate;
    }
}
