using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RouteOptimizer;
using RouteOptimizer.Application;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddScoped<IProblemWorkbook, RouteOptimizer.Infrastructure.ExcelProblemWorkbook>();
builder.Services.AddScoped<ILittleSolver, LittleSolver>();

await builder.Build().RunAsync();