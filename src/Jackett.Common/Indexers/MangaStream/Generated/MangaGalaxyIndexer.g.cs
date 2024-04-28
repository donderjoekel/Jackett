// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.MangaStream
{
    [ExcludeFromCodeCoverage]
    public class MangaGalaxy : MangaStreamIndexer
    {
        public MangaGalaxy(IIndexerConfigurationService configService,
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

        public override string Id => "mangagalaxy-mangastream";
        public override string Name => "Manga Galaxy";
        public override string SiteLink { get; protected set; } = "https://mangagalaxy.me/";
        protected override string Key => "series";
    }
}
