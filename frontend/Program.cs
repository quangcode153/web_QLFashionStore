using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Frontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var backendUrl = builder.Configuration["BackendUrl"] ?? "http://localhost:5000/";
builder.Services.AddTransient<Frontend.Services.AuthHeaderHandler>();
builder.Services.AddScoped(sp => 
{
    var handler = sp.GetRequiredService<Frontend.Services.AuthHeaderHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(backendUrl) };
});
builder.Services.AddScoped<Frontend.Services.IToastService, Frontend.Services.ToastService>();
builder.Services.AddScoped<Frontend.Services.IAuthStateService, Frontend.Services.AuthStateService>();
builder.Services.AddScoped<Frontend.Services.ICatalogApiClient, Frontend.Services.CatalogApiClient>();
builder.Services.AddScoped<Frontend.Services.IPolicyApiClient, Frontend.Services.PolicyApiClient>();
builder.Services.AddScoped<Frontend.Services.ICartService, Frontend.Services.CartService>();
builder.Services.AddScoped<Frontend.Services.IOrderApiClient, Frontend.Services.OrderApiClient>();
builder.Services.AddScoped<Frontend.Services.ICustomerApiClient, Frontend.Services.CustomerApiClient>();
builder.Services.AddScoped<Frontend.Services.IPosApiClient, Frontend.Services.PosApiClient>();
builder.Services.AddScoped<Frontend.Services.IAdminApiClient>(sp => 
    new Frontend.Services.AdminApiClient(
        sp.GetRequiredService<HttpClient>(),
        sp.GetService<Microsoft.JSInterop.IJSRuntime>()));
builder.Services.AddSingleton<Frontend.Services.ISignalRNotificationService>(sp => 
    new Frontend.Services.SignalRNotificationService(
        backendUrl, 
        sp.GetService<Microsoft.JSInterop.IJSRuntime>()));

await builder.Build().RunAsync();
