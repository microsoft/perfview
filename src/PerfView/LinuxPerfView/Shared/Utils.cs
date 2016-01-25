using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Validation;

namespace LinuxTracing.Shared
{
	internal static class Utils
	{
		internal static long ConcatIntegers(int left, int right)
		{
			Requires.Argument(right >= 0, nameof(right), "Must be positive");
			return long.Parse(string.Format("{0}{1}", left, right));
		}
	}
}
