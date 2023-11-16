using System;

namespace PerfView.Utilities
{
    internal static class RandomNumberGenerator
    {
#if !NET
        private static System.Security.Cryptography.RNGCryptoServiceProvider rngCsp = new();
#endif

        internal static double GetDouble()
        {
            // As described in http://prng.di.unimi.it/:
            // "A standard double (64-bit) floating-point number in IEEE floating point format has 52 bits of significand,
            //  plus an implicit bit at the left of the significand. Thus, the representation can actually store numbers with
            //  53 significant binary digits. Because of this fact, in C99 a 64-bit unsigned integer x should be converted to
            //  a 64-bit double using the expression
            //  (x >> 11) * 0x1.0p-53"
            return (GetUInt64() >> 11) * (1.0 / (1ul << 53));
        }

        private static ulong GetUInt64()
        {
#if NET
            Span<byte> data = stackalloc byte[8];
            System.Security.Cryptography.RandomNumberGenerator.Fill(data);
            return BitConverter.ToUInt64(data);
#else
            byte[] data = new byte[8];
            rngCsp.GetBytes(data);
            return BitConverter.ToUInt64(data, 0);
#endif
        }
    }
}
