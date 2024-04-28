using System;
using System.Collections.Generic;
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
using NLog;

namespace Jackett.Common.Indexers.Abstract
{
    public abstract class BuddyComplexIndexer : MangarrIndexerBase
    {

        public BuddyComplexIndexer(IIndexerConfigurationService configService, WebClient client, Logger logger, IProtectionService p, ICacheService cacheService) : base(configService, client, logger, p, cacheService)
        {
        }

        protected override async Task<IEnumerable<ReleaseInfo>> FetchLatestReleases()
        {
            var releases = new List<ReleaseInfo>();
            var result = await RequestWithCookiesAndRetryAsync(SiteLink);
            var htmlDocument = new HtmlParser().ParseDocument(result.ContentString);
            var elements = htmlDocument.QuerySelectorAll<IHtmlDivElement>(".book-item.latest-item");
            foreach (var element in elements)
            {
                var anchor = element.QuerySelector<IHtmlAnchorElement>("div.title a");
                var title = anchor.TextContent.Trim();
                var chapterElements = element.QuerySelectorAll<IHtmlDivElement>("div.chapters div.chap-item");
                foreach (var chapterElement in chapterElements)
                {
                    var anchorElement = chapterElement.QuerySelector<IHtmlAnchorElement>("a");
                    var updatedElement = chapterElement.QuerySelector<IHtmlDivElement>("div.updated-date");
                    releases.Add(
                        CreateReleaseInfo(
                            SiteLink + anchorElement.PathName.Trim('/'), title,
                            ParseChapterToEpisode(anchorElement.TextContent.Trim()),
                            ParseHumanReleaseDate(updatedElement.TextContent.Trim())));
                }
            }

            return releases.Where(x => x.PublishDate < DateTime.Now);
        }

        protected override async Task<IEnumerable<ReleaseInfo>> PerformSearch(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var result = await RequestWithCookiesAndRetryAsync(SiteLink + "search?page=1&q=" + query.SanitizedSearchTerm);
            var document = new HtmlParser().ParseDocument(result.ContentString);
            var elements = document.QuerySelectorAll<IHtmlAnchorElement>("div.book-detailed-item .meta .title a");
            foreach (var element in elements)
            {
                var url = SiteLink + "api/manga/" + element.PathName.Trim('/') + "/chapters?source=detail";
                result = await RequestWithCookiesAndRetryAsync(url);
                document = new HtmlParser().ParseDocument(result.ContentString);
                var anchorElements = document.QuerySelectorAll<IHtmlAnchorElement>("li a");
                foreach (var anchorElement in anchorElements)
                {
                    var titleElement = anchorElement.QuerySelector<IHtmlElement>(".chapter-title");
                    var episode = ParseChapterToEpisode(titleElement.TextContent.Trim());

                    if (query.Episode.IsNotNullOrWhiteSpace() && query.Episode != episode)
                    {
                        continue;
                    }

                    var dateElement = anchorElement.QuerySelector<IHtmlElement>(".chapter-update");
                    var date = dateElement.TextContent.Trim();
                    DateTime.TryParse(date, out var parsedDate);

                    releases.Add(
                        CreateReleaseInfo(
                            SiteLink + anchorElement.PathName.Trim('/'), element.TextContent.Trim(), episode, parsedDate));
                }
            }
            return releases;
        }

        protected override Task<IEnumerable<string>> GetChapterImageLinks(WebResult response)
        {
            var match = Regex.Match(response.ContentString, @"chapImages\s=\s'(.+)(?=')");
            return Task.FromResult<IEnumerable<string>>(match.Groups[1].Value.Split(','));
        }
    }
}
