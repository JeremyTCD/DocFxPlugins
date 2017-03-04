using HtmlAgilityPack;
using Microsoft.DocAsCode.Plugins;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace JeremyTCD.DocFxPlugins.Shared
{
    public static class ManifestItemExtensions
    {
        public static string GetHtmlOutputRelPath(this ManifestItem manifestItem)
        {
            return manifestItem.OutputFiles.First(o => o.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)).Value.RelativePath;
        }

        public static HtmlDocument GetHtmlOutputDoc(this ManifestItem manifestItem, string outputFolder)
        {
            string relPath = manifestItem.GetHtmlOutputRelPath();

            HtmlDocument htmlDoc = new HtmlDocument();
            try
            {
                htmlDoc.Load(Path.Combine(outputFolder, relPath), Encoding.UTF8);
            }
            catch
            {
                throw new InvalidDataException($"{nameof(ManifestItemExtensions)}: Html output {relPath} could not be loaded");
            }

            return htmlDoc;
        }

        public static HtmlNode GetHtmlOutputArticleNode(this ManifestItem manifestItem, string outputFolder)
        {
            HtmlNode articleNode = manifestItem.
                GetHtmlOutputDoc(outputFolder).
                DocumentNode.
                SelectSingleNode("//article[@class=\"main-article\"]");

            if (articleNode == null)
            {
                throw new InvalidDataException($"{nameof(ManifestItemExtensions)}: Html output {manifestItem.GetHtmlOutputRelPath()} has no article node");
            }

            return articleNode;
        }
    }
}
