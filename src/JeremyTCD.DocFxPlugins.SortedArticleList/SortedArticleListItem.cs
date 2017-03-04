using HtmlAgilityPack;
using Newtonsoft.Json;
using System;

namespace JeremyTCD.DocFxPlugins.SortedArticleList
{
    public class SortedArticleListItem
    {
        [JsonProperty("relPath")]
        public string RelPath { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("snippetNode")]
        public HtmlNode SnippetNode { get; set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as SortedArticleListItem);
        }

        public bool Equals(SortedArticleListItem other)
        {
            if (other == null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return HtmlNode.Equals(this.SnippetNode, other.SnippetNode) && 
                string.Equals(this.RelPath, other.RelPath) &&
                DateTime.Equals(this.Date, other.Date);
        }

        public override int GetHashCode()
        {
            return SnippetNode.GetHashCode() ^ RelPath.GetHashCode() ^ Date.GetHashCode();
        }
    }
}