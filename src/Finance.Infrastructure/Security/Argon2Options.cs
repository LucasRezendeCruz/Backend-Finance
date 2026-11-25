namespace Finance.Infrastructure.Security;

public sealed class Argon2Options
{
    public const string SectionName = "Security:Argon2";

    public int MemorySizeKb { get; set; } = 65536;
    public int Iterations { get; set; } = 3;
    public int DegreeOfParallelism { get; set; } = 2;
    public int SaltSizeBytes { get; set; } = 16;
    public int HashSizeBytes { get; set; } = 32;
}
