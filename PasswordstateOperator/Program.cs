using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Passwordstate;

namespace PasswordstateOperator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //TODO: test run in cluster
            //TODO: unit tests 
            //TODO: refactor operation handler into smaller parts 

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
                    services.AddTransient<IKubernetesFactory, KubernetesFactory>();
                    services.AddTransient<PasswordstateSdk>();
                });
    }
}
