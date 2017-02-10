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

            List<string> articleHtmlFiles = GetArticleHtmlFiles(manifest);
            if (articleHtmlFiles.Count == 0)
            {
                return manifest;
            }

            Logger.LogInfo($"Extracting data from {articleHtmlFiles.Count} html files");
            List<ArticleListItem> articleListItems = new List<ArticleListItem>();
            foreach (string relativePath in articleHtmlFiles)
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
                    ArticleListItem articleListItem = ExtractItem(htmlDoc, relativePath);
                    if (articleListItem != null)
                    {
                        articleListItems.Add(articleListItem);
                    }
                }
            }

            articleListItems.Sort((x, y) => DateTime.Compare(y.Date, x.Date));
            InsertArticlesIntoRecent(outputFolder, articleListItems);

            return manifest;
        }

        private void InsertArticlesIntoRecent(string outputFolder, List<ArticleListItem> articleListItems)
        {
            string recentFile = Path.Combine(outputFolder, "articles/recent.html");
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.Load(recentFile, Encoding.UTF8);

            HtmlNode articleListItemsNode = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'al-items')]");

            foreach(ArticleListItem item in articleListItems)
            {
                HtmlNode alItem = htmlDoc.CreateElement("div");
                alItem.Attributes.Add("class", "al-item");
                articleListItemsNode.AppendChild(alItem);

                HtmlNode itemBrief = htmlDoc.CreateElement("div");
                itemBrief.Attributes.Add("class", "item-brief");
                itemBrief.InnerHtml = item.Keywords;
                alItem.AppendChild(itemBrief);

                HtmlNode itemHref = htmlDoc.CreateElement("div");
                itemHref.Attributes.Add("class", "item-href");
                itemHref.InnerHtml = item.Href;
                alItem.AppendChild(itemHref);

                HtmlNode itemTitle = htmlDoc.CreateElement("div");
                itemTitle.Attributes.Add("class", "item-title");
                alItem.AppendChild(itemTitle);

                HtmlNode itemTitleAnchor = htmlDoc.CreateElement("a");
                itemTitleAnchor.Attributes.Add("href", item.Href);
                itemTitleAnchor.Attributes.Add("target", "_blank");
                itemTitleAnchor.InnerHtml = item.Title;
                itemTitle.AppendChild(itemTitleAnchor);
            }

            htmlDoc.Save(recentFile);
        }

        private List<string> GetArticleHtmlFiles(Manifest manifest)
        {
            return (from item in manifest.Files ?? Enumerable.Empty<ManifestItem>()
                     from output in item.OutputFiles
                     where item.SourceRelativePath.StartsWith("articles/") &&
                        item.DocumentType != "Toc" &&
                        item.SourceRelativePath != "articles/recent.md" &&
                        output.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)
                     select output.Value.RelativePath).ToList();
        }

        private ArticleListItem ExtractItem(HtmlDocument html, string href)
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
            string dateRaw = ExtractDateFromHtml(html);
            DateTime date = default(DateTime);

            try
            {
                date = DateTime.ParseExact(dateRaw, "d", new CultureInfo("en-us"));
            }
            catch
            {
                throw new InvalidDataException($"{nameof(CreateArticleList)}: Article {href}'s date is invalid");
            }

            return new ArticleListItem { Href = href, Title = title, Keywords = content, DateRaw = dateRaw, Date = date };
        }

        private string ExtractDateFromHtml(HtmlDocument html)
        {
            HtmlNode dateNode = html.DocumentNode.SelectSingleNode("//div[contains(@class,'meta')]/span[contains(@class,'date')]");
            return dateNode?.InnerText;
        }

        private string ExtractTitleFromHtml(HtmlDocument html)
        {
            HtmlNode titleNode = html.DocumentNode.SelectSingleNode("//head/title");
            string originalTitle = titleNode.InnerText;
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
