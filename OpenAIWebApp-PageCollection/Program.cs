var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Use a Distributed Memory Cache to illustrate the principle.
// In a production system, we'd want to use a different distributed caching mechanism
// See: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0#distributed-memory-cache
builder.Services.AddDistributedMemoryCache();

// TODO: Something like https://learn.microsoft.com/en-us/dotnet/api/overview/azure/microsoft.extensions.azure-readme?view=azure-dotnet?
// builder.Services.Add(new OpenAIServiceClient());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
