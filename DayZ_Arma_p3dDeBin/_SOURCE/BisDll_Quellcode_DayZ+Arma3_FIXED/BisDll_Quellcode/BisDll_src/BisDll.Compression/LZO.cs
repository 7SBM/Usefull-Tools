using System;
using System.IO;

namespace BisDll.Compression;

public static class LZO
{
	private static readonly uint M2_MAX_OFFSET = 2048u;

	public unsafe static uint decompress(byte* input, byte* output, uint expectedSize)
	{
		byte* ptr = output + expectedSize;
		byte* ptr2 = output;
		byte* ptr3 = input;
		if (*ptr3 <= 17)
		{
			goto IL_0050;
		}
		uint num = (uint)(*(ptr3++) - 17);
		if (num >= 4)
		{
			if (ptr - ptr2 < num)
			{
				throw new OverflowException("Outpur Overrun");
			}
			do
			{
				*(ptr2++) = *(ptr3++);
			}
			while (--num != 0);
			goto IL_00f2;
		}
		goto IL_037d;
		IL_037d:
		if (ptr - ptr2 < num)
		{
			throw new OverflowException("Output Overrun");
		}
		*(ptr2++) = *(ptr3++);
		if (num > 1)
		{
			*(ptr2++) = *(ptr3++);
			if (num > 2)
			{
				*(ptr2++) = *(ptr3++);
			}
		}
		num = *(ptr3++);
		goto IL_0169;
		IL_0169:
		byte* ptr4;
		if (num >= 64)
		{
			ptr4 = ptr2 - 1;
			ptr4 -= (num >> 2) & 7;
			ptr4 -= *(ptr3++) << 3;
			num = (num >> 5) - 1;
			if (ptr4 < output || ptr4 >= ptr2)
			{
				throw new OverflowException("Lookbehind Overrun");
			}
			if (ptr - ptr2 < num + 2)
			{
				throw new OverflowException("Output Overrun");
			}
		}
		else
		{
			if (num >= 32)
			{
				num &= 0x1F;
				if (num == 0)
				{
					for (; *ptr3 == 0; ptr3++)
					{
						num += 255;
					}
					num += (uint)(31 + *(ptr3++));
				}
				ptr4 = ptr2 - 1;
				ptr4 -= (*ptr3 >> 2) + (ptr3[1] << 6);
				ptr3 += 2;
			}
			else
			{
				if (num < 16)
				{
					ptr4 = ptr2 - 1;
					ptr4 -= num >> 2;
					ptr4 -= *(ptr3++) << 2;
					if (ptr4 < output || ptr4 >= ptr2)
					{
						throw new OverflowException("Lookbehind Overrun");
					}
					if (ptr - ptr2 < 2)
					{
						throw new OverflowException("Output Overrun");
					}
					*(ptr2++) = *(ptr4++);
					*(ptr2++) = *ptr4;
					goto IL_036f;
				}
				ptr4 = ptr2;
				ptr4 -= (num & 8) << 11;
				num &= 7;
				if (num == 0)
				{
					for (; *ptr3 == 0; ptr3++)
					{
						num += 255;
					}
					num += (uint)(7 + *(ptr3++));
				}
				ptr4 -= (*ptr3 >> 2) + (ptr3[1] << 6);
				ptr3 += 2;
				if (ptr4 == ptr2)
				{
					_ = ptr2 - output;
					if (ptr4 != ptr)
					{
						throw new OverflowException("Output Underrun");
					}
					return (uint)(ptr3 - input);
				}
				ptr4 -= 16384;
			}
			if (ptr4 < output || ptr4 >= ptr2)
			{
				throw new OverflowException("Lookbehind Overrun");
			}
			if (ptr - ptr2 < num + 2)
			{
				throw new OverflowException("Output Overrun");
			}
			if (num >= 6 && ptr2 - ptr4 >= 4)
			{
				*(int*)ptr2 = *(int*)ptr4;
				ptr2 += 4;
				ptr4 += 4;
				num -= 2;
				do
				{
					*(int*)ptr2 = *(int*)ptr4;
					ptr2 += 4;
					ptr4 += 4;
					num -= 4;
				}
				while (num >= 4);
				if (num != 0)
				{
					do
					{
						*(ptr2++) = *(ptr4++);
					}
					while (--num != 0);
				}
				goto IL_036f;
			}
		}
		*(ptr2++) = *(ptr4++);
		*(ptr2++) = *(ptr4++);
		do
		{
			*(ptr2++) = *(ptr4++);
		}
		while (--num != 0);
		goto IL_036f;
		IL_036f:
		num = (uint)(ptr3[-2] & 3);
		if (num == 0)
		{
			goto IL_0050;
		}
		goto IL_037d;
		IL_00f2:
		num = *(ptr3++);
		if (num >= 16)
		{
			goto IL_0169;
		}
		ptr4 = ptr2 - (1 + M2_MAX_OFFSET);
		ptr4 -= num >> 2;
		ptr4 -= *(ptr3++) << 2;
		if (ptr4 < output || ptr4 >= ptr2)
		{
			throw new OverflowException("Lookbehind Overrun");
		}
		if (ptr - ptr2 < 3)
		{
			throw new OverflowException("Output Overrun");
		}
		*(ptr2++) = *(ptr4++);
		*(ptr2++) = *(ptr4++);
		*(ptr2++) = *ptr4;
		goto IL_036f;
		IL_0050:
		num = *(ptr3++);
		if (num < 16)
		{
			if (num == 0)
			{
				for (; *ptr3 == 0; ptr3++)
				{
					num += 255;
				}
				num += (uint)(15 + *(ptr3++));
			}
			if (ptr - ptr2 < num + 3)
			{
				throw new OverflowException("Output Overrun");
			}
			*(int*)ptr2 = *(int*)ptr3;
			ptr2 += 4;
			ptr3 += 4;
			if (--num != 0)
			{
				if (num >= 4)
				{
					do
					{
						*(int*)ptr2 = *(int*)ptr3;
						ptr2 += 4;
						ptr3 += 4;
						num -= 4;
					}
					while (num >= 4);
					if (num != 0)
					{
						do
						{
							*(ptr2++) = *(ptr3++);
						}
						while (--num != 0);
					}
				}
				else
				{
					do
					{
						*(ptr2++) = *(ptr3++);
					}
					while (--num != 0);
				}
			}
			goto IL_00f2;
		}
		goto IL_0169;
	}

	private static byte ip(System.IO.Stream i)
	{
		byte result = (byte)i.ReadByte();
		i.Position--;
		return result;
	}

	private static byte ip(System.IO.Stream i, short offset)
	{
		i.Position += offset;
		byte result = (byte)i.ReadByte();
		i.Position -= offset + 1;
		return result;
	}

	private static byte next(System.IO.Stream i)
	{
		return (byte)i.ReadByte();
	}

	public unsafe static uint decompress(System.IO.Stream i, byte* output, uint expectedSize)
	{
		long position = i.Position;
		byte* ptr = output + expectedSize;
		byte* ptr2 = output;
		if (ip(i) <= 17)
		{
			goto IL_0059;
		}
		uint num = (uint)(next(i) - 17);
		if (num >= 4)
		{
			if (ptr - ptr2 < num)
			{
				throw new OverflowException("Outpur Overrun");
			}
			do
			{
				*(ptr2++) = next(i);
			}
			while (--num != 0);
			goto IL_0156;
		}
		goto IL_0435;
		IL_0435:
		if (ptr - ptr2 < num)
		{
			throw new OverflowException("Output Overrun");
		}
		*(ptr2++) = next(i);
		if (num > 1)
		{
			*(ptr2++) = next(i);
			if (num > 2)
			{
				*(ptr2++) = next(i);
			}
		}
		num = next(i);
		goto IL_01cd;
		IL_01cd:
		byte* ptr3;
		if (num >= 64)
		{
			ptr3 = ptr2 - 1;
			ptr3 -= (num >> 2) & 7;
			ptr3 -= next(i) << 3;
			num = (num >> 5) - 1;
			if (ptr3 < output || ptr3 >= ptr2)
			{
				throw new OverflowException("Lookbehind Overrun");
			}
			if (ptr - ptr2 < num + 2)
			{
				throw new OverflowException("Output Overrun");
			}
		}
		else
		{
			if (num >= 32)
			{
				num &= 0x1F;
				if (num == 0)
				{
					while (ip(i) == 0)
					{
						num += 255;
						i.Position++;
					}
					num += (uint)(31 + next(i));
				}
				ptr3 = ptr2 - 1;
				ptr3 -= (ip(i, 0) >> 2) + (ip(i, 1) << 6);
				i.Position += 2L;
			}
			else
			{
				if (num < 16)
				{
					ptr3 = ptr2 - 1;
					ptr3 -= num >> 2;
					ptr3 -= next(i) << 2;
					if (ptr3 < output || ptr3 >= ptr2)
					{
						throw new OverflowException("Lookbehind Overrun");
					}
					if (ptr - ptr2 < 2)
					{
						throw new OverflowException("Output Overrun");
					}
					*(ptr2++) = *(ptr3++);
					*(ptr2++) = *ptr3;
					goto IL_0424;
				}
				ptr3 = ptr2;
				ptr3 -= (num & 8) << 11;
				num &= 7;
				if (num == 0)
				{
					while (ip(i) == 0)
					{
						num += 255;
						i.Position++;
					}
					num += (uint)(7 + next(i));
				}
				ptr3 -= (ip(i, 0) >> 2) + (ip(i, 1) << 6);
				i.Position += 2L;
				if (ptr3 == ptr2)
				{
					_ = ptr2 - output;
					if (ptr3 != ptr)
					{
						throw new OverflowException("Output Underrun");
					}
					return (uint)(i.Position - position);
				}
				ptr3 -= 16384;
			}
			if (ptr3 < output || ptr3 >= ptr2)
			{
				throw new OverflowException("Lookbehind Overrun");
			}
			if (ptr - ptr2 < num + 2)
			{
				throw new OverflowException("Output Overrun");
			}
			if (num >= 6 && ptr2 - ptr3 >= 4)
			{
				*(int*)ptr2 = *(int*)ptr3;
				ptr2 += 4;
				ptr3 += 4;
				num -= 2;
				do
				{
					*(int*)ptr2 = *(int*)ptr3;
					ptr2 += 4;
					ptr3 += 4;
					num -= 4;
				}
				while (num >= 4);
				if (num != 0)
				{
					do
					{
						*(ptr2++) = *(ptr3++);
					}
					while (--num != 0);
				}
				goto IL_0424;
			}
		}
		*(ptr2++) = *(ptr3++);
		*(ptr2++) = *(ptr3++);
		do
		{
			*(ptr2++) = *(ptr3++);
		}
		while (--num != 0);
		goto IL_0424;
		IL_0424:
		num = (uint)(ip(i, -2) & 3);
		if (num == 0)
		{
			goto IL_0059;
		}
		goto IL_0435;
		IL_0156:
		num = next(i);
		if (num >= 16)
		{
			goto IL_01cd;
		}
		ptr3 = ptr2 - (1 + M2_MAX_OFFSET);
		ptr3 -= num >> 2;
		ptr3 -= next(i) << 2;
		if (ptr3 < output || ptr3 >= ptr2)
		{
			throw new OverflowException("Lookbehind Overrun");
		}
		if (ptr - ptr2 < 3)
		{
			throw new OverflowException("Output Overrun");
		}
		*(ptr2++) = *(ptr3++);
		*(ptr2++) = *(ptr3++);
		*(ptr2++) = *ptr3;
		goto IL_0424;
		IL_0059:
		num = next(i);
		if (num < 16)
		{
			if (num == 0)
			{
				while (ip(i) == 0)
				{
					num += 255;
					i.Position++;
				}
				num += (uint)(15 + next(i));
			}
			if (ptr - ptr2 < num + 3)
			{
				throw new OverflowException("Output Overrun");
			}
			*(ptr2++) = next(i);
			*(ptr2++) = next(i);
			*(ptr2++) = next(i);
			*(ptr2++) = next(i);
			if (--num != 0)
			{
				if (num >= 4)
				{
					do
					{
						*(ptr2++) = next(i);
						*(ptr2++) = next(i);
						*(ptr2++) = next(i);
						*(ptr2++) = next(i);
						num -= 4;
					}
					while (num >= 4);
					if (num != 0)
					{
						do
						{
							*(ptr2++) = next(i);
						}
						while (--num != 0);
					}
				}
				else
				{
					do
					{
						*(ptr2++) = next(i);
					}
					while (--num != 0);
				}
			}
			goto IL_0156;
		}
		goto IL_01cd;
	}

	public unsafe static uint readLZO(System.IO.Stream input, out byte[] dst, uint expectedSize)
	{
		dst = new byte[expectedSize];
		fixed (byte* output = &dst[0])
		{
			return decompress(input, output, expectedSize);
		}
	}

	public unsafe static byte[] readLZO(System.IO.Stream input, uint expectedSize)
	{
		byte[] array = new byte[expectedSize];
		fixed (byte* output = &array[0])
		{
			decompress(input, output, expectedSize);
		}
		return array;
	}
}
