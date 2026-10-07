using System.Security.Cryptography;

namespace OVSD.Core;

public static class Ids
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static string New(int length = 10) => RandomNumberGenerator.GetString(Alphabet, length);
}
