using Microsoft.DocAsCode.Plugins;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Collections.Immutable;
using System.IO;
using Microsoft.DocAsCode.Common;
using HtmlAgilityPack;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.DocAsCode.MarkdownLite;
using System.Globalization;

namespace DocFxPlugins
{
    [Export(nameof(ArticleListPostProcessor), typeof(IPostProcessor))]
    public class ArticleListPostProcessor : IPostProcessor
    {
        private static readonly Regex RegexWhiteSpace = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly int ArticleSnippetMaxLength = 145;
        private static int ArticleSnippetLength = 0;

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
        {
            return metadata;
        }

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            if (outputFolder == null)
            {
                throw new ArgumentNullException("Base directory cannot be null");
            }

            List<ArticleListItem> articleListItems = GetArticleListItems(manifest);
            if (articleListItems.Count == 0)
            {
                return manifest;
            }

            articleListItems.Sort((x, y) => DateTime.Compare(y.Date, x.Date));

            Logger.LogInfo($"Extracting data from {articleListItems.Count} html files");
            foreach (ArticleListItem articleListItem in articleListItems)
            {
                string filePath = Path.Combine(outputFolder, articleListItem.Href);
                HtmlDocument htmlDoc = new HtmlDocument();

                Logger.LogVerbose($"Extracting data from {filePath}");
                if (File.Exists(filePath))
                {
                    try
                    {
                        htmlDoc.Load(filePath, Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Warning: Can't load content from {filePath}: {ex.Message}");
                        continue;
                    }

                    ExtractData(htmlDoc, articleListItem);
                }
            }

            InsertArticleLists(outputFolder, manifest, articleListItems);

            return manifest;
        }

        private void InsertArticleLists(string outputFolder, Manifest manifest, List<ArticleListItem> articleListItems)
        {
            List<string> articleListEnabledFiles = new List<string>();

            foreach (ManifestItem manifestItem in manifest.Files)
            {
                Dictionary<string, object> metadata = manifestItem.Metadata as Dictionary<string, object>;
                if (metadata != null && metadata.ContainsKey("jr.enableArticleList"))
                {
                    articleListEnabledFiles.Add(Path.Combine(outputFolder, 
                        manifestItem.
                            OutputFiles.
                            First(o => o.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)).
                            Value.
                            RelativePath));
                }
            }

            foreach (string file in articleListEnabledFiles)
            {
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.Load(file, Encoding.UTF8);

                HtmlNode articleListItemsNode = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'al-items')]");

                foreach (ArticleListItem item in articleListItems)
                {
                    HtmlNode alItem = htmlDoc.CreateElement("div");
                    alItem.Attributes.Add("class", "al-item");
                    articleListItemsNode.AppendChild(alItem);

                    alItem.AppendChild(item.Snippet);
                }

                htmlDoc.Save(file);
            }
        }

        private List<ArticleListItem> GetArticleListItems(Manifest manifest)
        {
            List<ArticleListItem> articleListItems = new List<ArticleListItem>();

            foreach(ManifestItem manifestItem in manifest.Files)
            {
                Dictionary<string, object> metadata = manifestItem.Metadata as Dictionary<string, object>;
                if(metadata != null && metadata.ContainsKey("jr.includeInArticleList"))
                {
                    articleListItems.Add(new ArticleListItem
                    {
                        Href = manifestItem.OutputFiles.First(o => o.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)).Value.RelativePath,
                        Date = (DateTime)metadata["jr.date"]

                    });
                }
            }

            return articleListItems;
        }

        private void ExtractData(HtmlDocument html, ArticleListItem articleListItem)
        {
            StringBuilder contentBuilder = new StringBuilder();

            // Select content between the data-searchable class tag
            HtmlNode articleNode = html.DocumentNode.SelectSingleNode("//article");

            if(articleListItem == null)
            {
                throw new InvalidDataException($"{nameof(ArticleListPostProcessor)}: Article {articleListItem.Href} has an empty article");
            }


            ArticleSnippetLength = 0;
            TrimNode(articleNode);

            articleListItem.Snippet = articleNode.Clone();
        }

        private bool TrimNode(HtmlNode node)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                string text = node.InnerText.Trim();
                ArticleSnippetLength += text.Length;
                int diff = ArticleSnippetMaxLength - ArticleSnippetLength;

                if (diff > 0)
                {
                    return true;
                }
                else if (diff < 0)
                {
                    node.InnerHtml = text.Substring(0, text.Length + diff);
                }

                node.InnerHtml += "...";

                return false;
            }

            HtmlNodeCollection childNodes = node.ChildNodes;

            for (int i = 0; i < childNodes.Count; i++)
            {
                HtmlNode childNode = childNodes[i];

                if (!TrimNode(childNode))
                {
                    int numNodesToRemove = childNodes.Count - i - 1;

                    while (numNodesToRemove-- > 0)
                    {
                        node.RemoveChild(node.LastChild);
                    }

                    return false;
                }
            }

            return true;
        } 
    }
}
