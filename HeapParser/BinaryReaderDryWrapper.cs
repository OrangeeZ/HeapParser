using System;
using System.IO;
using System.Text;

namespace HeapParser
{
    public class BinaryReaderDryWrapper
    {
        public readonly BinaryReader Reader;

        public bool IsDryMode;

        private byte[] _byteBuffer = new byte[1024];
        private char[] _charBuffer = new char[1024];

        public BinaryReaderDryWrapper(BinaryReader reader)
        {
            Reader = reader;
        }

        public uint ReadUInt32()
        {
            return IsDryMode ? FastForwardReader<UInt32>(sizeof(UInt32)) : Reader.ReadUInt32();
        }

        public int ReadInt32()
        {
            return IsDryMode ? FastForwardReader<Int32>(sizeof(Int32)) : Reader.ReadInt32();
        }

        public string ReadString()
        {
            var stringLength = Reader.ReadInt32();

            if (IsDryMode)
            {
                FastForwardReader<byte>(stringLength);
                return string.Empty;
            }

            Reader.Read(_byteBuffer, 0, stringLength);
            Encoding.UTF8.GetChars(_byteBuffer, 0, stringLength, _charBuffer, 0);

            return new string(_charBuffer, 0, stringLength);
        }

        public ulong ReadUInt64()
        {
            return IsDryMode ? FastForwardReader<UInt64>(sizeof(UInt64)) : Reader.ReadUInt64();
        }

        public byte ReadByte()
        {
            return IsDryMode ? FastForwardReader<byte>(sizeof(byte)) : Reader.ReadByte();
        }

        public bool ReadBoolean()
        {
            return IsDryMode ? FastForwardReader<Boolean>(sizeof(Boolean)) : Reader.ReadBoolean();
        }

        public byte[] ReadBytes(int size)
        {
            return IsDryMode ? FastForwardReader<byte[]>(size) : Reader.ReadBytes(size);
        }

        public int ReadInt16()
        {
            return IsDryMode ? FastForwardReader<Int16>(sizeof(Int16)) : Reader.ReadInt16();
        }

        private T FastForwardReader<T>(int amount)
        {
            Reader.BaseStream.Seek(amount, SeekOrigin.Current);

            return default(T);
        }
    }
}