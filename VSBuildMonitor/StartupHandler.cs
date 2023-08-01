using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using WebMessage.Server;

namespace com.lobger.vsbuildmonitor
{
    class StartupHandler : CommandHandler
    {
        private WebMessageServer? _webMessageServer;

        private CancellationTokenSource? _cleanCancellationTokenSource;
        private CancellationTokenSource? _buildCancellationTokenSource;


        protected override void Run()
        {
            IdeServices.Workspace.SolutionLoaded += Workspace_SolutionLoaded;
            IdeServices.Workspace.SolutionUnloaded += Workspace_SolutionUnloaded;

            IdeServices.ProjectOperations.StartClean += ProjectOperations_StartClean;
            IdeServices.ProjectOperations.EndClean += ProjectOperations_EndClean;

            IdeServices.ProjectOperations.StartBuild += ProjectOperations_StartBuild;
            IdeServices.ProjectOperations.EndBuild += ProjectOperations_EndBuild;
        }

        private void Workspace_SolutionLoaded(object? sender, SolutionEventArgs e)
        {
            _webMessageServer?.Dispose();
            _webMessageServer = null;

            var port = 13001;
            while (_webMessageServer is null)
            {
                try
                {
                    _webMessageServer = new(port, true);
                    var service = _webMessageServer.AddService("/", r =>
                    {
                        var answer = MessageService.AskQuestion($@"Allow connection from {r.Key}?", new[] { AlertButton.Proceed, AlertButton.Discard });
                        var key = answer == AlertButton.Proceed ? $@"MARTIN {Guid.NewGuid()}" : string.Empty;
                        return Task.FromResult(key);
                    });
                }
                catch (InvalidOperationException ex) when (ex.InnerException is SocketException)
                {
                    port++;
                }
            }
        }

        private void Workspace_SolutionUnloaded(object? sender, SolutionEventArgs e)
        {
            _webMessageServer?.Dispose();
            _webMessageServer = null;
        }

        private void ProjectOperations_StartClean(object sender, CleanEventArgs args)
        {
            StartMonitor(args.Monitor, ref _cleanCancellationTokenSource);
        }

        private void ProjectOperations_EndClean(object sender, CleanEventArgs args)
        {
            StopMonitor(ref _cleanCancellationTokenSource);
        }

        private void ProjectOperations_StartBuild(object sender, BuildEventArgs args)
        {
            StartMonitor(args.ProgressMonitor, ref _buildCancellationTokenSource);
        }

        private void ProjectOperations_EndBuild(object sender, BuildEventArgs args)
        {
            StopMonitor(ref _buildCancellationTokenSource);

            MessageService.ShowMessage("End build - handler ran");
        }

        private void StartMonitor(ProgressMonitor progressMonitor, ref CancellationTokenSource? cancellationTokenSource)
        {
            if (cancellationTokenSource is null)
            {
                cancellationTokenSource = new CancellationTokenSource();
                _ = ProgressMonitor(progressMonitor, cancellationTokenSource.Token);
            }
        }

        private static void StopMonitor(ref CancellationTokenSource? cancellationTokenSource)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = null;
        }

        private async Task ProgressMonitor(ProgressMonitor progressMonitor, CancellationToken cancellationToken)
        {
            //double progress = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                //if (progress != progressMonitor.Progress)
                //{

                //}
                System.Diagnostics.Debug.WriteLine($@"{progressMonitor.CurrentTaskName}: {progressMonitor.Progress} - {progressMonitor.CurrentTask?.TotalWork}");
                //if (progressMonitor.CurrentTask is not null)
                //{
                //    System.Diagnostics.Debug.WriteLine($@"\t{progressMonitor.CurrentTask.Name}: {progressMonitor.CurrentTask.Progress}");
                //}
                await Task.Delay(TimeSpan.FromSeconds(0.1));
            }
        }

    }
}
