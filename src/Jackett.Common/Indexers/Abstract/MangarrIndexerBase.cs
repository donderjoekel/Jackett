using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BencodeNET.Objects;
using BencodeNET.Torrents;
using Jackett.Common.Extensions;
using Jackett.Common.Models;
using Jackett.Common.Models.IndexerConfig;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using Newtonsoft.Json.Linq;
using NLog;
using WebClient = Jackett.Common.Utils.Clients.WebClient;

namespace Jackett.Common.Indexers.Abstract
{
    [ExcludeFromCodeCoverage]
    public abstract class MangarrIndexerBase : IndexerBase
    {
        protected const int LATEST_PAGE_COUNT = 1;

        private static readonly Regex inDaysRegex = new Regex(@"in (\d+) days", RegexOptions.IgnoreCase);
        private static readonly Regex inHoursRegex = new Regex(@"in (\d+) hours", RegexOptions.IgnoreCase);
        private static readonly Regex secondsAgoRegex = new Regex(@"(\d+) seconds? ago", RegexOptions.IgnoreCase);
        private static readonly Regex minutesAgoRegex = new Regex(@"(\d+) minutes? ago", RegexOptions.IgnoreCase);
        private static readonly Regex hoursAgoRegex = new Regex(@"(\d+) hours? ago", RegexOptions.IgnoreCase);
        private static readonly Regex daysAgoRegex = new Regex(@"(\d+) days? ago", RegexOptions.IgnoreCase);
        private static readonly Regex monthsAgoRegex = new Regex(@"(\d+) months? ago", RegexOptions.IgnoreCase);

        private static readonly Regex chapterRegex = new Regex(@"[Cc]hapter\s(\d+(\.\d+)?)");

        protected MangarrIndexerBase(IIndexerConfigurationService configService, WebClient client, Logger logger, IProtectionService p, ICacheService cacheService)
            : base(configService: configService, client: client, logger: logger, p: p, cacheService: cacheService, configData: new ConfigurationData())
        {
        }

        public sealed override TorznabCapabilities TorznabCaps => SetCapabilities();

        public override bool SupportsPagination => true;
        public override string Type => "public";
        public override string Language => "en-US";
        public override string Description => string.Empty;
        protected virtual string ImageReferrer => SiteLink;

        private TorznabCapabilities SetCapabilities()
        {
            var caps = new TorznabCapabilities()
            {
                TvSearchParams = new List<TvSearchParam>() { TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.Ep }
            };

            caps.Categories.AddCategoryMapping(1, TorznabCatType.TVAnime);

            return caps;
        }

        public sealed override async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            LoadValuesFromJson(configJson);
            var releases = await PerformQuery(new TorznabQuery());

            await ConfigureIfOK(
                string.Empty, releases.Any(), () => throw new Exception($"Could not find releases for '{GetType().Name}' using URL '{SiteLink}'"));

            return IndexerConfigurationStatus.Completed;
        }

        protected sealed override Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query) =>
            query.IsTest || string.IsNullOrWhiteSpace(query.SearchTerm) ? FetchLatestReleases() : PerformSearch(query);

        protected abstract Task<IEnumerable<ReleaseInfo>> FetchLatestReleases();
        protected abstract Task<IEnumerable<ReleaseInfo>> PerformSearch(TorznabQuery query);

        public sealed override async Task<byte[]> Download(Uri link)
        {
            var response = await RequestWithCookiesAndRetryAsync(link.ToString());

            if (response.IsRedirect)
            {
                await FollowIfRedirect(response);
            }

            var imageLinks = await GetChapterImageLinks(response);
            var files = new MultiFileInfoList();
            files.AddRange(imageLinks.Select(x => new MultiFileInfo() { FullPath = x, FileSize = 1 }));
            var torrent = new Torrent
            {
                ExtraFields = new BDictionary(),
                PieceSize = 1,
                Pieces = Enumerable.Range(0, 20).Select(x => (byte)x).ToArray(),
                Files = files
            };
            torrent.ExtraFields.Add("referer", ImageReferrer);
            using var stream = new MemoryStream();
            await torrent.EncodeToAsync(stream);
            return stream.ToArray();
        }

        protected abstract Task<IEnumerable<string>> GetChapterImageLinks(WebResult response);

        protected string ParseChapterToEpisode(string chapter)
        {
            return chapterRegex.Match(chapter).Groups[1].Value.Trim();
        }

        protected ReleaseInfo CreateReleaseInfo(string url, string title, int chapterNumber, DateTime parsedDate)
        {
            return CreateReleaseInfo(url, title, chapterNumber.ToString(NumberFormatInfo.InvariantInfo), parsedDate);
        }

        protected ReleaseInfo CreateReleaseInfo(string url, string title, double chapterNumber, DateTime parsedDate)
        {
            return CreateReleaseInfo(url, title, chapterNumber.ToString(NumberFormatInfo.InvariantInfo), parsedDate);
        }

        protected ReleaseInfo CreateReleaseInfo(string url, string title, string chapterNumber, DateTime parsedDate)
        {
            var uri = new Uri(url);
            return new ReleaseInfo()
            {
                Details = uri,
                Title = $"[{Name}] {title} - S01E{chapterNumber}",
                PublishDate = parsedDate,
                Link = uri,
                Category = new List<int> { TorznabCatType.TVAnime.ID },
                Guid = uri,
                Origin = this,
                Size = 1,
                Files = 1,
                Seeders = 1,
                Peers = 1,
                MinimumRatio = 1,
                MinimumSeedTime = 172800,
                DownloadVolumeFactor = 1,
                UploadVolumeFactor = 1,
            };
        }

        protected static DateTime ParseHumanReleaseDate(string input)
        {
            if (inDaysRegex.IsMatch(input))
            {
                return DateTime.Now.AddDays(int.Parse(inDaysRegex.Match(input).Groups[1].Value));
            }

            if (string.Equals(input, "in a day", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.Now.AddDays(1);
            }

            if (inHoursRegex.IsMatch(input))
            {
                return DateTime.Now.AddHours(int.Parse(inHoursRegex.Match(input).Groups[1].Value));
            }

            if (string.Equals(input, "in an hour", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.Now.AddHours(1);
            }

            if (secondsAgoRegex.IsMatch(input))
            {
                return DateTime.Now.AddSeconds(-int.Parse(secondsAgoRegex.Match(input).Groups[1].Value));
            }

            if (minutesAgoRegex.IsMatch(input))
            {
                return DateTime.Now.AddMinutes(-int.Parse(minutesAgoRegex.Match(input).Groups[1].Value));
            }

            if (string.Equals(input, "an hour ago", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.Now.AddHours(-1);
            }

            if (hoursAgoRegex.IsMatch(input))
            {
                return DateTime.Now.AddHours(-int.Parse(hoursAgoRegex.Match(input).Groups[1].Value));
            }

            if (string.Equals(input, "a day ago", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.Now.AddDays(-1);
            }

            if (daysAgoRegex.IsMatch(input))
            {
                return DateTime.Now.AddDays(-int.Parse(daysAgoRegex.Match(input).Groups[1].Value));
            }

            if (string.Equals(input, "a month ago", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.Now.AddMonths(-1);
            }

            if (monthsAgoRegex.IsMatch(input))
            {
                return DateTime.Now.AddMonths(-int.Parse(monthsAgoRegex.Match(input).Groups[1].Value));
            }

            return DateTime.MinValue;
        }
    }
}
