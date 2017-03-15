using Microsoft.DocAsCode.Plugins;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Collections.Immutable;
using System.IO;
using Microsoft.DocAsCode.Common;
using HtmlAgilityPack;
using System.Text;
using JeremyTCD.DocFxPlugins.Utils;
using Microsoft.DocAsCode.MarkdownLite;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace JeremyTCD.DocFxPlugins.SearchIndex
{
    [Export(nameof(SearchIndexGenerator), typeof(IPostProcessor))]
    public class SearchIndexGenerator : IPostProcessor
    {
        private static readonly Regex RegexWhiteSpace = new Regex(@"\s+", RegexOptions.Compiled);
        private int SearchIndexSnippetLength;

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
        {
            object length = null;
            metadata.TryGetValue(SearchIndexConstants.SearchIndexSnippetLengthKey, out length);
            SearchIndexSnippetLength = length as int? ?? SearchIndexConstants.DefaultArticleSnippetLength;

            return metadata;
        }

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            if (outputFolder == null)
            {
                throw new ArgumentNullException("Base directory cannot be null");
            }

            Dictionary<string, SearchIndexItem> SearchIndexItems = GetSearchIndexItems(outputFolder, manifest);
            if (SearchIndexItems.Count == 0)
            {
                return manifest;
            }

            OutputSearchIndex(outputFolder, manifest, SearchIndexItems);

            return manifest;
        }

        private void OutputSearchIndex(string outputFolder, Manifest manifest, Dictionary<string, SearchIndexItem> searchIndexItems)
        {
            string indexFile = Path.Combine(outputFolder, SearchIndexConstants.IndexFileName);

            JsonUtility.Serialize(indexFile, searchIndexItems, Formatting.Indented);

            var manifestItem = new ManifestItem
            {
                DocumentType = "Resource",
                Metadata = new Dictionary<string, object>(),
                OutputFiles = new Dictionary<string, OutputFileInfo>()
            };
            manifestItem.OutputFiles.Add("resource", new OutputFileInfo
            {
                RelativePath = PathUtility.MakeRelativePath(outputFolder, indexFile),
            });

            manifest.Files?.Add(manifestItem);
        }

        private Dictionary<string, SearchIndexItem> GetSearchIndexItems(string outputFolder, Manifest manifest)
        {
            Dictionary<string, SearchIndexItem> SearchIndexItems = new Dictionary<string, SearchIndexItem>();

            foreach (ManifestItem manifestItem in manifest.Files)
            {
                object includeInSearchIndex = null;
                manifestItem.Metadata.TryGetValue(SearchIndexConstants.IncludeInSearchIndexKey, out includeInSearchIndex);
                if (includeInSearchIndex as bool? != true)
                {
                    continue;
                }

                string relPath = manifestItem.GetHtmlOutputRelPath();
                HtmlNode articleNode = manifestItem.GetHtmlOutputArticleNode(outputFolder);
                StringBuilder stringBuilder = new StringBuilder();
                ExtractTextFromNode(articleNode, stringBuilder);
                string text = NormalizeNodeText(stringBuilder.ToString());

                HtmlNode snippet = SnippetCreator.CreateSnippet(articleNode, relPath, SearchIndexSnippetLength);

                SearchIndexItems.Add(relPath, new SearchIndexItem
                {
                    RelPath = relPath,
                    SnippetHtml = snippet.OuterHtml,
                    Text = text,
                    Title = snippet.SelectSingleNode("./h1/a").InnerText
                });
            }

            return SearchIndexItems;
        }

        private string NormalizeNodeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            text = StringHelper.HtmlDecode(text);
            return RegexWhiteSpace.Replace(text, " ").Trim();
        }

        private void ExtractTextFromNode(HtmlNode node, StringBuilder stringBuilder)
        {
            if (!node.HasChildNodes)
            {
                stringBuilder.Append(node.InnerText);
                stringBuilder.Append(" ");
            }
            else
            {
                foreach (HtmlNode childNode in node.ChildNodes)
                {
                    ExtractTextFromNode(childNode, stringBuilder);
                }
            }
        }
    }
}
