using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PasswordstateOperator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //TODO: extract current state to separate class
            //TODO: replace thread lock() with SemaphoreSlim (with narrower locking if possible) 
            //TODO: change to async/await
            //TODO: work on trigger for refresh: flag in crd? feature toggle for polling? 

            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                })
                .ConfigureServices((hostBuilderContext, services) =>
                {
                    services.AddHostedService<Controller>();
                    services.AddTransient<OperationHandler>();
                });
    }
}
