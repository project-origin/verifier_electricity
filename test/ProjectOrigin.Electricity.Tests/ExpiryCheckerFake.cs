using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Models;

namespace ProjectOrigin.Electricity.Tests;

public class ExpiryCheckerFake : IExpiryChecker
{
    private readonly bool _isExpired;

    public ExpiryCheckerFake(bool isExpired = false)
    {
        _isExpired = isExpired;
    }

    public bool IsExpired(GranularCertificate certificate)
    {
        return _isExpired;
    }
}
