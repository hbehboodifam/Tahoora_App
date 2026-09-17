using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using OrderManagementApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ===== بارگذاری appsettings.json =====
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

if (builder.HostEnvironment.IsProduction())
{
    builder.Configuration.AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);
}

// ===== خواندن آدرس API از تنظیمات (اختیاری) =====
var configuredUrl = builder.Configuration["ApiSettings:BaseUrl"];

// ===== اگه تنظیمات آدرس داره، همون رو استفاده کن =====
// اگه نداره (یا localhost بود)، خودکار از روی URL فعلی بساز
string apiBaseUrl;

if (builder.HostEnvironment.IsProduction() && !string.IsNullOrEmpty(configuredUrl))
{
    // Production: از تنظیمات
    apiBaseUrl = configuredUrl;
}
else
{
    // Development: از روی hostname فعلی، پورت API رو بساز
    var currentBase = builder.HostEnvironment.BaseAddress;   // مثل http://localhost:5255/
    var uri = new Uri(currentBase);
    var apiHost = uri.Host;                                   // localhost یا 192.168.1.10 یا هرچی
    var apiScheme = uri.Scheme;                               // http یا https
    apiBaseUrl = $"{apiScheme}://{apiHost}:5202/";
}

// ===== ثبت HttpClient =====
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

await builder.Build().RunAsync();