using Microsoft.Extensions.Options;
using SecureFileTransfer.Models;
using SecureFileTransfer.Network;
using SecureFileTransfer.Security;
using SecureFileTransfer.Services;

var builder = WebApplication.CreateBuilder(args);

// Load configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Add services to the container
builder.Services.Configure<AppConfig>(builder.Configuration.GetSection("AppConfig"));
builder.Services.AddScoped<IAesCryptography, AesService>();
builder.Services.AddScoped<ITcpClient, TcpSender>();
builder.Services.AddScoped<ITcpServer, TcpReceiver>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<FileTransferManager>();

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Transfer}/{action=Index}/{id?}");

app.Run();
