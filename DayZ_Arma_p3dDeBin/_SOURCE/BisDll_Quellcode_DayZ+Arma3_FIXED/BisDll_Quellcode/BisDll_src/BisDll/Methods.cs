using System;
using System.Collections.Generic;
using System.Linq;

namespace BisDll;

public static class Methods
{
	public static void Swap<T>(ref T v1, ref T v2)
	{
		T val = v1;
		v1 = v2;
		v2 = val;
	}

	public static bool EqualsFloat(float f1, float f2, float tolerance = 0.0001f)
	{
		if (Math.Abs(f1 - f2) <= tolerance)
		{
			return true;
		}
		return false;
	}

	public static IEnumerable<T> Yield<T>(this T src)
	{
		yield return src;
	}

	public static IEnumerable<T> Yield<T>(params T[] elems)
	{
		return elems;
	}

	public static string CharsToString(this IEnumerable<char> chars)
	{
		return new string(chars.ToArray());
	}
}
