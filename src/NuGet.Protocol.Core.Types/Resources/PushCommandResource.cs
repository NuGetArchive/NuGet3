using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.Types
{
    public class PushCommandResource : INuGetResource
    {
        public string PushEndpoint { get; }
        public PushCommandResource(string pushEndpoint)
        {
            if (pushEndpoint == null)
            {
                throw new ArgumentNullException(nameof(pushEndpoint));
            }

            PushEndpoint = pushEndpoint;
        }

        public Task<string> GetPushEndpointAsync(CancellationToken token)
        {
            return Task.FromResult<string>(PushEndpoint);
        }
    }
}
