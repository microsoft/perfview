using FastSerialization;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public static class DeserializerExtension
    {
        public static bool ReadByteAndVerify(this Deserializer deserializer, byte expectedByte, bool throwOnError = true)
        {
            return ReadAndVerify(() =>
            {
                var actualByte = deserializer.ReadByte();
                if (actualByte != expectedByte)
                {
                    throw new SerializationException($"The byte of position {deserializer.Current - 1} is {actualByte:x}. But it is expected to be {expectedByte:x}.");
                }
            }, throwOnError);
        }

        public static bool ReadBytesAndVerify(this Deserializer deserializer, byte[] expectedBytes, bool throwOnError = true)
        {
            return ReadAndVerify(() =>
            {
                foreach (byte expectedByte in expectedBytes)
                {
                    var actualByte = deserializer.ReadByte();
                    if (actualByte != expectedByte)
                    {
                        throw new SerializationException($"The byte of position {deserializer.Current - 1} is {actualByte:x}. But it is expected to be {expectedByte:x}.");
                    }
                }

            }, throwOnError);
        }

        public static bool ReadStringAndVerify(this Deserializer deserializer, string expectedString, bool throwOnError = true)
        {
            return ReadAndVerify(() =>
            {
                var current = deserializer.Current;
                var actualString = deserializer.ReadString();

                if (!string.Equals(actualString, expectedString, StringComparison.Ordinal))
                {
                    throw new SerializationException($"The string of position {current} is {actualString}. But it is expected to be {expectedString}.");
                }

            }, throwOnError);
        }

        public static bool ReadIntAndVerify(this Deserializer deserializer, int expectedValue, bool throwOnError = true)
        {
            return ReadAndVerify(() =>
            {
                var current = deserializer.Current;
                var actualValue = deserializer.ReadInt();

                if (actualValue != expectedValue)
                {
                    throw new SerializationException($"The value of position {current} is {actualValue}. But it is expected to be {expectedValue}.");
                }

            }, throwOnError);
        }

        public static string ReadNullTerminatedUnicodeString(this Deserializer deserializer)
        {
            StringBuilder sb = new StringBuilder();
            short value = deserializer.ReadInt16();
            while (value != 0)
            {
                sb.Append(Convert.ToChar(value));
                value = deserializer.ReadInt16();
            }

            return sb.ToString();
        }

        private static bool ReadAndVerify(Action verifier, bool throwOnError)
        {
            try
            {
                verifier();
            }
            catch
            {
                if (throwOnError)
                {
                    throw;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}