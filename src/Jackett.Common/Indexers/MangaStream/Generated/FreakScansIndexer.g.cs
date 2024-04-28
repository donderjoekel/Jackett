// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.MangaStream
{
    [ExcludeFromCodeCoverage]
    public class FreakScans : MangaStreamIndexer
    {
        public FreakScans(IIndexerConfigurationService configService,
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

        public override string Id => "freakscans-mangastream";
        public override string Name => "Freak Scans";
        public override string SiteLink { get; protected set; } = "https://freakscans.com/";
        protected override string Key => "manga";
    }
}
