using System;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PasswordstateOperator.Kubernetes;

namespace PasswordstateOperator
{
    public class Controller : BackgroundService
    {
        private const int ReconciliationCheckIntervalSeconds = 10;

        private readonly OperationHandler handler;
        private Watcher<PasswordListCrd> watcher;

        private readonly IKubernetesSdk kubernetesSdk;

        private readonly ILogger<Controller> logger;

        public Controller(IKubernetesSdk kubernetesSdk, OperationHandler handler, ILogger<Controller> logger)
        {
            this.handler = handler;
            this.logger = logger;
            this.kubernetesSdk = kubernetesSdk;
        }

        ~Controller() => DisposeWatcher();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation($"{nameof(ExecuteAsync)}: Starting");

            try
            {
                StartWatcher();

                logger.LogInformation($"{nameof(ExecuteAsync)}: Started");

                await ReconciliationLoop(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{nameof(ExecuteAsync)}: Failure");
                throw;
            }
            finally
            {
                logger.LogInformation($"{nameof(ExecuteAsync)}: Terminating");
            }
        }

        private void StartWatcher()
        {
            DisposeWatcher();

            watcher = kubernetesSdk.WatchCustomResources(
                PasswordListCrd.ApiGroup,
                PasswordListCrd.ApiVersion,
                PasswordListCrd.Plural,
                new Action<WatchEventType, PasswordListCrd>(OnChange),
                OnError,
                OnClose);
        }

        private async Task ReconciliationLoop(CancellationToken stoppingToken)
        {
            logger.LogInformation($"{nameof(ReconciliationLoop)}: Reconciliation loop will run every {ReconciliationCheckIntervalSeconds} seconds");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ReconciliationCheckIntervalSeconds * 1000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    logger.LogInformation($"{nameof(ReconciliationLoop)}: Cancellation requested for stopping token, reconciliation loop aborted");
                }

                await handler.CheckCurrentState();
            }
        }

        private void DisposeWatcher()
        {
            if (watcher == null || !watcher.Watching)
            {
                return;
            }

            watcher.Dispose();
        }

        private async void OnChange(WatchEventType type, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnChange)}: {type}: '{crd.Id}'");

            try
            {
                switch (type)
                {
                    case WatchEventType.Added:
                        await handler.OnAdded(crd);
                        break;
                    case WatchEventType.Modified:
                        await handler.OnUpdated(crd);
                        break;
                    case WatchEventType.Deleted:
                        await handler.OnDeleted(crd);
                        break;
                    case WatchEventType.Error:
                        await handler.OnError(crd);
                        break;
                    case WatchEventType.Bookmark:
                        await handler.OnBookmarked(crd);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported {nameof(WatchEventType)}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(OnChange)}: {type}: {crd.Id}: Failure");
            }
        }

        private void OnError(Exception exception)
        {
            if (exception is TaskCanceledException canceledException)
            {
                if (canceledException.InnerException is TimeoutException)
                {
                    logger.LogError(exception, $"{nameof(OnError)}: TimeoutException");
                    DisposeWatcher();
                    return;
                }
            }

            logger.LogCritical(exception, $"{nameof(OnError)}: Exception");
        }

        private void OnClose()
        {
            logger.LogCritical($"{nameof(OnClose)}: Connection closed, restarting watcher");

            StartWatcher();
        }
    }
}