using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("FixedWindowPolicy", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(5);
        opt.PermitLimit = 5;
        opt.QueueLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddSlidingWindowLimiter("SlidingWindowPolicy", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(15);
        opt.SegmentsPerWindow = 3;
        opt.PermitLimit = 15;
    });

    options.AddTokenBucketLimiter("TokenBucketPolicy", opt =>
    {
        opt.TokenLimit = 4;
        opt.QueueLimit = 2;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        opt.TokensPerPeriod = 4;
        opt.AutoReplenishment = true;
    });
});

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
}).RequireRateLimiting("FixedWindowPolicy");

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

app.UseRateLimiter();

app.UseHttpsRedirection();

// app.UseAuthorization();

// app.MapControllers();

app.Run();
