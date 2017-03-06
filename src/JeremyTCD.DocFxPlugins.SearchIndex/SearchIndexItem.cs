namespace JeremyTCD.DocFxPlugins.SearchIndex
{
    using HtmlAgilityPack;
    using Newtonsoft.Json;

    public class SearchIndexItem
    {
        [JsonProperty("relPath")]
        public string RelPath { get; set; }

        [JsonProperty("snippetHtml")]
        public string SnippetHtml { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as SearchIndexItem);
        }

        public bool Equals(SearchIndexItem other)
        {
            if (other == null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(this.SnippetHtml, other.SnippetHtml) && 
                string.Equals(this.RelPath, other.RelPath) && 
                string.Equals(this.Text, other.Text);
        }

        public override int GetHashCode()
        {
            return SnippetHtml.GetHashCode() ^ RelPath.GetHashCode() ^ Text.GetHashCode();
        }
    }
}