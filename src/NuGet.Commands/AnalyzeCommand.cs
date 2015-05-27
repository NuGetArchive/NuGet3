using System.Threading.Tasks;
using NuGet.Logging;

namespace NuGet.Commands
{
    public class AnalyzeCommand
    {
        private readonly ILogger _log;

        public AnalyzeCommand(ILogger logger)
        {
            _log = logger;
        }

        public Task<AnalyzeResult> ExecuteAsync(AnalyzeRequest request)
        {
            _log.LogInformation($"Analyzing {request.Project.Name} for use in {request.TargetFramework} on {request.RuntimeIdentifier}");

            return Task.FromResult(new AnalyzeResult());
        }
    }
}
