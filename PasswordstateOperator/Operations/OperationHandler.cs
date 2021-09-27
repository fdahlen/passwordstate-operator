using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasswordstateOperator.Cache;

namespace PasswordstateOperator.Operations
{
    public class OperationHandler
    {
        private readonly ILogger<OperationHandler> logger;
        private readonly CacheManager cacheManager = new();
        private readonly Settings settings;
        private readonly IGetOperation getOperation;
        private readonly ICreateOperation createOperation;
        private readonly IUpdateOperation updateOperation;
        private readonly IDeleteOperation deleteOperation;
        private readonly ISyncOperation syncOperation;

        private DateTimeOffset previousSyncTime = DateTimeOffset.MinValue;

        public OperationHandler(
            ILogger<OperationHandler> logger,
            IOptions<Settings> passwordstateSettings,
            IGetOperation getOperation,
            ICreateOperation createOperation,
            IUpdateOperation updateOperation,
            IDeleteOperation deleteOperation,
            ISyncOperation syncOperation)
        {
            this.logger = logger;
            this.getOperation = getOperation;
            this.createOperation = createOperation;
            this.updateOperation = updateOperation;
            this.deleteOperation = deleteOperation;
            this.syncOperation = syncOperation;
            this.settings = passwordstateSettings.Value;
        }

        public async Task OnAdded(PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnAdded)}: {crd.Id}");

            cacheManager.AddOrUpdate(crd.Id, crd);

            await createOperation.Create(crd);
        }

        public async Task OnUpdated(PasswordListCrd newCrd)
        {
            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.Id}");

            var existingCrd = cacheManager.Get(newCrd.Id);
            cacheManager.AddOrUpdate(newCrd.Id, newCrd);

            await updateOperation.Update(existingCrd, newCrd);
        }

        public async Task OnDeleted(PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnDeleted)}: {crd.Id}");

            cacheManager.Delete(crd.Id);

            await deleteOperation.Delete(crd);
        }

        public Task OnBookmarked(PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnBookmarked)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public Task OnError(PasswordListCrd crd)
        {
            logger.LogError($"{nameof(OnError)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public async Task CheckCurrentState()
        {
            logger.LogDebug(nameof(CheckCurrentState));

            var sync = DateTimeOffset.UtcNow >= previousSyncTime.AddSeconds(settings.SyncIntervalSeconds);
            if (sync)
            {
                logger.LogDebug($"{nameof(CheckCurrentState)}: {settings.SyncIntervalSeconds}s has passed, will sync with Passwordstate");
            }

            foreach (var crd in cacheManager.List())
            {
                try
                {
                    await CheckCurrentStateForCrd(crd, sync);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"{nameof(CheckCurrentState)}: {crd.Id}: Failure");
                }
            }

            if (sync)
            {
                previousSyncTime = DateTimeOffset.UtcNow;
            }
        }

        private async Task CheckCurrentStateForCrd(PasswordListCrd crd, bool sync)
        {
            var passwordsSecret = await getOperation.Get(crd);
            if (passwordsSecret == null)
            {
                logger.LogInformation($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: password secret does not exist, will create");

                await createOperation.Create(crd);
            }
            else
            {
                logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: password secret exists");

                if (sync)
                {
                    await syncOperation.Sync(crd, passwordsSecret);
                }
            }
        }
    }
}