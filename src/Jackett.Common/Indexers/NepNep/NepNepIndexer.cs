using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jackett.Common.Extensions;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Models;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils;
using Jackett.Common.Utils.Clients;
using Newtonsoft.Json;
using NLog;

namespace Jackett.Common.Indexers.Abstract
{
    [ExcludeFromCodeCoverage]
    public abstract class NepNepIndexer : MangarrIndexerBase
    {
        protected NepNepIndexer(IIndexerConfigurationService configService, WebClient client, Logger logger, IProtectionService p, ICacheService cacheService) : base(configService, client, logger, p, cacheService)
        {
        }

        protected sealed override async Task<IEnumerable<ReleaseInfo>> PerformSearch(TorznabQuery query)
        {
            var releaseInfo = new List<ReleaseInfo>();
            var queryParameters = new NameValueCollection() { { "name", query.SanitizedSearchTerm } };
            var result = await RequestWithCookiesAndRetryAsync(SiteLink + "search/?" + queryParameters.GetQueryString());
            var match = Regex.Match(result.ContentString, @"(?=Directory =).+?(\[.+?\])\;");
            var json = match.Groups[1].Value;
            var directory = JsonConvert.DeserializeObject<List<DirectoryItem>>(json);
            var items = directory.Where(x => x.Slug.ContainsIgnoreCase(query.SanitizedSearchTerm) || x.al.ContainsIgnoreCase(query.SanitizedSearchTerm)).ToList();
            foreach (var directoryItem in items)
            {
                result = await RequestWithCookiesAndRetryAsync(SiteLink + "manga/" + directoryItem.Index);
                match = Regex.Match(result.ContentString, @"(?=Chapters =).+?(\[.+?\])\;");
                json = match.Groups[1].Value;
                var chapters = JsonConvert.DeserializeObject<List<ChapterInfo>>(json);

                foreach (var chapter in chapters)
                {
                    var url = new Uri(CreateUrl(directoryItem.Index, chapter.Chapter, out var chapterNumber));

                    if (query.Episode.IsNotNullOrWhiteSpace() && query.Episode != chapterNumber.ToString(CultureInfo.InvariantCulture))
                        continue;

                    var release = new ReleaseInfo
                    {
                        Details = url,
                        Title = $"[{Name}] {directoryItem.Slug} - S1E{chapterNumber} [ENG]",
                        PublishDate = DateTime.Parse(chapter.Date),
                        Link = url,
                        Genres = directoryItem.Genres,
                        Category = new List<int> { TorznabCatType.TVAnime.ID },
                        Guid = url,
                        Origin = this,
                        Size = 1,
                        Files = 1,
                        Seeders = 1,
                        Peers = 1,
                        MinimumRatio = 1,
                        MinimumSeedTime = 172800,
                        DownloadVolumeFactor = 1,
                        UploadVolumeFactor = 1
                    };

                    releaseInfo.Add(release);
                }
            }
            return releaseInfo;
        }

        protected sealed override async Task<IEnumerable<ReleaseInfo>> FetchLatestReleases()
        {
            var releaseInfo = new List<ReleaseInfo>();
            var result = await RequestWithCookiesAndRetryAsync(SiteLink);
            var match = Regex.Match(result.ContentString, @"(?=LatestJSON =).+?(\[.+?\])\;");
            var json = match.Groups[1].Value;
            var releases = JsonConvert.DeserializeObject<List<LatestRelease>>(json);
            foreach (var latestRelease in releases)
            {
                var url = new Uri(CreateUrl(latestRelease.IndexName, latestRelease.Chapter, out double chapterNumber));

                var release = new ReleaseInfo
                {
                    Details = url,
                    Title = $"[{Name}] {latestRelease.SeriesName} - S1E{chapterNumber} [ENG]",
                    PublishDate = DateTime.Parse(latestRelease.Date),
                    Link = url,
                    Genres = latestRelease.Genres.Split(','),
                    Category = new List<int> { TorznabCatType.TVAnime.ID },
                    Guid = url,
                    Origin = this,
                    Size = 1,
                    Files = 1,
                    Seeders = 1,
                    Peers = 1,
                    MinimumRatio = 1,
                    MinimumSeedTime = 172800,
                    DownloadVolumeFactor = 1,
                    UploadVolumeFactor = 1
                };
                releaseInfo.Add(release);
            }

            return releaseInfo;
        }

        protected sealed override Task<IEnumerable<string>> GetChapterImageLinks(WebResult response)
        {
            var match = Regex.Match(response.ContentString, @"(?=CurChapter =).+?(\{.+?\})\;");
            var json = match.Groups[1].Value;
            var chapterInfo = JsonConvert.DeserializeObject<ChapterInfo>(json);

            if (!int.TryParse(chapterInfo.Page, out var pageCount))
            {
                throw new InvalidOperationException("Unable to parse page count");
            }

            match = Regex.Match(response.ContentString, @"(?=ng-src=).+\"".+\/manga\/(.+?)\/.+\""");
            var slug = match.Groups[1].Value;

            var directory = string.IsNullOrEmpty(chapterInfo.Directory) ? string.Empty : chapterInfo.Directory + "/";
            var chapterString = chapterInfo.Chapter[1..^1];
            if (chapterInfo.Chapter[^1] != '0')
            {
                chapterString += $".{chapterInfo.Chapter[^1]}";
            }

            match = Regex.Match(response.ContentString,@"(?=CurPathName =).+?(\"".+?\"")\;");
            var urlBase = match.Groups[1].Value.Trim('"');

            var urls = new List<string>();
            for (var i = 0; i < pageCount; i++)
            {
                var s = "000" + (i + 1);
                var page = s[^3..];
                var url = $"https://{urlBase}/manga/{slug}/{directory}{chapterString}-{page}.png";
                urls.Add(url);
            }

            return Task.FromResult<IEnumerable<string>>(urls);
        }

        private string CreateUrl(string indexName, string chapterCode, out double chapterNumber)
        {
            var volume = int.Parse(chapterCode[..1]);
            var index = volume != 1 ? "-index-" + volume : string.Empty;
            var n = int.Parse(chapterCode[1..^1]);
            var a = int.Parse(chapterCode[^1].ToString());
            var m = a != 0 ? "." + a : string.Empty;
            var id = indexName + "-chapter-" + n + m + index + ".html";
            chapterNumber = n + a * 0.1;
            var chapterUrl = SiteLink + "read-online/" + id;
            return chapterUrl;
        }

        private class DirectoryItem
        {
            [JsonProperty("i")]
            public string Index { get; set; }
            [JsonProperty("s")]
            public string Slug { get; set; }
            [JsonProperty("o")]
            public string Official { get; set; }
            [JsonProperty("ss")]
            public string ScanStatus { get; set; }
            [JsonProperty("ps")]
            public string PublishStatus { get; set; }
            [JsonProperty("t")]
            public string Type { get; set; }
            public string v { get; set; }
            public string vm { get; set; }
            [JsonProperty("y")]
            public string Year { get; set; }
            [JsonProperty("a")]
            public string[] Authors { get; set; }
            public string[] al { get; set; }
            [JsonProperty("l")]
            public string LatestChapter { get; set; }
            [JsonProperty("lt")]
            public long LastUpdated { get; set; }
            [JsonProperty("ls")]
            public string LastUpdatedString { get; set; }
            [JsonProperty("g")]
            public string[] Genres { get; set; }
            [JsonProperty("h")]
            public bool IsHot { get; set; }
        }

        private class ChapterInfo
        {
            public string Chapter { get; set; }
            public string Type { get; set; }
            public string Date { get; set; }
            public string ChapterName { get; set; }
            public string Page { get; set; }
            public string Directory { get; set; }
        }

        private class LatestRelease
        {
            [JsonProperty("SeriesID")]
            public string SeriesId { get; set; }
            public string IndexName { get; set; }
            public string SeriesName { get; set; }
            public string ScanStatus { get; set; }
            public string Chapter { get; set; }
            public string Genres { get; set; }
            public string Date { get; set; }
            public bool IsEdd { get; set; }
        }
    }
}
