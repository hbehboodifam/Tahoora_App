using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using OrderManagementApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ===== بارگذاری appsettings.json (پیش‌فرض) =====
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// ===== در حالت Production، فایل Production رو هم بارگذاری کن =====
if (builder.HostEnvironment.IsProduction())
{
    builder.Configuration.AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);
}

// ===== خواندن آدرس API از تنظیمات =====
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] 
                 ?? "http://localhost:5202/";

// ===== ثبت HttpClient =====
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

await builder.Build().RunAsync();