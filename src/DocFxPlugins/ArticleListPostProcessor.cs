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

namespace DocFxPlugins
{
    [Export(nameof(ArticleListPostProcessor), typeof(IPostProcessor))]
    public class ArticleListPostProcessor : IPostProcessor
    {
        private int ArticleSnippetLength;

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
        {
            object length = null;
            metadata.TryGetValue(ArticleListConstants.ArticleListSnippetLengthKey, out length);
            ArticleSnippetLength = length as int? ?? ArticleListConstants.DefaultArticleSnippetLength;

            return metadata;
        }

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            if (outputFolder == null)
            {
                throw new ArgumentNullException("Base directory cannot be null");
            }

            List<ArticleListItem> articleListItems = GetArticleListItems(outputFolder, manifest);
            if (articleListItems.Count == 0)
            {
                return manifest;
            }

            articleListItems.Sort((x, y) => DateTime.Compare(y.Date, x.Date));
            InsertArticleListItems(outputFolder, manifest, articleListItems);

            return manifest;
        }

        private void InsertArticleListItems(string outputFolder, Manifest manifest, List<ArticleListItem> articleListItems)
        {
            List<string> articleListEnabledFiles = new List<string>();

            foreach (ManifestItem manifestItem in manifest.Files)
            {
                Dictionary<string, object> metadata = manifestItem.Metadata as Dictionary<string, object>;
                if (metadata != null && metadata.ContainsKey(ArticleListConstants.EnableArticleListKey))
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

                HtmlNode articleListItemsNode = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'al-items-all')]");

                foreach (ArticleListItem item in articleListItems)
                {
                    HtmlNode alItem = HtmlNode.CreateNode("<div class=\"al-item\"></div>");
                    alItem.AppendChildren(item.Snippet.ChildNodes);

                    articleListItemsNode.AppendChild(alItem);
                }

                htmlDoc.Save(file);
            }
        }

        private List<ArticleListItem> GetArticleListItems(string outputFolder, Manifest manifest)
        {
            List<ArticleListItem> articleListItems = new List<ArticleListItem>();

            foreach (ManifestItem manifestItem in manifest.Files)
            {
                Dictionary<string, object> metadata = manifestItem.Metadata as Dictionary<string, object>;
                if (metadata == null || !metadata.ContainsKey(ArticleListConstants.IncludeInArticleListKey))
                {
                    continue;
                }

                string href = manifestItem.OutputFiles.First(o => o.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)).Value.RelativePath;
                string filePath = Path.Combine(outputFolder, href);

                Logger.LogVerbose($"Generating article list item from {filePath}");

                if (!File.Exists(filePath))
                {
                    Logger.LogWarning($"Warning: {filePath} does not exist");
                }

                HtmlDocument htmlDoc = new HtmlDocument();

                try
                {
                    htmlDoc.Load(filePath, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Warning: Can't load content from {filePath}: {ex.Message}");
                    continue;
                }

                HtmlNode snippet = ExtractSnippet(htmlDoc);
                if (snippet == null)
                {
                    Logger.LogWarning($"Warning: {filePath} has no article node");
                    continue;
                }
                NormalizeSnippet(snippet, href);

                articleListItems.Add(new ArticleListItem
                {
                    Href = href,
                    Snippet = snippet,
                    Date = (DateTime)metadata[ArticleListConstants.DateKey]
                });
            }

            return articleListItems;
        }

        private HtmlNode ExtractSnippet(HtmlDocument html)
        {
            HtmlNode snippet = html.DocumentNode.SelectSingleNode("//article");

            if (snippet == null)
            {
                return null;
            }

            return snippet.Clone();
        }

        private void NormalizeSnippet(HtmlNode snippet, string href)
        {
            HtmlNode titleAnchorNode = HtmlNode.CreateNode($"<a href=\"/{href}\"></a>");
            HtmlNode titleNode = snippet.SelectSingleNode(".//h1");
            titleAnchorNode.InnerHtml = titleNode.InnerText;
            titleNode.RemoveAllChildren();
            titleNode.AppendChild(titleAnchorNode);

            TrimNode(snippet, 0);

            HtmlNodeCollection headers = snippet.SelectNodes(".//*[self::h2 or self::h3 or self::h4 or self::h5 or self::h6]");
            if(headers == null)
            {
                return;
            }
            foreach (HtmlNode node in headers)
            {
                node.Attributes.Add("class", "no-anchor" + node.Attributes["class"]?.Value ?? "");
            }
        }

        private int TrimNode(HtmlNode node, int currentSnippetLength)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                string text = node.InnerText.Trim();
                currentSnippetLength += text.Length;

                if (ArticleSnippetLength > currentSnippetLength)
                {
                    return currentSnippetLength;
                }

                int endIndex = text.IndexOfAny(new char[] { ' ', '.', ',', '!', '?', ';' }, text.Length - (currentSnippetLength - ArticleSnippetLength) - 1);

                if (endIndex == -1)
                {
                    endIndex = text.Length - 1;
                }

                node.InnerHtml = text.Substring(0, endIndex + 1);

                if (text[endIndex] != ' ')
                {
                    node.InnerHtml += " ";
                }

                node.InnerHtml += "...";

                return currentSnippetLength;
            }

            HtmlNodeCollection childNodes = node.ChildNodes;

            for (int i = 0; i < childNodes.Count; i++)
            {
                HtmlNode childNode = childNodes[i];
                currentSnippetLength = TrimNode(childNode, currentSnippetLength);
                if (currentSnippetLength >= ArticleSnippetLength)
                {
                    int numNodesToRemove = childNodes.Count - i - 1;

                    while (numNodesToRemove-- > 0)
                    {
                        node.RemoveChild(node.LastChild);
                    }

                    return currentSnippetLength;
                }
            }

            return currentSnippetLength;
        }
    }
}
