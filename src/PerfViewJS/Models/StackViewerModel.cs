// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;

    public sealed class StackViewerModel
    {
        public StackViewerModel(HttpContext httpContext)
        {
            this.Filename = (string)httpContext.Request.Query["Filename"] ?? string.Empty;
            this.StackType = (string)httpContext.Request.Query["StackType"] ?? string.Empty;
            this.Pid = (string)httpContext.Request.Query["Pid"] ?? string.Empty;
            this.Start = (string)httpContext.Request.Query["Start"] ?? string.Empty;
            this.End = (string)httpContext.Request.Query["End"] ?? string.Empty;
            this.GroupPats = (string)httpContext.Request.Query["GroupPats"] ?? string.Empty;
            this.IncPats = (string)httpContext.Request.Query["IncPats"] ?? string.Empty;
            this.ExcPats = (string)httpContext.Request.Query["ExcPats"] ?? string.Empty;
            this.FoldPats = (string)httpContext.Request.Query["FoldPats"] ?? string.Empty;
            this.FoldPct = (string)httpContext.Request.Query["FoldPct"] ?? string.Empty;
            this.DrillIntoKey = (string)httpContext.Request.Query["DrillIntoKey"] ?? string.Empty;

            this.Filename = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(this.Filename));
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
            if (ReferenceEquals(null, obj))
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
