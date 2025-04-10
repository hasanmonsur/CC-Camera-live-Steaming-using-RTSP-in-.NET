using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using UniviewNvrApi.Models;
using UniviewNvrApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(); // For MVC

builder.Services.AddControllers();
builder.Services.Configure<RtspSettings>(builder.Configuration.GetSection("RtspSettings"));
// Register RtspStreamingService as a singleton for injection
builder.Services.AddSingleton<RtspStreamingService>();
// Optionally, register it as a hosted service if you want it to start automatically
builder.Services.AddHostedService(provider => provider.GetRequiredService<RtspStreamingService>());

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});


// Configure static files with custom MIME types
var staticFileOptions = new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    RequestPath = "",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache"); // Optional: for live streaming
    }
};



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// Add MIME type for .m3u8
var provider = new FileExtensionContentTypeProvider();
if (!provider.Mappings.ContainsKey(".m3u8"))
{
    provider.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";
}
staticFileOptions.ContentTypeProvider = provider;


app.UseCors("AllowAll"); // Apply CORS policy
// Configure middleware pipeline
//app.UseHttpsRedirection();
app.UseStaticFiles(staticFileOptions); // Serve files from wwwroot
app.UseRouting();
app.UseAuthorization();

// Map MVC and API routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers(); // For API endpoints

app.Run();