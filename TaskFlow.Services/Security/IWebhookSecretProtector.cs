namespace TaskFlow.Services.Security;

public interface IWebhookSecretProtector
{
    string Protect(string secret);

    bool TryUnprotect(string protectedSecret, out string secret);
}
