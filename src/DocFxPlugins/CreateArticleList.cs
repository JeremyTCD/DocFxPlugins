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

namespace DocFxPlugins
{
    [Export(nameof(CreateArticleList), typeof(IPostProcessor))]
    public class CreateArticleList : IPostProcessor
    {
        private static readonly Regex RegexWhiteSpace = new Regex(@"\s+", RegexOptions.Compiled);

        public CreateArticleList()
        {
        }

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
            Dictionary<string, ArticleListItem> items = new Dictionary<string, ArticleListItem>();
            // TODO don't include html files that aren't articles 
            List<string> htmlFiles = (from item in manifest.Files ?? Enumerable.Empty<ManifestItem>()
                                      from output in item.OutputFiles
                                      where output.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)
                                      select output.Value.RelativePath).ToList();
            if (htmlFiles.Count == 0)
            {
                return manifest;
            }

            Logger.LogInfo($"Extracting data from {htmlFiles.Count} html files");
            foreach (string relativePath in htmlFiles)
            {
                string filePath = Path.Combine(outputFolder, relativePath);
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
                    ArticleListItem indexItem = ExtractItem(htmlDoc, relativePath);
                    if (indexItem != null)
                    {
                        items[relativePath] = indexItem;
                    }
                }
            }
            
            // TODO Sort items by date
            // TODO Insert items into article list page

            return manifest;
        }

        internal ArticleListItem ExtractItem(HtmlDocument html, string href)
        {
            StringBuilder contentBuilder = new StringBuilder();

            // Select content between the data-searchable class tag
            IEnumerable<HtmlNode> nodes = html.DocumentNode.SelectNodes("//*[contains(@class,'data-searchable')]") ?? Enumerable.Empty<HtmlNode>();
            // Select content between the article tag
            nodes = nodes.Union(html.DocumentNode.SelectNodes("//article") ?? Enumerable.Empty<HtmlNode>());
            foreach (HtmlNode node in nodes)
            {
                ExtractTextFromNode(node, contentBuilder);
            }

            string content = NormalizeContent(contentBuilder.ToString());
            string title = ExtractTitleFromHtml(html);

            // TODO extract date

            return new ArticleListItem { Href = href, Title = title, Keywords = content };
        }

        private string ExtractTitleFromHtml(HtmlDocument html)
        {
            HtmlNode titleNode = html.DocumentNode.SelectSingleNode("//head/title");
            string originalTitle = titleNode?.InnerText;
            return NormalizeContent(originalTitle);
        }

        private string NormalizeContent(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }
            str = StringHelper.HtmlDecode(str);
            return RegexWhiteSpace.Replace(str, " ").Trim();
        }

        private void ExtractTextFromNode(HtmlNode root, StringBuilder contentBuilder)
        {
            if (root == null)
            {
                return;
            }

            if (!root.HasChildNodes)
            {
                contentBuilder.Append(root.InnerText);
                contentBuilder.Append(" ");
            }
            else
            {
                foreach (HtmlNode node in root.ChildNodes)
                {
                    ExtractTextFromNode(node, contentBuilder);
                }
            }
        }
    }
}
