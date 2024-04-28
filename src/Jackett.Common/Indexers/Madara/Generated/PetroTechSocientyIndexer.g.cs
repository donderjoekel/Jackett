// Auto generated

using System.Diagnostics.CodeAnalysis;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils.Clients;
using NLog;

namespace Jackett.Common.Indexers.Madara
{
    [ExcludeFromCodeCoverage]
    public class PetroTechSocienty : MadaraIndexer
    {
        public PetroTechSocienty(IIndexerConfigurationService configService,
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

        public override string Id => "petrotechsocienty-madara";
        public override string Name => "Petro Tech Society";
        public override string SiteLink { get; protected set; } = "https://www.petrotechsociety.org/";
        protected override bool AltMode => false;
    }
}
