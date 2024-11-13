using ProjectOrigin.Electricity.Models;

namespace ProjectOrigin.Electricity.Interfaces;

public interface IExpiryChecker
{
    public bool IsExpired(GranularCertificate certificate);
}
