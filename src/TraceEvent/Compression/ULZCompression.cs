using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Tracing
{
    internal static class ULZCompression
    {
        private const int WindowBits = 17;
        private const int WindowSize = 1 << WindowBits;
        private const int WindowMask = WindowSize - 1;
        private const int MinMatch = 4;
        private const int HashBits = 19;
        private const int HashSize = 1 << HashBits;
        private const int Nil = -1;

        public static unsafe int Compress(ArraySegment<byte> input, int level)
        {
            var hashTable = new int[HashSize];
            var prev = new int[WindowSize];
            var output = new byte[input.Count];

            fixed (int* hashTablePtr = &hashTable[0])
            {
                fixed (int* prevPtr = &prev[0])
                {
                    fixed (byte* inputPtr = &input.Array[input.Offset])
                    {
                        fixed (byte* outputPtr = &output[0])
                        {
                            return Compress(hashTablePtr, prevPtr, inputPtr, input.Count, outputPtr, level);
                        }
                    }
                }
            }
        }

        public static unsafe ArraySegment<byte> Decompress(ArraySegment<byte> input, int decompressedSize)
        {
            byte[] output = new byte[decompressedSize * 2];
            fixed (byte* inputPtr = &input.Array[input.Offset])
            {
                fixed (byte* outputPtr = &output[0])
                {
                    int actualDecompressedSize = Decompress(inputPtr, input.Count, outputPtr, output.Length);
                    if (decompressedSize != actualDecompressedSize)
                    {
                        throw new Exception($"Unexpected decompressed size. Expected: {decompressedSize}, Actual: {actualDecompressedSize}");
                    }

                    return new ArraySegment<byte>(output, 0, decompressedSize);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint Hash32(byte* p)
        {
            return (Unsafe.ReadUnaligned<uint>(p) * 0x9E3779B9) >> (32 - HashBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WildCopy(byte* d, byte* s, int n)
        {
            Unsafe.WriteUnaligned(d, Unsafe.ReadUnaligned<ulong>(s));

            for (int i = 8; i < n; i += 8)
            {
                Unsafe.WriteUnaligned(d + i, Unsafe.ReadUnaligned<ulong>(s + i));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void EncodeMod(ref byte* p, uint x)
        {
            while (x >= 128)
            {
                x -= 128;
                *p++ = (byte)(128 + (x & 127));
                x >>= 7;
            }

            *p++ = (byte)x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint DecodeMod(ref byte* p)
        {
            uint x = 0;

            for (int i = 0; i <= 21; i += 7)
            {
                uint c = *p++;
                x += c << i;
                if (c < 128)
                {
                    break;
                }
            }

            return x;
        }

        private static unsafe int Decompress(byte* input, int inputLength, byte* output, int outputLength)
        {
            byte* op = output;
            byte* ip = input;
            byte* ipEnd = ip + inputLength;
            byte* opEnd = op + outputLength;

            while (ip < ipEnd)
            {
                int token = *ip++;

                if (token >= 32)
                {
                    int run = token >> 5;

                    if (run == 7)
                    {
                        run += (int)DecodeMod(ref ip);
                    }

                    if ((opEnd - op) < run || (ipEnd - ip) < run) // Overrun check
                    {
                        return -1;
                    }

                    WildCopy(op, ip, run);

                    op += run;
                    ip += run;

                    if (ip >= ipEnd)
                    {
                        break;
                    }
                }

                int len = (token & 15) + MinMatch;

                if (len == 15 + MinMatch)
                {
                    len += (int)DecodeMod(ref ip);
                }

                if (opEnd - op < len) // Overrun check
                {
                    return -1;
                }

                int dist = ((token & 16) << 12) + Unsafe.ReadUnaligned<ushort>(ip);
                ip += 2;
                byte* cp = op - dist;
                if (op - output < dist)
                {
                    return -1;
                }

                if (dist >= 8)
                {
                    WildCopy(op, cp, len);
                    op += len;
                }
                else
                {
                    *op++ = *cp++;
                    *op++ = *cp++;
                    *op++ = *cp++;
                    *op++ = *cp++;

                    while (len-- != 4)
                    {
                        *op++ = *cp++;
                    }
                }
            }

            return ip == ipEnd ? (int)(op - output) : -1;
        }

        private static unsafe int Compress(int* hashTable, int* prev, byte* input, int inputLength, byte* output, int level)
        {
            if (level < 1 || level > 9)
            {
                return -1;
            }

            int maxChain = level < 9 ? 1 << level : 1 << 13;

            for (int i = 0; i < HashSize; ++i)
            {
                hashTable[i] = Nil;
            }

            byte* op = output;
            int anchor = 0;

            int p = 0;
            while (p < inputLength)
            {
                int bestLen = 0;
                int dist = 0;

                int maxMatch = inputLength - p;
                if (maxMatch >= MinMatch)
                {
                    int limit = Math.Max(p - WindowSize, Nil);
                    int chainLen = maxChain;

                    int s = hashTable[Hash32(&input[p])];
                    while (s > limit)
                    {
                        if (input[s + bestLen] == input[p + bestLen] && Unsafe.ReadUnaligned<uint>(&input[s]) == Unsafe.ReadUnaligned<uint>(&input[p]))
                        {
                            int len = MinMatch;
                            while (len < maxMatch && input[s + len] == input[p + len])
                            {
                                ++len;
                            }

                            if (len > bestLen)
                            {
                                bestLen = len;
                                dist = p - s;

                                if (len == maxMatch)
                                {
                                    break;
                                }
                            }
                        }

                        if (--chainLen == 0)
                        {
                            break;
                        }

                        s = prev[s & WindowMask];
                    }
                }

                if (bestLen == MinMatch && (p - anchor) >= (7 + 128))
                {
                    bestLen = 0;
                }

                if (level >= 5 && bestLen >= MinMatch && bestLen < maxMatch && (p - anchor) != 6)
                {
                    int x = p + 1;
                    int targetLen = bestLen + 1;

                    int limit = Math.Max(x - WindowSize, Nil);
                    int chainLen = maxChain;

                    int s = hashTable[Hash32(&input[x])];
                    while (s > limit)
                    {
                        if (input[s + bestLen] == input[x + bestLen] && Unsafe.ReadUnaligned<uint>(&input[s]) == Unsafe.ReadUnaligned<uint>(&input[x]))
                        {
                            int len = MinMatch;

                            while (len < targetLen && input[s + len] == input[x + len])
                            {
                                ++len;
                            }

                            if (len == targetLen)
                            {
                                bestLen = 0;
                                break;
                            }
                        }

                        if (--chainLen == 0)
                        {
                            break;
                        }

                        s = prev[s & WindowMask];
                    }
                }

                if (bestLen >= MinMatch)
                {
                    int len = bestLen - MinMatch;
                    int token = ((dist >> 12) & 16) + Math.Min(len, 15);

                    if (anchor != p)
                    {
                        int run = p - anchor;

                        if (run >= 7)
                        {
                            *op++ = (byte)((7 << 5) + token);
                            EncodeMod(ref op, (uint)(run - 7));
                        }
                        else
                        {
                            *op++ = (byte)((run << 5) + token);
                        }

                        WildCopy(op, &input[anchor], run);
                        op += run;
                    }
                    else
                    {
                        *op++ = (byte)token;
                    }

                    if (len >= 15)
                    {
                        EncodeMod(ref op, (uint)(len - 15));
                    }

                    Unsafe.WriteUnaligned(op, (ushort)dist);
                    op += 2;

                    while (bestLen-- != 0)
                    {
                        uint h = Hash32(&input[p]);
                        prev[p & WindowMask] = hashTable[h];
                        hashTable[h] = p++;
                    }

                    anchor = p;
                }
                else
                {
                    uint h = Hash32(&input[p]);
                    prev[p & WindowMask] = hashTable[h];
                    hashTable[h] = p++;
                }
            }

            if (anchor != p)
            {
                int run = p - anchor;

                if (run >= 7)
                {
                    *op++ = 7 << 5;
                    EncodeMod(ref op, (uint)(run - 7));
                }
                else
                {
                    *op++ = (byte)(run << 5);
                }

                WildCopy(op, &input[anchor], run);
                op += run;
            }

            return (int)(op - output);
        }
    }
}
