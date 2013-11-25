using System;
using System.IO;

namespace Noxico
{
	public class CryptStream : Stream
	{
		public virtual Stream BaseStream { get; private set; }

		public CryptStream(Stream stream)
		{
			BaseStream = stream;
		}

		public override bool CanRead
		{
			get { return BaseStream.CanRead; }
		}

		public override bool CanSeek
		{
			get { return BaseStream.CanSeek; }
		}

		public override bool CanWrite
		{
			get { return BaseStream.CanWrite; }
		}

		public override void Flush()
		{
			BaseStream.Flush();
		}

		public override long Length
		{
			get { return BaseStream.Length; }
		}

		public override long Position
		{
			get
			{
				return BaseStream.Position;
			}
			set
			{
				BaseStream.Position = value;
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var cb = new Byte[count];
			var j = BaseStream.Read(cb, 0, count);
			for (var i = 0; i < count; i++)
				buffer[i + offset] = (byte)(cb[i] ^ 0x80);
			return j;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return BaseStream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			BaseStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var cb = new byte[count];
			for (int i = 0; i < count; i++)
				cb[i] = (byte)(buffer[i + offset] ^ 0x80);
			BaseStream.Write(cb, 0, count);
		}
	}
}
