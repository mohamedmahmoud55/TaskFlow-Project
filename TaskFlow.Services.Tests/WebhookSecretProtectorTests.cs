using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using TaskFlow.Data.Entities;
using TaskFlow.Services.Security;

namespace TaskFlow.Services.Tests;

public sealed class WebhookSecretProtectorTests
{
    [Fact]
    public void ProtectAndUnprotectRoundTripsTheSecret()
    {
        var provider = CreateProvider();
        var protector = new DataProtectionWebhookSecretProtector(provider);

        var protectedSecret = protector.Protect("webhook-secret");

        Assert.True(protector.TryUnprotect(protectedSecret, out var secret));
        Assert.Equal("webhook-secret", secret);
    }

    [Fact]
    public void ProtectedValueDiffersFromPlaintext()
    {
        var provider = CreateProvider();
        var protector = new DataProtectionWebhookSecretProtector(provider);

        var protectedSecret = protector.Protect("webhook-secret");

        Assert.NotEqual("webhook-secret", protectedSecret);
        Assert.DoesNotContain("webhook-secret", protectedSecret, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidCiphertextFailsSafely()
    {
        var provider = CreateProvider();
        var protector = new DataProtectionWebhookSecretProtector(provider);

        var result = protector.TryUnprotect("not-protected-ciphertext", out var secret);

        Assert.False(result);
        Assert.Empty(secret);
    }

    [Fact]
    public void DifferentPurposeCannotDecryptThePayload()
    {
        var provider = CreateProvider();
        var webhookProtector = new DataProtectionWebhookSecretProtector(provider);
        var otherProtector = provider.CreateProtector("TaskFlow.Other.Purpose");
        var protectedSecret = webhookProtector.Protect("webhook-secret");

        Assert.Throws<CryptographicException>(() => otherProtector.Unprotect(protectedSecret));
    }

    [Fact]
    public void EntityDoesNotExposeAPlaintextSecretProperty()
    {
        Assert.Null(typeof(WebhookSubscription).GetProperty("Secret"));
        Assert.NotNull(typeof(WebhookSubscription).GetProperty("ProtectedSecret"));
    }

    private static IDataProtectionProvider CreateProvider()
    {
        return DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(Path.GetTempPath(), "TaskFlowTests", Guid.NewGuid().ToString("N"))));
    }
}
