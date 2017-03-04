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

namespace JeremyTCD.DocFxPlugins.SortedArticleList
{
    [Export(nameof(SortedArticleListPostProcessor), typeof(IPostProcessor))]
    public class SortedArticleListPostProcessor : IPostProcessor
    {
        private int ArticleSnippetLength;

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
        {
            object length = null;
            metadata.TryGetValue(SortedArticleListConstants.ArticleListSnippetLengthKey, out length);
            ArticleSnippetLength = length as int? ?? SortedArticleListConstants.DefaultArticleSnippetLength;

            return metadata;
        }

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            if (outputFolder == null)
            {
                throw new ArgumentNullException("Base directory cannot be null");
            }

            List<SortedArticleListItem> articleListItems = GetArticleListItems(outputFolder, manifest);
            if (articleListItems.Count == 0)
            {
                return manifest;
            }

            articleListItems.Sort((x, y) => DateTime.Compare(y.Date, x.Date));
            HtmlNode articleListNode = GenerateArticleListNode(articleListItems);
            InsertArticleListNode(outputFolder, manifest, articleListNode);

            return manifest;
        }

        private HtmlNode GenerateArticleListNode(List<SortedArticleListItem> articleListItems)
        {
            HtmlNode articleListNode = HtmlNode.CreateNode($"<div class=\"{SortedArticleListConstants.ArticleListNodeClass}\"></div>");

            foreach (SortedArticleListItem articleListItem in articleListItems)
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
                manifestItem.Metadata.TryGetValue(SortedArticleListConstants.EnableArticleListKey, out enableArticleList);
                if (enableArticleList as bool? != true)
                {
                    continue;
                }

                string relPath = manifestItem.GetHtmlOutputRelPath();

                HtmlDocument htmlDoc = manifestItem.GetHtmlOutputDoc(outputFolder);
                HtmlNode articleListWrapperNode = htmlDoc.
                    DocumentNode.
                    SelectSingleNode($"//div[@id='{SortedArticleListConstants.ArticleListWrapperNodeClass}']");
                if (articleListWrapperNode == null)
                {
                    throw new InvalidDataException($"{nameof(SortedArticleListPostProcessor)}: Html output {relPath} has no article list wrapper node");

                }
                articleListWrapperNode.AppendChild(articleListItemsNode);


                htmlDoc.Save(Path.Combine(outputFolder, relPath));
            }
        }

        private List<SortedArticleListItem> GetArticleListItems(string outputFolder, Manifest manifest)
        {
            List<SortedArticleListItem> articleListItems = new List<SortedArticleListItem>();

            foreach (ManifestItem manifestItem in manifest.Files)
            {
                object includeInArticleList = null;
                manifestItem.Metadata.TryGetValue(SortedArticleListConstants.IncludeInArticleListKey, out includeInArticleList);
                if (includeInArticleList as bool? != true)
                {
                    continue;
                }

                HtmlNode articleNode = manifestItem.GetHtmlOutputArticleNode(outputFolder);
                string relPath = manifestItem.GetHtmlOutputRelPath();
                HtmlNode snippetNode = SnippetCreator.CreateSnippet(articleNode, relPath, ArticleSnippetLength);
                snippetNode.Attributes.Add("class", SortedArticleListConstants.ArticleListItemClass);

                DateTime date = default(DateTime);
                try
                {
                    date = DateTime.ParseExact(manifestItem.Metadata[SortedArticleListConstants.DateKey] as string, "d", new CultureInfo("en-us"));
                }
                catch
                {
                    throw new InvalidDataException($"{nameof(SortedArticleListPostProcessor)}: Article {manifestItem.SourceRelativePath} has an invalid {SortedArticleListConstants.DateKey}");
                }

                articleListItems.Add(new SortedArticleListItem
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
