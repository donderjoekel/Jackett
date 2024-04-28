// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.Madara
{
    [ExcludeFromCodeCoverage]
    public class GirlsLoveManga : MadaraIndexer
    {
        public GirlsLoveManga(IIndexerConfigurationService configService,
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

        public override string Id => "girlslovemanga-madara";
        public override string Name => "Girls Love Manga";
        public override string SiteLink { get; protected set; } = "https://glmanga.com/";
        protected override bool AltMode => false;
    }
}
