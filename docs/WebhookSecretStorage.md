# Webhook Secret Storage

`WebhookSubscription.ProtectedSecret` contains an ASP.NET Core Data Protection payload, not a plaintext webhook secret. The protection purpose is `TaskFlow.WebhookSubscription.Secret.v1`; changing the purpose intentionally invalidates existing payloads.

## Data flow

1. A webhook secret is accepted by an application/service boundary.
2. `IWebhookSecretProtector.Protect` is called before the entity is persisted.
3. Only `ProtectedSecret` is stored in SQL Server.
4. A signing or verification operation calls `TryUnprotect` only when the plaintext secret is required.
5. Entities and ordinary queries never expose a plaintext secret.

Invalid ciphertext fails closed through `TryUnprotect`; plaintext fallback is intentionally not supported.

## Production key storage

Data Protection keys must survive application restarts and must be shared by all instances that need to decrypt webhook secrets. Configure `DataProtection:KeyRingPath` to a persistent, access-controlled location outside the application content directory, or replace the file-system key store with a managed key store.

For Azure production deployments, the recommended arrangement is:

- Persist the Data Protection key ring in Azure Blob Storage or another durable, access-controlled store.
- Protect the key ring encryption material with Azure Key Vault.
- Use managed identity and least-privilege access for the application.
- Restrict access to the key ring and rotate/revoke access through the deployment security process.

This repository does not claim Azure Key Vault or production key persistence is configured. The application startup requires an explicit key-ring path outside Development.

## Existing plaintext values

The schema migration renames `Secret` to `ProtectedSecret` and widens the column. It does not transform existing values. Existing values must be treated as potentially plaintext and must never be passed to `TryUnprotect` as though they were protected.

Before enabling the new code against a database containing existing secrets:

1. Take a verified backup and restrict access to the backfill process.
2. Stop webhook writes and delivery workers.
3. Read each legacy value through a controlled one-time process.
4. Protect it using the configured `IWebhookSecretProtector`.
5. Write only the protected payload to `ProtectedSecret`.
6. Verify successful unprotection and webhook signing.
7. Remove or securely destroy temporary plaintext copies and audit output.
8. Resume webhook processing only after the backfill is complete.

If the legacy value cannot be verified as plaintext, quarantine it for manual recovery; do not silently attempt multiple formats.
