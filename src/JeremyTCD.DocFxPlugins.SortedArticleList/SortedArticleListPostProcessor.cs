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
        private int SalSnippetLength;

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
        {
            object length = null;
            metadata.TryGetValue(SortedArticleListConstants.SalSnippetLengthKey, out length);
            SalSnippetLength = length as int? ?? SortedArticleListConstants.DefaultSalSnippetLength;

            return metadata;
        }

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            if (outputFolder == null)
            {
                throw new ArgumentNullException("Base directory cannot be null");
            }

            List<SortedArticleListItem> salItems = GetSalItems(outputFolder, manifest);
            if (salItems.Count == 0)
            {
                return manifest;
            }

            salItems.Sort((x, y) => DateTime.Compare(y.Date, x.Date));
            HtmlNode salNode = GenerateSalNode(salItems);
            InsertSalNode(outputFolder, manifest, salNode);

            return manifest;
        }

        private HtmlNode GenerateSalNode(List<SortedArticleListItem> salItems)
        {
            HtmlNode salNode = HtmlNode.CreateNode($"<div></div>");

            foreach (SortedArticleListItem salItem in salItems)
            {
                salNode.AppendChild(salItem.SnippetNode);
            }

            return salNode;
        }

        private void InsertSalNode(string outputFolder, Manifest manifest, HtmlNode salItemsNode)
        {
            foreach (ManifestItem manifestItem in manifest.Files)
            {
                object enableSal = null;
                manifestItem.Metadata.TryGetValue(SortedArticleListConstants.EnableSalKey, out enableSal);
                if (enableSal as bool? != true)
                {
                    continue;
                }

                string relPath = manifestItem.GetHtmlOutputRelPath();

                HtmlDocument htmlDoc = manifestItem.GetHtmlOutputDoc(outputFolder);
                HtmlNode salWrapperNode = htmlDoc.
                    DocumentNode.
                    SelectSingleNode($"//div[@id='{SortedArticleListConstants.SalAllItemsNodeId}']");
                if (salWrapperNode == null)
                {
                    throw new InvalidDataException($"{nameof(SortedArticleListPostProcessor)}: Html output {relPath} has no sorted article list all-items node");

                }
                salWrapperNode.AppendChildren(salItemsNode.ChildNodes);


                htmlDoc.Save(Path.Combine(outputFolder, relPath));
            }
        }

        private List<SortedArticleListItem> GetSalItems(string outputFolder, Manifest manifest)
        {
            List<SortedArticleListItem> salItems = new List<SortedArticleListItem>();

            foreach (ManifestItem manifestItem in manifest.Files)
            {
                object includeInSal = null;
                manifestItem.Metadata.TryGetValue(SortedArticleListConstants.IncludeInSalKey, out includeInSal);
                if (includeInSal as bool? != true)
                {
                    continue;
                }

                HtmlNode articleNode = manifestItem.GetHtmlOutputArticleNode(outputFolder);
                string relPath = manifestItem.GetHtmlOutputRelPath();
                HtmlNode snippetNode = SnippetCreator.CreateSnippet(articleNode, relPath, SalSnippetLength);

                DateTime date = default(DateTime);
                try
                {
                    date = DateTime.ParseExact(manifestItem.Metadata[SortedArticleListConstants.DateKey] as string, "d", new CultureInfo("en-us"));
                }
                catch
                {
                    throw new InvalidDataException($"{nameof(SortedArticleListPostProcessor)}: Article {manifestItem.SourceRelativePath} has an invalid {SortedArticleListConstants.DateKey}");
                }

                salItems.Add(new SortedArticleListItem
                {
                    RelPath = relPath,
                    SnippetNode = snippetNode,
                    Date = date
                });
            }

            return salItems;
        }
    }
}
