﻿using Microsoft.DocAsCode.Plugins;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Immutable;
using Microsoft.DocAsCode.Build.ConceptualDocuments;
using System.Dynamic;
using System.IO;
using System.Globalization;

namespace DocFxPlugins
{
    [Export(nameof(ConceptualDocumentProcessor), typeof(IDocumentBuildStep))]
    public class ArticleListBuildStep : IDocumentBuildStep
    {
        public int BuildOrder => 10;

        public string Name => nameof(ArticleListBuildStep);

        public void Build(FileModel model, IHostService host)
        {
            // Do nothing
        }

        public void Postbuild(ImmutableList<FileModel> models, IHostService host)
        {
            foreach(FileModel model in models)
            {
                Dictionary<string, object> content = model.Content as Dictionary<string, object>;
                if(content != null)
                {
                    object obj = null;

                    content.TryGetValue("jr.includeInArticleList", out obj);
                    bool includeInArticleList = obj is bool && (bool) obj ? true: false;
                    if (includeInArticleList)
                    {
                        IDictionary<string, object> manifestProperties = model.ManifestProperties as IDictionary<string, object>;

                        manifestProperties.Add("jr.includeInArticleList", true);

                        content.TryGetValue("jr.date", out obj);
                        DateTime date = default(DateTime);
                        try
                        {
                            date = DateTime.ParseExact(obj as string, "d", new CultureInfo("en-us"));
                        }
                        catch
                        {
                            throw new InvalidDataException($"{nameof(ArticleListPostProcessor)}: Article {model.Key}'s date is invalid");
                        }

                        manifestProperties.Add("jr.date", date);
                    }

                    content.TryGetValue("jr.enableArticleList", out obj);
                    bool enableArticleList = obj is bool && (bool)obj ? true : false;
                    if (enableArticleList)
                    {
                        IDictionary<string, object> manifestProperties = model.ManifestProperties as IDictionary<string, object>;

                        manifestProperties.Add("jr.enableArticleList", true);
                    }
                }
            }

            return;
        }

        public IEnumerable<FileModel> Prebuild(ImmutableList<FileModel> models, IHostService host)
        {
            // Do nothing
            return models;
        }
    }
}
