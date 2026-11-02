using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RouteOptimizer;
using RouteOptimizer.Application;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddScoped<IProblemWorkbook, RouteOptimizer.Infrastructure.ExcelProblemWorkbook>();
builder.Services.AddScoped<ILittleSolver, LittleSolver>();

await builder.Build().RunAsync();
