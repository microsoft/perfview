//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 

namespace System.Collections.Generic
{
    internal class FunctorComparer<T> : IComparer<T>
    {
        public FunctorComparer(Comparison<T> comparison) { this.comparison = comparison; }
        public int Compare(T x, T y) { return comparison(x, y); }

        private Comparison<T> comparison;
    }
}
