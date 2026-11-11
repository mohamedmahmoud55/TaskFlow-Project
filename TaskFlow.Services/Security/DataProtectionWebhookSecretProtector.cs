using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace TaskFlow.Services.Security;

public sealed class DataProtectionWebhookSecretProtector : IWebhookSecretProtector
{
    public const string Purpose = "TaskFlow.WebhookSubscription.Secret.v1";

    private readonly IDataProtector protector;

    public DataProtectionWebhookSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return protector.Protect(secret);
    }

    public bool TryUnprotect(string protectedSecret, out string secret)
    {
        secret = string.Empty;

        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            return false;
        }

        try
        {
            secret = protector.Unprotect(protectedSecret);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
