namespace PerfDataService.Models
{
    public sealed class StackViewerModel
    {
        public string Filename { get; set; }

        public string StackType { get; set; }

        public string Start { get; set; }

        public string End { get; set; }

        public string GroupPats { get; set; }

        public string IncPats { get; set; }

        public string ExcPats { get; set; }

        public string FoldPats { get; set; }

        public string FoldPct { get; set; }

        public string SymPath { get; set; }

        public string SrcPth { get; set; }

        public string ModulesFilter { get; set; }

        public string ImageFilter { get; set; }
    }
}