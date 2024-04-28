// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.BuddyComplex
{
    [ExcludeFromCodeCoverage]
    public class MangaSpin : BuddyComplexIndexer
    {
        public MangaSpin(IIndexerConfigurationService configService,
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

        public override string Id => "mangaspin-buddycomplex";
        public override string Name => "Manga Spin";
        public override string SiteLink { get; protected set; } = "https://mangaspin.com/";

    }
}
