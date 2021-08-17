using System;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
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
    private readonly string k8sNamespace;

    private readonly IKubernetesSdk kubernetesSdk;
    
    private readonly ILogger<Controller> logger;

    public Controller(IKubernetesSdk kubernetesSdk, OperationHandler handler, ILogger<Controller> logger)
    {
      this.handler = handler;
      this.logger = logger;
      k8sNamespace = "";
      this.kubernetesSdk = kubernetesSdk;
    }

    ~Controller() => DisposeWatcher();

    private async Task<bool> IsCRDAvailable()
    {
      var available = await kubernetesSdk.CustomResourcesExistAsync(
        PasswordListCrd.ApiGroup,
        PasswordListCrd.ApiVersion,
        k8sNamespace,
        PasswordListCrd.Plural);
      
      if (!available)
      {
        //TODO: keep here? cleanup w string interpolation?
        logger.LogWarning($"{nameof(Controller)}: {nameof(IsCRDAvailable)}: No CustomResourceDefinition found for '" + PasswordListCrd.Plural + "', group '" +
                          PasswordListCrd.ApiGroup + "' and version '" + PasswordListCrd.ApiVersion + "' on namespace '" + k8sNamespace + "'");
      }

      return available;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      logger.LogInformation($"=== {nameof(Controller)} STARTING ===");

      //TODO: is this needed? can't we just watch without any CRDs existing to start with?
      try
      {
        while (!await IsCRDAvailable() && !stoppingToken.IsCancellationRequested)
        {
          await Task.Delay(ReconciliationCheckIntervalSeconds * 1000);
        }

        StartWatcher();

        logger.LogInformation($"=== {nameof(Controller)} STARTED ===");

        await ReconciliationLoop(stoppingToken);
      }
      catch (Exception ex)
      {
        logger.LogCritical(ex, ex.Message);
        throw;
      }
      finally
      {
        logger.LogInformation($"=== {nameof(Program)} TERMINATING ===");
      }
    }

    private void StartWatcher()
    {
      DisposeWatcher();

      watcher = kubernetesSdk.WatchCustomResources(
          PasswordListCrd.ApiGroup,
          PasswordListCrd.ApiVersion,
          k8sNamespace,
          PasswordListCrd.Plural,
          new Action<WatchEventType, PasswordListCrd>(OnChange),
          OnError,
          OnClose);
    }

    private async Task ReconciliationLoop(CancellationToken stoppingToken)
    {
      logger.LogInformation($"{nameof(Controller)}: {nameof(ReconciliationLoop)}: Reconciliation loop will run every {ReconciliationCheckIntervalSeconds} seconds");
      
      while (!stoppingToken.IsCancellationRequested)
      {
        await Task.Delay(ReconciliationCheckIntervalSeconds * 1000);
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
      logger.LogInformation($"{nameof(Controller)}: {nameof(OnChange)}: {nameof(PasswordListCrd)} '{crd.Id}' event {(object) type} in namespace {crd.Namespace()}");
      
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
            throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, $"{nameof(Controller)}: {nameof(OnChange)}: Exception for {nameof(PasswordListCrd)} '{crd.Name()}' event {(object) type} in namespace {crd.Namespace()}");
      }
    }

    private void OnError(Exception exception)
    {
      if (exception is TaskCanceledException canceledException)
      {
        if (canceledException.InnerException is TimeoutException)
        {
          logger.LogError(exception, $"{nameof(Controller)}: {nameof(OnError)}: TimeoutException");
          DisposeWatcher();
          return;
        }
      }

      logger.LogCritical(exception, $"{nameof(Controller)}: {nameof(OnError)}: Exception");
    }

    private void OnClose()
    {
      logger.LogCritical($"{nameof(Controller)}: {nameof(OnClose)}: Connection closed, restarting watcher");
      
      StartWatcher();
    }
  }
}
