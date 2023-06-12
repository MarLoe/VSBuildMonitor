using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using com.lobger.WebSocketServer;

namespace com.lobger.vsbuildmonitor
{
    class StartupHandler : CommandHandler
    {
        private CancellationTokenSource? _buildCancellationTokenSource;

        protected override void Run()
        {
            var server = new WebSocketServer.WebSocketServer("http://localhost:11011/");
            _ = server.Start();

            IdeServices.ProjectOperations.StartBuild += ProjectOperations_StartBuild;
            IdeServices.ProjectOperations.EndBuild += ProjectOperations_EndBuild;
        }

        private void ProjectOperations_StartBuild(object sender, BuildEventArgs args)
        {
            if (_buildCancellationTokenSource is null)
            {
                _buildCancellationTokenSource = new CancellationTokenSource();
                _ = MonitorBuild(args.ProgressMonitor, _buildCancellationTokenSource.Token);
            }
        }

        private void ProjectOperations_EndBuild(object sender, BuildEventArgs args)
        {
            _buildCancellationTokenSource?.Cancel();
        }

        private async Task MonitorBuild(ProgressMonitor progressMonitor, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($@"Progress: {progressMonitor.Progress}");
                if (progressMonitor.CurrentTask is not null)
                {
                    System.Diagnostics.Debug.WriteLine($@"\t{progressMonitor.CurrentTask.Name}: {progressMonitor.CurrentTask.Progress}");
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            _buildCancellationTokenSource = null;
        }

    }
}
