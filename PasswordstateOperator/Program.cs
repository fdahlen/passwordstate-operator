using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Passwordstate;
using PasswordstateOperator.Rest;

namespace PasswordstateOperator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //TODO: auto restart deployments
            //TODO: test run in cluster
            //TODO: unit tests 

            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole().AddFilter(_ => true);
                })
                .ConfigureServices((hostBuilderContext, services) =>
                {
                    services.AddHostedService<Controller>();
                    services.AddTransient<OperationHandler>();
                    services.AddTransient<IKubernetesFactory, KubernetesFactory>();
                    services.AddTransient<PasswordstateSdk>();
                    services.AddTransient<IRestClientFactory, RestClientFactory>();
                    services.AddTransient<IKubernetesSdk, KubernetesSdk>();
                    services.AddTransient<Settings>();
                });
    }
}
