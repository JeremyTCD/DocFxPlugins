using HtmlAgilityPack;
using Newtonsoft.Json;
using System;

namespace DocFxPlugins
{
    public class ArticleListItem
    {
        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("snippet")]
        public HtmlNode Snippet { get; set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ArticleListItem);
        }

        public bool Equals(ArticleListItem other)
        {
            if (other == null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return HtmlNode.Equals(this.Snippet, other.Snippet) && 
                string.Equals(this.Href, other.Href) &&
                DateTime.Equals(this.Date, other.Date);
        }

        public override int GetHashCode()
        {
            return Snippet.GetHashCode() ^ Href.GetHashCode() ^ Date.GetHashCode();
        }
    }
}