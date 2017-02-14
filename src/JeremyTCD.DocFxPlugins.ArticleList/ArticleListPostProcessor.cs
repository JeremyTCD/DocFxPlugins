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
using System.Globalization;
using JeremyTCD.DocFxPlugins.Shared;

namespace JeremyTCD.DocFxPlugins.ArticleList
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
                object enableArticleList = null;
                manifestItem.Metadata.TryGetValue(ArticleListConstants.EnableArticleListKey, out enableArticleList);
                if (enableArticleList as bool? != true)
                {
                    continue;
                }

                articleListEnabledFiles.Add(Path.Combine(outputFolder,
                    manifestItem.
                        OutputFiles.
                        First(o => o.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)).
                        Value.
                        RelativePath));
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
                object includeInArticleList = null;
                manifestItem.Metadata.TryGetValue(ArticleListConstants.IncludeInArticleListKey, out includeInArticleList);
                if (includeInArticleList as bool? != true)
                {
                    continue;
                }

                string href = manifestItem.OutputFiles.First(o => o.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)).Value.RelativePath;
                string filePath = Path.Combine(outputFolder, href);

                Logger.LogVerbose($"Generating article list item from {filePath}");

                if (!File.Exists(filePath))
                {
                    throw new InvalidDataException($"{nameof(ArticleListPostProcessor)}: Article {filePath} does not exist");
                }

                HtmlDocument htmlDoc = new HtmlDocument();

                try
                {
                    htmlDoc.Load(filePath, Encoding.UTF8);
                }
                catch
                {
                    throw new InvalidDataException($"{nameof(ArticleListPostProcessor)}: Article {filePath} cannot be loaded");
                }

                HtmlNode article = htmlDoc.DocumentNode.SelectSingleNode("//article");
                if (article == null)
                {
                    throw new InvalidDataException($"{nameof(ArticleListPostProcessor)}: Article {filePath} has no article node");
                }
                HtmlNode snippet = SnippetCreator.CreateSnippet(article, href, ArticleSnippetLength);

                DateTime date = default(DateTime);
                try
                {
                    date = DateTime.ParseExact(manifestItem.Metadata[ArticleListConstants.DateKey] as string, "d", new CultureInfo("en-us"));
                }
                catch
                {
                    throw new InvalidDataException($"{nameof(ArticleListPostProcessor)}: Article {filePath} has an invalid {ArticleListConstants.DateKey}");
                }

                articleListItems.Add(new ArticleListItem
                {
                    Href = href,
                    Snippet = snippet,
                    Date = date
                });
            }

            return articleListItems;
        }
    }
}
