using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using PasswordstateOperator.Kubernetes;

namespace PasswordstateOperator
{
  public class Controller : BackgroundService
  {
    private const int ReconciliationCheckIntervalSeconds = 10; 

    private readonly OperationHandler handler;
    private Watcher<PasswordListCrd> watcher;
    private readonly string k8sNamespace;

    private readonly IKubernetes kubernetes;
    
    private readonly ILogger<Controller> logger;

    public Controller(IKubernetesFactory kubernetesFactory, OperationHandler handler, ILogger<Controller> logger)
    {
      this.handler = handler;
      this.logger = logger;
      k8sNamespace = "";
      kubernetes = kubernetesFactory.Create();
    }

    ~Controller() => DisposeWatcher();

    private async Task<bool> IsCRDAvailable()
    {
      try
      {
        await kubernetes.ListNamespacedCustomObjectWithHttpMessagesAsync(
          PasswordListCrd.ApiGroup, 
          PasswordListCrd.ApiVersion, 
          k8sNamespace, 
          PasswordListCrd.Plural);
      }
      catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
      {
        logger.LogWarning($"{nameof(Controller)}: {nameof(IsCRDAvailable)}: No CustomResourceDefinition found for '" + PasswordListCrd.Plural + "', group '" + PasswordListCrd.ApiGroup + "' and version '" + PasswordListCrd.ApiVersion + "' on namespace '" + k8sNamespace + "'");
        return false;
      }
      
      return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      logger.LogInformation($"=== {nameof(Controller)} STARTING ===");

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
        logger.LogWarning($"=== {nameof(Program)} TERMINATING ===");
      }
    }

    private void StartWatcher()
    {
      DisposeWatcher();

      watcher = kubernetes.ListNamespacedCustomObjectWithHttpMessagesAsync(
          PasswordListCrd.ApiGroup,
          PasswordListCrd.ApiVersion,
          k8sNamespace,
          PasswordListCrd.Plural,
          watch: true)
        .Watch(
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
        await handler.CheckCurrentState(kubernetes);
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
      logger.LogWarning($"{nameof(Controller)}: {nameof(OnChange)}: {nameof(PasswordListCrd)} '{crd.Id}' event {(object) type} in namespace {crd.Namespace()}");
      
      try
      {
        switch (type)
        {
          case WatchEventType.Added:
            await handler.OnAdded(kubernetes, crd);
            break;
          case WatchEventType.Modified:
            await handler.OnUpdated(kubernetes, crd);
            break;
          case WatchEventType.Deleted:
            await handler.OnDeleted(kubernetes, crd);
            break;
          case WatchEventType.Error:
            await handler.OnError(kubernetes, crd);
            break;
          case WatchEventType.Bookmark:
            await handler.OnBookmarked(kubernetes, crd);
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
