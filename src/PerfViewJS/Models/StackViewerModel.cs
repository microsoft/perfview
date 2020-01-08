// <copyright file="StackViewerModel.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System.IO;
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;

    public sealed class StackViewerModel
    {
        public StackViewerModel(string dataDirectoryListingRoot, IQueryCollection queryCollection)
        {
            this.Filename = (string)queryCollection["Filename"] ?? string.Empty;
            this.StackType = (string)queryCollection["StackType"] ?? string.Empty;
            this.Pid = (string)queryCollection["Pid"] ?? string.Empty;
            this.Start = (string)queryCollection["Start"] ?? string.Empty;
            this.End = (string)queryCollection["End"] ?? string.Empty;
            this.GroupPats = (string)queryCollection["GroupPats"] ?? string.Empty;
            this.IncPats = (string)queryCollection["IncPats"] ?? string.Empty;
            this.ExcPats = (string)queryCollection["ExcPats"] ?? string.Empty;
            this.FoldPats = (string)queryCollection["FoldPats"] ?? string.Empty;
            this.FoldPct = (string)queryCollection["FoldPct"] ?? string.Empty;
            this.DrillIntoKey = (string)queryCollection["DrillIntoKey"] ?? string.Empty;

            this.Filename = Path.Combine(dataDirectoryListingRoot, Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(this.Filename)));
            this.Start = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(this.Start));
            this.End = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(this.End));
            this.GroupPats = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(this.GroupPats));
            this.IncPats = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(this.IncPats));
            this.FoldPats = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(this.FoldPats));
            this.ExcPats = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(this.ExcPats));
        }

        public string Filename { get; }

        public string Pid { get; }

        public string StackType { get; }

        public string Start { get; }

        public string End { get; }

        public string GroupPats { get; }

        public string IncPats { get; }

        public string ExcPats { get; }

        public string FoldPats { get; }

        public string FoldPct { get; }

        public string DrillIntoKey { get; private set; }

        public override string ToString()
        {
            return $"pid={this.Pid}&stacktype={this.StackType}&filename={this.Filename}&start={this.Start}&end={this.End}&grouppats={this.GroupPats}&incpats={this.IncPats}&excpats={this.ExcPats}&foldpats={this.FoldPats}&foldpct={this.FoldPct}&drillIntoKey={this.DrillIntoKey}";
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = this.Filename != null ? this.Filename.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (this.Pid != null ? this.Pid.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.StackType != null ? this.StackType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Start != null ? this.Start.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.End != null ? this.End.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.GroupPats != null ? this.GroupPats.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.IncPats != null ? this.IncPats.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.ExcPats != null ? this.ExcPats.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.FoldPats != null ? this.FoldPats.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.FoldPct != null ? this.FoldPct.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((StackViewerModel)obj);
        }

        public void SetDrillIntoKey(string drillIntoKey)
        {
            this.DrillIntoKey = drillIntoKey;
        }

        private bool Equals(StackViewerModel other)
        {
            return string.Equals(this.Filename, other.Filename) &&
                   string.Equals(this.Pid, other.Pid) &&
                   string.Equals(this.StackType, other.StackType) &&
                   string.Equals(this.Start, other.Start) &&
                   string.Equals(this.End, other.End) &&
                   string.Equals(this.GroupPats, other.GroupPats) &&
                   string.Equals(this.IncPats, other.IncPats) &&
                   string.Equals(this.ExcPats, other.ExcPats) &&
                   string.Equals(this.FoldPats, other.FoldPats) &&
                   string.Equals(this.FoldPct, other.FoldPct) &&
                   string.Equals(this.DrillIntoKey, other.DrillIntoKey);
        }
    }
}
