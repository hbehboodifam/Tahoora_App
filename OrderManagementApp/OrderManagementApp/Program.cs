using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OrderManagementApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// تنظیم HttpClient برای ارتباط با API (آدرس API محلی)
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri("https://order-management-api-f3sc.onrender.com") // ← اصلاح شد
});

await builder.Build().RunAsync();