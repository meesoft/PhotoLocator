using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PhotoLocator.Metadata
{
    sealed class IfdDecoder : IDisposable
    {
        public enum FieldType : short
        {
            Byte = 1,
            Ascii = 2,
            Short = 3,
            Long = 4,
            Rational = 5,
            Undefined = 7,
            SShort = 8,
            SLong = 9,
            SRational = 10,
            Float = 11,
            Double = 12
        }

#pragma warning disable CA2213 // Disposable fields should be disposed
        readonly Stream _stream;
#pragma warning restore CA2213 // Disposable fields should be disposed
        readonly long _offset;
        readonly BinaryReader _reader;

        public IfdDecoder(Stream stream, long offset)
        {
            _stream = stream;
            _offset = offset;
            _reader = new BinaryReader(_stream, Encoding.ASCII, true);
        }

        public record IfdTag(ushort TagId, FieldType FieldType, int ValueCount, uint ValueOrOffset);

        public IEnumerable<IfdTag> EnumerateIfdTags()
        {
            var offset = _offset;
            var length = _stream.Length;
            do
            {
                _stream.Position = offset;
                var ifdFieldCount = _reader.ReadUInt16();
                for (var i = 0; i < ifdFieldCount; i++)
                {
                    if (_stream.Position + 12 > length)
                        yield break;
                    yield return new IfdTag(_reader.ReadUInt16(), (FieldType)_reader.ReadInt16(), _reader.ReadInt32(), _reader.ReadUInt32());
                }
                if (_stream.Position + 2 > length)
                    yield break;
                offset = _reader.ReadUInt32();
            }
            while (offset > _stream.Position && offset + 2 < length);
        }

        public string? DecodeStringTag(IfdTag tag)
        {
            Debug.Assert(tag.FieldType == FieldType.Ascii, "Expected field type 2 for string tag");
            if (tag.ValueCount > 4)
            {
                var previousPosition = _stream.Position;
                _stream.Position = _offset + tag.ValueOrOffset;
                var chars = _reader.ReadChars(tag.ValueCount);
                _stream.Position = previousPosition;
                return new string(chars).TrimEnd('\0');
            }
            Span<byte> buf = stackalloc byte[4];
            BitConverter.TryWriteBytes(buf, tag.ValueOrOffset);
            return Encoding.ASCII.GetString(buf[..tag.ValueCount]).TrimEnd('\0');
        }

        public uint[] DecodeUInt32Tag(IfdTag tag)
        {
            Debug.Assert(tag.FieldType == FieldType.Long, "Expected field type 4 for UInt32 tag");
            if (tag.ValueCount == 1)
                return [tag.ValueOrOffset];
            if (tag.ValueCount > 1 && _offset + tag.ValueOrOffset + tag.ValueCount * 4 <= _stream.Length)
            {
                var previousPosition = _stream.Position;
                _stream.Position = _offset + tag.ValueOrOffset;
                var value = new uint[tag.ValueCount];
                for (var i = 0; i < tag.ValueCount; i++)
                    value[i] = _reader.ReadUInt32();
                _stream.Position = previousPosition;
                return value;
            }
            return [];
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
