using System;
using System.IO;

namespace PhotoLocator.PictureFileFormats
{
    class OffsetStreamReader : Stream
    {
        readonly long _sourceOffset;
        readonly Stream _source;

        public OffsetStreamReader(Stream source)
        {
            _sourceOffset = source.Position;
            _source = source;
        }

        public override bool CanRead => _source.CanRead;

        public override bool CanSeek => _source.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _source.Length - _sourceOffset;

        public override long Position 
        {
            get => _source.Position - _sourceOffset;
            set => _source.Position = value + _sourceOffset;
        }

        public override int Read(byte[] buffer, int bufferOffset, int count)
        {
            return _source.Read(buffer, bufferOffset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return origin switch
            {
                SeekOrigin.Begin => _source.Seek(offset + _sourceOffset, SeekOrigin.Begin) - _sourceOffset,
                SeekOrigin.Current or SeekOrigin.End => _source.Seek(offset, origin) - _sourceOffset,
                _ => throw new ArgumentException(nameof(origin)),
            };
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
