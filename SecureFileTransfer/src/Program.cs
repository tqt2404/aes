using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SecureFileTransfer.UI;
using SecureFileTransfer.Services;
using SecureFileTransfer.Security;
using SecureFileTransfer.Network;
using SecureFileTransfer.Models;
using Microsoft.Extensions.Options;

namespace SecureFileTransfer;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();

            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) => {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) => {
                    services.Configure<AppConfig>(context.Configuration.GetSection("AppConfig"));
                    
                    services.AddSingleton<IAesCryptography, AesCryptographyService>();
                    services.AddSingleton<HubTcpClient>();
                    services.AddSingleton<CentralHubServer>();
                    services.AddSingleton<DatabaseService>();
                    services.AddSingleton<FileTransferManager>();
                    services.AddSingleton<MainForm>(sp => new MainForm(
                        sp.GetRequiredService<FileTransferManager>(),
                        sp.GetRequiredService<HubTcpClient>(),
                        sp.GetRequiredService<CentralHubServer>(),
                        sp.GetRequiredService<IOptions<AppConfig>>()
                    ));
                })
                .Build();

            var mainForm = host.Services.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }
        catch (Exception ex)
        {
            File.WriteAllText("crashlog.txt", ex.ToString());
            throw;
        }
    }
}
