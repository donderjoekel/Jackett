using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Jackett.Common.Extensions;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Models;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.Madara;

[ExcludeFromCodeCoverage]
public abstract class MadaraIndexer : MangarrIndexerBase
{
    protected MadaraIndexer(IIndexerConfigurationService configService, WebClient client, Logger logger,
                            IProtectionService p, ICacheService cacheService) : base(
        configService, client, logger, p, cacheService)
    {
    }

    protected virtual bool AltMode => false;

    protected override async Task<IEnumerable<ReleaseInfo>> FetchLatestReleases()
    {
        var releases = new List<ReleaseInfo>();

        for (var i = 0; i < LATEST_PAGE_COUNT; i++)
        {
            var data = new Dictionary<string, string>
            {
                { "action", "madara_load_more" },
                { "page", i.ToString() },
                { "template", "madara-core/content/content-archive" },
                { "vars[meta_key]", "_latest_update" },
                { "vars[meta_query][0][relation]", "AND" },
                { "vars[meta_query][relation]", "AND" },
                { "vars[order]", "desc" },
                { "vars[orderby]", "meta_value_num" },
                { "vars[paged]", "1" },
                { "vars[post_status]", "publish" },
                { "vars[post_type]", "wp-manga" },
                { "vars[posts_per_page]", "20" },
                { "vars[timerange]", "" },
            };

            var result = await RequestWithCookiesAndRetryAsync(
                SiteLink + "wp-admin/admin-ajax.php", method: RequestType.POST, data: data, referer: SiteLink,
                emulateBrowser: true);

            if (result.IsRedirect)
            {
                logger.Info("Redirect detected, following...");
                await FollowIfRedirect(result);
            }

            var document = new HtmlParser().ParseDocument(result.ContentString);
            var elements = document.QuerySelectorAll<IHtmlDivElement>(".page-item-detail");
            foreach (var element in elements)
            {
                var titleElement = element.QuerySelector<IHtmlAnchorElement>(".post-title a");
                var title = titleElement.TextContent.Trim();

                var chapterElements = element.QuerySelectorAll<IHtmlDivElement>(".chapter-item");
                foreach (var chapterElement in chapterElements)
                {
                    var chapterUrlElement = chapterElement.QuerySelector<IHtmlAnchorElement>(".chapter a");
                    var chapterUrl = chapterUrlElement.Href;
                    var chapterTitle = chapterUrlElement.TextContent.Trim();
                    var episode = ParseChapterToEpisode(chapterTitle);

                    DateTime parsedDate = DateTime.MinValue;
                    var dateElement = chapterElement.QuerySelector<IHtmlAnchorElement>(".post-on a");
                    if (dateElement != null)
                    {
                        var date = dateElement.GetAttribute("title", string.Empty);
                        if (!string.IsNullOrWhiteSpace(date))
                        {
                            // TODO: Parse date
                            // parsedDate = DateTime.Parse(date);
                        }
                    }

                    releases.Add(CreateReleaseInfo(chapterUrl, title, episode, parsedDate));
                }
            }
        }

        return releases;
    }

    protected override async Task<IEnumerable<ReleaseInfo>> PerformSearch(TorznabQuery query)
    {
        var releases = new List<ReleaseInfo>();

        var data = new Dictionary<string, string>
        {
            { "action", "madara_load_more" },
            { "page", 0.ToString() },
            { "template", "madara-core/content/content-search" },
            { "vars[manga_archives_item_layout]", "big_thumbnail" },
            { "vars[meta_query][0][relation]", "AND" },
            { "vars[meta_query][relation]", "AND" },
            { "vars[paged]", "1" },
            { "vars[post_status]", "publish" },
            { "vars[post_type]", "wp-manga" },
            { "vars[s]", query.SearchTerm },
            { "vars[template]", "search" },
        };

        var result = await RequestWithCookiesAndRetryAsync(
            SiteLink + "wp-admin/admin-ajax.php", method: RequestType.POST, data: data, referer: SiteLink);
        var document = new HtmlParser().ParseDocument(result.ContentString);
        var elements = document.QuerySelectorAll<IHtmlDivElement>(".c-tabs-item__content");
        foreach (var element in elements)
        {
            var titleElement = element.QuerySelector<IHtmlAnchorElement>(".post-title a");
            var title = titleElement.TextContent.Trim();

            result = await RequestWithCookiesAndRetryAsync(titleElement.Href + "ajax/chapters/", method: RequestType.POST);

            document = new HtmlParser().ParseDocument(result.ContentString);
            var chapterElements = document.QuerySelectorAll<IHtmlListItemElement>(".wp-manga-chapter");
            foreach (var chapterElement in chapterElements)
            {
                var urlElement = chapterElement.QuerySelector<IHtmlAnchorElement>("a");
                var url = urlElement.Href;
                var chapterTitle = urlElement.TextContent.Trim();
                var episode = ParseChapterToEpisode(chapterTitle);

                if (query.Episode.IsNotNullOrWhiteSpace() && query.Episode != episode)
                {
                    continue;
                }

                var releaseDateElement = chapterElement.QuerySelector<IHtmlSpanElement>(".chapter-release-date");
                var parsedDate = DateTime.Today;
                if (releaseDateElement != null)
                {
                    var releaseDate = releaseDateElement.TextContent.Trim();
                    if (!string.IsNullOrWhiteSpace(releaseDate))
                    {
                        parsedDate = DateTime.Parse(releaseDate);
                    }
                }

                releases.Add(CreateReleaseInfo(url, title, episode, parsedDate));
            }
        }
        return releases;
    }

    protected override Task<IEnumerable<string>> GetChapterImageLinks(WebResult response)
    {
        var document = new HtmlParser().ParseDocument(response.ContentString);
        var elements = document.QuerySelectorAll<IHtmlImageElement>(".wp-manga-chapter-img");
        if (!elements.Any())
            elements = document.QuerySelectorAll<IHtmlImageElement>(".reading-content img");

        return Task.FromResult(elements.Select(GetUrl));

        string GetUrl(IHtmlImageElement element)
        {
            var url = element.GetAttribute("data-src");
            if (string.IsNullOrEmpty(url))
                url = element.GetAttribute("src");
            return url?.Trim() ?? string.Empty;
        }
    }
}
