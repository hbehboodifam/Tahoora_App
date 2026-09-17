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

// ===== آدرس API: نسبی به آدرس فعلی مرورگر =====
// وقتی از localhost باز می‌شه:  http://localhost:5255/  → API: http://localhost:5202/
// وقتی از Tailscale باز می‌شه:  https://xxx.ts.net/    → API: https://xxx.ts.net/api/ (از طریق Tailscale Serve)
//
// بنابراین کد باید تشخیص بده کجا اجرا می‌شه
string apiBaseUrl;

var currentBase = builder.HostEnvironment.BaseAddress; // مثل http://localhost:5255/ یا https://xxx.ts.net/
var uri = new Uri(currentBase);

if (uri.Host == "localhost" || uri.Host == "127.0.0.1" || IsLocalIp(uri.Host))
{
    // ===== حالت Local: API روی پورت جداگانه 5202 =====
    apiBaseUrl = $"{uri.Scheme}://{uri.Host}:5202/";
}
else
{
    // ===== حالت Tailscale Funnel: API از مسیر /api روی همون دامنه =====
    apiBaseUrl = $"{uri.Scheme}://{uri.Host}/api/";
}

// ===== ثبت HttpClient =====
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

await builder.Build().RunAsync();

// ===== تشخیص IP محلی شبکه (192.168.x.x, 10.x.x.x, 172.16-31.x.x) =====
static bool IsLocalIp(string host)
{
    if (!System.Net.IPAddress.TryParse(host, out var ip)) return false;
    var bytes = ip.GetAddressBytes();
    if (bytes.Length != 4) return false;

    // 10.0.0.0/8
    if (bytes[0] == 10) return true;
    // 172.16.0.0/12
    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
    // 192.168.0.0/16
    if (bytes[0] == 192 && bytes[1] == 168) return true;

    return false;
}