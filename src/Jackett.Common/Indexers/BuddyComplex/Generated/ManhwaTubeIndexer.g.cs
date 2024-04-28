// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.BuddyComplex
{
    [ExcludeFromCodeCoverage]
    public class ManhwaTube : BuddyComplexIndexer
    {
        public ManhwaTube(IIndexerConfigurationService configService,
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

        public override string Id => "manhwatube-buddycomplex";
        public override string Name => "Manhwa Tube";
        public override string SiteLink { get; protected set; } = "https://manhwatube.com/";

    }
}
