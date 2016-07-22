namespace PerfDataService.Models
{
    using System.Collections.Generic;

    public class StackViewerViewModel
    {
        public string Filename { get; set; }

        public string StackType { get; set; }

        public string GroupPats { get; set; }

        public string IncPats { get; set; }

        public string ExcPats { get; set; }

        public string FoldPats { get; set; }

        public string FoldPct { get; set; }

        public string SymPath { get; set; }

        public string SrcPath { get; set; }

        public string ModulesFilter { get; set; }

        public string ImageFilter { get; set; }

        public string Start { get; set; }

        public string End { get; set; }

        public IEnumerable<TreeNode> TreeNodes { get; set; }

        public string CacheKey => this.Filename + this.StackType + this.Start + this.End + this.IncPats + this.ExcPats + this.FoldPats + this.GroupPats + this.FoldPct;

        public StackViewerViewModel()
        {
            this.Start = string.Empty;
            this.End = string.Empty;
            this.GroupPats = string.Empty;
            this.IncPats = string.Empty;
            this.ExcPats = string.Empty;
            this.FoldPats = string.Empty;
            this.FoldPct = string.Empty;
            this.SymPath = string.Empty;
            this.SrcPath = string.Empty;
            this.ModulesFilter = string.Empty;
            this.ImageFilter = string.Empty;
        }

        public override string ToString()
        {
            return $"stacktype={this.StackType}&filename={this.Filename}&start={this.Start}&end={this.End}&grouppats={this.GroupPats}&incpats={this.IncPats}&excpats={this.ExcPats}&foldpats={this.FoldPats}&foldpct={this.FoldPct}&sympath={this.SymPath}&srcpath={this.SrcPath}&modulesFilter={this.ModulesFilter}&imageFilter={this.ImageFilter}";
        }
    }
}