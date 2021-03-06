﻿using HtmlAgilityPack;

namespace JeremyTCD.DocFxPlugins.Utils
{
    public class SnippetCreator
    {
        public static HtmlNode CreateSnippet(HtmlNode article, string href, int snippetLength)
        {
            HtmlNode snippet = HtmlNode.CreateNode("<article></article>");
            snippet.AppendChildren(article.ChildNodes);
            HtmlNode titleAnchorNode = HtmlNode.CreateNode($"<a href=\"/{href}\"></a>");
            HtmlNode titleNode = snippet.SelectSingleNode(".//h1");
            titleAnchorNode.InnerHtml = titleNode.InnerText;
            titleNode.RemoveAllChildren();
            titleNode.AppendChild(titleAnchorNode);

            TrimNode(snippet, 0, snippetLength);

            HtmlNodeCollection headers = snippet.SelectNodes(".//*[self::h2 or self::h3 or self::h4 or self::h5 or self::h6]");
            if (headers != null)
            {
                foreach (HtmlNode node in headers)
                {
                    node.Attributes.Add("class", "no-anchor" + node.Attributes["class"]?.Value ?? "");
                }
            }

            return snippet;
        }

        private static int TrimNode(HtmlNode node, int currentSnippetLength, int snippetLength)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                string text = node.InnerText.Trim();
                currentSnippetLength += text.Length;

                if (snippetLength > currentSnippetLength)
                {
                    return currentSnippetLength;
                }

                int endIndex = text.IndexOfAny(new char[] { ' ', '.', ',', '!', '?', ';' }, text.Length - (currentSnippetLength - snippetLength) - 1);

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
                currentSnippetLength = TrimNode(childNode, currentSnippetLength, snippetLength);
                if (currentSnippetLength >= snippetLength)
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
