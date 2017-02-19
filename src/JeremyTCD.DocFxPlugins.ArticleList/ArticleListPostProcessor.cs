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
            HtmlNode articleListNode = GenerateArticleListNode(articleListItems);
            InsertArticleListNode(outputFolder, manifest, articleListNode);

            return manifest;
        }

        private HtmlNode GenerateArticleListNode(List<ArticleListItem> articleListItems)
        {
            HtmlNode articleListNode = HtmlNode.CreateNode($"<div class=\"{ArticleListConstants.ArticleListNodeClass}\"></div>");

            foreach (ArticleListItem articleListItem in articleListItems)
            {
                articleListNode.AppendChild(articleListItem.SnippetNode);
            }

            return articleListNode;
        }

        private void InsertArticleListNode(string outputFolder, Manifest manifest, HtmlNode articleListItemsNode)
        {
            foreach (ManifestItem manifestItem in manifest.Files)
            {
                object enableArticleList = null;
                manifestItem.Metadata.TryGetValue(ArticleListConstants.EnableArticleListKey, out enableArticleList);
                if (enableArticleList as bool? != true)
                {
                    continue;
                }

                string filePath = Path.Combine(outputFolder,
                    manifestItem.
                        OutputFiles.
                        First(o => o.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)).
                        Value.
                        RelativePath);

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.Load(filePath, Encoding.UTF8);

                htmlDoc.
                    DocumentNode.
                    SelectSingleNode($"//div[@id='{ArticleListConstants.ArticleListWrapperNodeClass}']");
                    AppendChild(articleListItemsNode);

                htmlDoc.Save(filePath);
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
                snippetNode.Attributes.Add("class", ArticleListConstants.ArticleListItemClass);

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
                    RelPath = relPath,
                    SnippetNode = snippetNode,
                    Date = date
                });
            }

            return articleListItems;
        }
    }
}
