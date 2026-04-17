using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Sodium;
using VaultArc.Models;

namespace VaultArc.Archive.Arc;

internal static class ArcCrypto
{
    internal const int KeyLength = 32;
    internal const int SaltLength = 16;
    internal const int Argon2Iterations = 3;
    internal const int Argon2MemoryKb = 65536;
    internal const int Argon2Parallelism = 4;
    internal const int Pbkdf2Iterations = 210_000;

    internal static byte[] CreateRandom(int byteCount)
    {
        var bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    internal static byte[] DeriveKey(
        string password,
        ArcEncryptionProfileKind profile,
        byte[] salt,
        int iterations,
        int memoryKb,
        int parallelism)
    {
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        if (profile == ArcEncryptionProfileKind.XChaCha20Argon2id)
        {
            var argon = new Argon2id(passwordBytes)
            {
                Salt = salt,
                Iterations = iterations,
                MemorySize = memoryKb,
                DegreeOfParallelism = parallelism
            };
            return argon.GetBytes(KeyLength);
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeyLength);
    }

    internal static byte[] Encrypt(
        byte[] plain,
        byte[] key,
        ArcEncryptionProfileKind profile,
        byte[] nonce,
        byte[] aad)
    {
        return profile switch
        {
            ArcEncryptionProfileKind.XChaCha20Argon2id => SecretAeadXChaCha20Poly1305.Encrypt(plain, nonce, key, aad),
            ArcEncryptionProfileKind.AesGcmPbkdf2 => EncryptAesGcm(plain, key, nonce, aad),
            _ => throw new InvalidOperationException($"Unsupported profile: {profile}")
        };
    }

    internal static byte[] Decrypt(
        byte[] cipher,
        byte[] key,
        ArcEncryptionProfileKind profile,
        byte[] nonce,
        byte[] aad)
    {
        return profile switch
        {
            ArcEncryptionProfileKind.XChaCha20Argon2id => SecretAeadXChaCha20Poly1305.Decrypt(cipher, nonce, key, aad),
            ArcEncryptionProfileKind.AesGcmPbkdf2 => DecryptAesGcm(cipher, key, nonce, aad),
            _ => throw new InvalidOperationException($"Unsupported profile: {profile}")
        };
    }

    internal static int GetNonceLength(ArcEncryptionProfileKind profile) =>
        profile == ArcEncryptionProfileKind.XChaCha20Argon2id ? 24 : 12;

    private static byte[] EncryptAesGcm(byte[] plain, byte[] key, byte[] nonce, byte[] aad)
    {
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag, aad);

        var output = new byte[cipher.Length + tag.Length];
        Buffer.BlockCopy(cipher, 0, output, 0, cipher.Length);
        Buffer.BlockCopy(tag, 0, output, cipher.Length, tag.Length);
        return output;
    }

    private static byte[] DecryptAesGcm(byte[] cipherAndTag, byte[] key, byte[] nonce, byte[] aad)
    {
        if (cipherAndTag.Length < 16)
        {
            throw new CryptographicException("Ciphertext is too short.");
        }

        var cipherLength = cipherAndTag.Length - 16;
        var cipher = new byte[cipherLength];
        var tag = new byte[16];
        Buffer.BlockCopy(cipherAndTag, 0, cipher, 0, cipherLength);
        Buffer.BlockCopy(cipherAndTag, cipherLength, tag, 0, tag.Length);

        var plain = new byte[cipherLength];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain, aad);
        return plain;
    }
}
