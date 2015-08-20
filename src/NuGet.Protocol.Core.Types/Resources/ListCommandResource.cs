using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.Types
{
    public class ListCommandResource : INuGetResource
    {
        public string ListEndpoint { get; }
        public ListCommandResource(string listEndpoint)
        {
            if (listEndpoint == null)
            {
                throw new ArgumentNullException(nameof(listEndpoint));
            }

            ListEndpoint = listEndpoint;
        }

        public Task<string> GetListEndpointAsync(CancellationToken token)
        {
            return Task.FromResult<string>(ListEndpoint);
        }
    }
}
