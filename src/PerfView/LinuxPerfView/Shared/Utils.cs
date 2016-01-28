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

		internal static bool IsNumberChar(char c)
		{
			switch (c)
			{
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
				case '0':
					return true;
			}

			return false;
		}
	}
}
