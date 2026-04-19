using PeakMetrics.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IFreeApiDataService, FreeApiDataService>(client =>
{
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();
