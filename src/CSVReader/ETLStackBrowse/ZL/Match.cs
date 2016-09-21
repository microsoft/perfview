namespace System.IO.Compression2
{
    // This class represents a match in the history window
    internal class Match
    {
        internal MatchState State { get; set; }

        internal int Position { get; set; }

        internal int Length { get; set; }

        internal byte Symbol { get; set; }
    }
}