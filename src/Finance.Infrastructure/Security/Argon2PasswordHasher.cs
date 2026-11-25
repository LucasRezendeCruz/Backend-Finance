using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Finance.Application.Common.Interfaces;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Finance.Infrastructure.Security;

public sealed class Argon2PasswordHasher(IOptions<Argon2Options> options) : IPasswordHasher
{
    private readonly Argon2Options _options = options.Value;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] salt = RandomNumberGenerator.GetBytes(_options.SaltSizeBytes);
        byte[] hash = ComputeHash(
            password,
            salt,
            _options.MemorySizeKb,
            _options.Iterations,
            _options.DegreeOfParallelism,
            _options.HashSizeBytes);

        return $"$argon2id$v=19$m={_options.MemorySizeKb},t={_options.Iterations},p={_options.DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(encodedHash))
            return false;

        if (!TryParsePhc(
                encodedHash,
                out int memorySizeKb,
                out int iterations,
                out int parallelism,
                out byte[] salt,
                out byte[] expectedHash))
        {
            return false;
        }

        byte[] computedHash = ComputeHash(
            password,
            salt,
            memorySizeKb,
            iterations,
            parallelism,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static byte[] ComputeHash(
        string password,
        byte[] salt,
        int memorySizeKb,
        int iterations,
        int degreeOfParallelism,
        int hashSizeBytes)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = memorySizeKb,
                Iterations = iterations,
                DegreeOfParallelism = degreeOfParallelism
            };

            return argon2.GetBytes(hashSizeBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static bool TryParsePhc(
        string encodedHash,
        out int memorySizeKb,
        out int iterations,
        out int parallelism,
        out byte[] salt,
        out byte[] expectedHash)
    {
        memorySizeKb = 0;
        iterations = 0;
        parallelism = 0;
        salt = [];
        expectedHash = [];

        string[] parts = encodedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            return false;

        if (!string.Equals(parts[0], "argon2id", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "v=19", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (string param in parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = param.Split('=', 2);
            if (kv.Length != 2 || !int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
                return false;

            switch (kv[0])
            {
                case "m":
                    memorySizeKb = val;
                    break;
                case "t":
                    iterations = val;
                    break;
                case "p":
                    parallelism = val;
                    break;
                default:
                    return false;
            }
        }

        if (memorySizeKb < 8 || iterations < 1 || parallelism < 1)
            return false;

        return TryDecodeBase64(parts[3], out salt) &&
               TryDecodeBase64(parts[4], out expectedHash) &&
               salt.Length > 0 &&
               expectedHash.Length > 0;
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value))
            return false;

        int mod4 = value.Length % 4;
        string padded = mod4 == 0 ? value : value + new string('=', 4 - mod4);

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
