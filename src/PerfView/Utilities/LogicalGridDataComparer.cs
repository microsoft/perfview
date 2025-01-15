using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Utilities
{
    static class NativeMethods
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        internal static extern int StrCmpLogicalW(string psz1, string psz2);
    }

    internal class LogicalGridDataComparer<T> : IComparer where T : class
    {
        private readonly ListSortDirection _direction;
        private readonly PropertyInfo _propertyInfo;

        public LogicalGridDataComparer(string sortMemberPath, ListSortDirection direction)
        {
            _direction = direction;
            _propertyInfo = typeof(T)
                .GetProperty(sortMemberPath);
        }

        public int Compare(object x, object y)
        {
            if (x == y || _propertyInfo == null)
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            var xVal = _propertyInfo.GetValue(x)?.ToString();
            var yVal = _propertyInfo.GetValue(y)?.ToString();

            if (string.IsNullOrEmpty(xVal))
            {
                if (string.IsNullOrEmpty(yVal))
                {
                    return 0;
                }

                return -1;
            }

            if (string.IsNullOrEmpty(yVal))
            {
                return 1;
            }

            if (DateTime.TryParse(xVal, out var xDt) && DateTime.TryParse(yVal, out var yDt))
            {
                return xDt.CompareTo(yDt);
            }

            if (NativeMethods.StrCmpLogicalW(xVal, yVal) is var res
                && _direction == ListSortDirection.Descending)
            {
                return -res;
            }

            return res;
        }
    }
}