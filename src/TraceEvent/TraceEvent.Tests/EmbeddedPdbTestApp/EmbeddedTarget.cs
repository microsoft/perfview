namespace EmbeddedPdbTestApp
{
    /// <summary>
    /// Minimal target type for the embedded-portable-PDB SymbolReader test.   The test resolves the
    /// metadata token of <see cref="Add"/> via reflection (do not rely on a fixed token, since the
    /// netstandard2.0 build synthesizes attribute-type constructors ahead of it in the MethodDef table).
    ///
    /// IMPORTANT: SymbolReaderTests asserts the source line of the first sequence point of
    /// <see cref="Add"/> (IL offset 0, the first statement).   If you move the body below,
    /// update the expected line number in the test.
    /// </summary>
    public static class EmbeddedTarget
    {
        public static int Add(int a, int b)
        {
            int sum = a + b; // first sequence point (IL offset 0) is on this line
            return sum;
        }
    }
}
