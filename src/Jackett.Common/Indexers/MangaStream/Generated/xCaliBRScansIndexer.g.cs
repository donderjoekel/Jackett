// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.MangaStream
{
    [ExcludeFromCodeCoverage]
    public class xCaliBRScans : MangaStreamIndexer
    {
        public xCaliBRScans(IIndexerConfigurationService configService,
                          WebClient client,
                          Logger logger,
                          IProtectionService p,
                          ICacheService cacheService)
            : base(configService: configService,
                   client: client,
                   logger: logger,
                   p: p,
                   cacheService: cacheService)
        {
        }

        public override string Id => "xcalibrscans-mangastream";
        public override string Name => "xCaliBR Scans";
        public override string SiteLink { get; protected set; } = "https://xcalibrscans.com/webcomics/";
        protected override string Key => "manga";
    }
}
