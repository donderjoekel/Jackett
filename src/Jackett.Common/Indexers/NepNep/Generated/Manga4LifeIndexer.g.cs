// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.NepNep
{
    [ExcludeFromCodeCoverage]
    public class Manga4Life : NepNepIndexer
    {
        public Manga4Life(IIndexerConfigurationService configService,
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

        public override string Id => "manga4life-nepnep";
        public override string Name => "Manga Life";
        public override string SiteLink { get; protected set; } = "https://manga4life.com/";

    }
}
