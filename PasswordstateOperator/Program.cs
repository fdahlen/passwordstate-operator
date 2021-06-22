using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PasswordstateOperator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //TODO: test run in cluster
            //TODO: crd field to trigger refresh
            //TODO: crd field for enabling polling 

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
