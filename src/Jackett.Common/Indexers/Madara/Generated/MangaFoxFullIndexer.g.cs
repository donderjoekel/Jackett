// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.Madara
{
    [ExcludeFromCodeCoverage]
    public class MangaFoxFull : MadaraIndexer
    {
        public MangaFoxFull(IIndexerConfigurationService configService,
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

        public override string Id => "mangafoxfull-madara";
        public override string Name => "Manga Fox Full";
        public override string SiteLink { get; protected set; } = "https://mangafoxfull.com/";
        protected override bool AltMode => false;
    }
}
