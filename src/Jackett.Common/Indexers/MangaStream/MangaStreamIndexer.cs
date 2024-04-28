using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Jackett.Common.Extensions;
using Jackett.Common.Models;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using Newtonsoft.Json;
using NLog;

namespace Jackett.Common.Indexers.Abstract
{
    [ExcludeFromCodeCoverage]
    public abstract class MangaStreamIndexer : MangarrIndexerBase
    {
        protected MangaStreamIndexer(IIndexerConfigurationService configService, WebClient client, Logger logger,
                                     IProtectionService p, ICacheService cacheService) : base(
            configService, client, logger, p, cacheService)
        {
        }

        protected virtual bool EmulateBrowser { get; }
        protected virtual bool UsePostId { get; } = true;
        protected virtual string Key => "manga";

        protected override async Task<IEnumerable<ReleaseInfo>> FetchLatestReleases()
        {
            var releases = new List<ReleaseInfo>();
            var result = await RequestWithCookiesAndRetryAsync(SiteLink, emulateBrowser: true);
            await FollowIfRedirect(result);
            var document = new HtmlParser().ParseDocument(result.ContentString);
            var container = GetLatestReleasesContainer(document);
            if (container == null)
            {
                logger.Warn($"Unable to find latest releases container on '{SiteLink}'");
                return new List<ReleaseInfo>();
            }

            var elements = container.QuerySelectorAll<IHtmlDivElement>("div.uta");
            if (!elements.Any())
                elements = container.QuerySelectorAll<IHtmlDivElement>("div.bsx");

            foreach (var element in elements)
            {
                var title = element.QuerySelector<IHtmlAnchorElement>("a")?.Title;
                if (title.IsNullOrWhiteSpace())
                {
                    logger.Warn("Unable to determine title for '{0}'", element.InnerHtml);
                    continue;
                }

                var listElements = element.QuerySelectorAll<IHtmlListItemElement>("li");
                foreach (var listElement in listElements)
                {
                    var urlElement = listElement.QuerySelector<IHtmlAnchorElement>("a");
                    if (urlElement == null)
                    {
                        logger.Warn("Unable to find URL element in '{0}'", listElement.InnerHtml);
                        continue;
                    }

                    var dateElement = listElement.QuerySelector<IHtmlSpanElement>("span");
                    var releaseDate = ParseHumanReleaseDate(dateElement.TextContent.Trim());

                    releases.Add(CreateReleaseInfo(urlElement.Href, title, ParseChapterToEpisode(urlElement.TextContent.Trim()), releaseDate));
                }
            }

            // for (var i = 0; i < LATEST_PAGE_COUNT; i++)
            // {
            //     var result = await RequestWithCookiesAndRetryAsync(SiteLink + $"{Key}/?order=update&page={i + 1}", emulateBrowser: EmulateBrowser);
            //     while (result.IsRedirect)
            //     {
            //         await FollowIfRedirect(result);
            //     }
            //     var document = new HtmlParser().ParseDocument(result.ContentString);
            //     var elements = document.QuerySelectorAll<IHtmlAnchorElement>("div.bsx a");
            //     foreach (var element in elements)
            //     {
            //         var chapterNumber = GetEpisode(element);
            //         if (string.IsNullOrEmpty(chapterNumber))
            //         {
            //             logger.Warn("Unable to parse chapter number from {0}", element.Href);
            //             continue;
            //         }
            //
            //         var url = element.Href.TrimEnd('/');
            //         url = url.Replace(Key + "/", string.Empty);
            //         url += "-chapter-" + chapterNumber;
            //         var titleElement = element.QuerySelector<IHtmlDivElement>("div.tt");
            //         releases.Add(CreateReleaseInfo(url, titleElement.TextContent.Trim(), chapterNumber, DateTime.MinValue));
            //     }
            // }

            return releases;
        }

        private static IHtmlDivElement GetLatestReleasesContainer(IHtmlDocument document)
        {
            foreach (var element in document.QuerySelectorAll<IHtmlDivElement>("div.bixbox"))
            {
                foreach (var headingElement in element.QuerySelectorAll<IHtmlHeadingElement>("h2"))
                {
                    var content = headingElement.TextContent.Trim();

                    if (content.EqualsIgnoreCase("project updates") || content.EqualsIgnoreCase("project update"))
                    {
                        return element;
                    }

                    if (content.EqualsIgnoreCase("latest updates") || content.EqualsIgnoreCase("latest update"))
                    {
                        return element;
                    }
                }
            }

            return null;
        }

        protected virtual string GetEpisode(IHtmlAnchorElement element)
        {
            var episodeElement = element.QuerySelector<IHtmlDivElement>("div.epxs");
            if (episodeElement != null)
            {
                var episode = episodeElement.TextContent.Trim();
                return ParseChapterToEpisode(episode);
            }

            var match = Regex.Match(element.InnerHtml, @"epxs\"">(.+)<\/div>");
            return ParseChapterToEpisode(match.Groups[1].Value);
        }

        protected override async Task<IEnumerable<ReleaseInfo>> PerformSearch(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var result = await RequestWithCookiesAndRetryAsync(
                SiteLink + "wp-admin/admin-ajax.php", method: RequestType.POST, data: new[]
                {
                    new KeyValuePair<string, string>("action", "ts_ac_do_search"),
                    new KeyValuePair<string, string>("ts_ac_query", query.SearchTerm),
                }, emulateBrowser: EmulateBrowser);
            var response = JsonConvert.DeserializeObject<SearchResponse>(result.ContentString);
            foreach (var series in response.series)
            {
                foreach (var all in series.all)
                {
                    result = await RequestWithCookiesAndRetryAsync(UsePostId ? SiteLink + "?p=" + all.ID : all.post_link, emulateBrowser: EmulateBrowser);
                    await FollowIfRedirect(result);

                    var document = new HtmlParser().ParseDocument(result.ContentString);
                    var elements = document.QuerySelectorAll<IHtmlListItemElement>("#chapterlist li");

                    foreach (var element in elements)
                    {
                        Match match = Regex.Match(element.GetAttribute("data-num"), @"(\d+\.?\d?)+");
                        double chapterNumber = 0;
                        if (!match.Success || match.Groups.Count <= 1)
                        {
                            continue;
                        }

                        chapterNumber = double.Parse(match.Groups[1].Value);
                        if (query.Episode.IsNotNullOrWhiteSpace() &&
                            query.Episode != chapterNumber.ToString(CultureInfo.InvariantCulture))
                        {
                            continue;
                        }

                        var anchorElement = element.FindDescendant<IHtmlAnchorElement>();
                        // var titleElement = element.QuerySelector<IHtmlSpanElement>(".chapternum");
                        var dateElement = element.QuerySelector<IHtmlSpanElement>(".chapterdate");
                        if (!DateTime.TryParse(dateElement.TextContent, out var date))
                        {
                            date = DateTime.UtcNow; // Maybe subtract something?
                        }

                        releases.Add(CreateReleaseInfo(anchorElement.Href, all.post_title, chapterNumber, date));
                    }
                }
            }

            return releases;
        }

        protected override Task<IEnumerable<string>> GetChapterImageLinks(WebResult response)
        {
            var possibleRegex = new List<Regex>()
            {
                new Regex(@"ts_reader\.run\((\{.+\}),"),
                new Regex(@"ts_reader\.run\((.*?(?=\);|},))")
            };

            foreach (var regex in possibleRegex)
            {
                try
                {
                    var links = GetChapterImageLinks(response, regex);
                    if (links.Any())
                        return Task.FromResult<IEnumerable<string>>(links);
                }
                catch (Exception e)
                {
                    // Suppress
                }
            }

            return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
        }

        private string[] GetChapterImageLinks(WebResult result, Regex regex)
        {
            var match = regex.Match(result.ContentString);
            if (!match.Success)
            {
                return Array.Empty<string>();
            }

            var json = match.Groups[1].Value;
            var loaderData = JsonConvert.DeserializeObject<LoaderData>(json);

            if (loaderData.sources.Length != 1)
            {
                throw new InvalidOperationException("Unexpected number of sources found in loader data");
            }

            return loaderData.sources.First().images;
        }

        private class SearchResponse
        {
            public Series[] series { get; set; }

            public class Series
            {
                public All[] all { get; set; }

                public class All
                {
                    public int ID { get; set; }
                    public string post_image { get; set; }
                    public string post_title { get; set; }
                    public string post_genres { get; set; }
                    public string post_type { get; set; }
                    public string post_status { get; set; }
                    public string post_link { get; set; }
                    public string post_latest { get; set; }
                }
            }
        }

        private class LoaderData
        {
            public Source[] sources { get; set; }

            public class Source
            {
                public string[] images { get; set; }
            }
        }
    }
}
