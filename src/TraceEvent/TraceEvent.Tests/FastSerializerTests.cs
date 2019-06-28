using FastSerialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TraceEventTests
{
    public class FastSerializerTests
    {
        [Fact]
        public void ParseStreamLabel()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            WriteString(writer, "!FastSerialization.1");
            writer.Write(0);
            writer.Write(19);
            writer.Write(1_000_000);
            writer.Write(0xf1234567);

            ms.Position = 0;
            Deserializer d = new Deserializer(new PinnedStreamReader(ms), "name");
            Assert.Equal((StreamLabel)0, d.ReadLabel());
            Assert.Equal((StreamLabel)19, d.ReadLabel());
            Assert.Equal((StreamLabel)1_000_000, d.ReadLabel());
            Assert.Equal((StreamLabel)0xf1234567, d.ReadLabel());
        }

        private void WriteString(BinaryWriter writer, string val)
        {
            writer.Write(val.Length);
            writer.Write(Encoding.UTF8.GetBytes(val));
        }
    }
}
