using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

using Syroot.BinaryData;
using Syroot.BinaryData.Memory;
using System.Buffers;

namespace XenoTools.Resources
{
    public class ResourceObject<T>
    {
        // Header - ml::DevFileUtilNx::readFile
        public byte[] ReadResource(BinaryStream bs)
        {
            if (bs.Length < 0x30)
            {
                bs.Position = 0;
                return bs.ReadBytes((int)bs.Length);
            }

            byte[] header = bs.ReadBytes(0x30);
            SpanReader headerReader = new SpanReader(header);

            if (headerReader.ReadUInt32() == 0x31636278)
            {
                uint version = headerReader.ReadUInt32();
                uint decompressedSize = headerReader.ReadUInt32();
                uint compressedSize = headerReader.ReadUInt32();
                uint hash = headerReader.ReadUInt32();

                const int BufferSize = 0x80000;
                byte[] buf = ArrayPool<byte>.Shared.Rent(BufferSize);

                using DeflateStream ds = new DeflateStream(bs, CompressionMode.Decompress);
                bs.Position += 0x02; // Skip zlib magic

                using var ms = new MemoryStream();

                while (decompressedSize > 0)
                {
                    int toRead = (int)Math.Min(decompressedSize, BufferSize);

                    uint nIoRead = (uint)ds.Read(buf, 0, toRead);
                    if (nIoRead == 0)
                        throw new IOException("Weird");

                    ms.Write(buf, 0, (int)nIoRead);

                    decompressedSize -= nIoRead;
                }

                return ms.ToArray();
            }
            else
            {
                bs.Position = 0;
                return bs.ReadBytes((int)bs.Length);
            }
        }
    }
}
