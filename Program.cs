using Microsoft.EntityFrameworkCore;
using urlshort;
using urlshort.Entities;
using urlshort.Extensions;
using urlshort.Models;
using urlshort.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<UrlShorteningService>();

builder.Services.AddScoped<CacheService>();

var connStr = builder.Configuration.GetConnectionString(name: "DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connStr));

builder.Services.AddStackExchangeRedisCache(redisOptions =>
{
    string connection = builder.Configuration
        .GetConnectionString("Redis");

    redisOptions.Configuration = connection;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // not sure about this and whether applicable with the ef migrations
    app.ApplyMigrations();
}

app.MapPost("api/shorten", async (
    ShortenUrlRequest request,
    UrlShorteningService urlShorteningService,
    ApplicationDbContext dbContext,
    HttpContext httpContext,
    CacheService cacheService) =>
{
    //var cacheData = httpContext.RequestServices.GetRequiredService<CacheService>();
    var cacheData = cacheService.GetData<ShortenedUrl>(request.Url);

    // here cacheData should be the ShortUrl
    if (cacheData != null) return Results.Ok(cacheData.ShortUrl);

    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        return Results.BadRequest("The specified URL is invalid.");
    
    var code = await urlShorteningService.GenerateUniqueCode(dbContext);

    var shortenedUrl = new ShortenedUrl
    {
        Id = Guid.NewGuid(),
        LongUrl = request.Url,
        Code = code,
        ShortUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/{code}",
        CreatedOnUtc = DateTime.Now
    };

    dbContext.ShortenedUrls.Add(shortenedUrl);

    await dbContext.SaveChangesAsync();

    // added 99 years as currently just a complication
    cacheService.SetData<ShortenedUrl>(request.Url, shortenedUrl, DateTime.Now.AddYears(99));

    return Results.Ok(shortenedUrl.ShortUrl);
});

app.MapGet("api/{code}", async (string code, ApplicationDbContext dbContext) =>
{
    var shortenedUrl = await dbContext.ShortenedUrls
    .FirstOrDefaultAsync(s => s.Code == code);

    if (shortenedUrl == null)
    {
        return Results.NotFound(code);
    }

    return Results.Redirect(shortenedUrl.LongUrl);
});

app.UseHttpsRedirection();

// app.UseAuthorization();

// app.MapControllers();

app.Run();
