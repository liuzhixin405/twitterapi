using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using twitterapi;

var builder = WebApplication.CreateBuilder(args);

// 添加配置
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

builder.Services.AddSingleton<TwitterHelper>();
// 添加Swagger服务
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Twitter API", Version = "v1" });
});

var app = builder.Build();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Twitter API v1"));
}

app.UseHttpsRedirection();

// 添加发推特的端点
app.MapPost("/tweet", async ([FromBody] TweetRequest request, [FromServices] TwitterHelper twitterCrawler) =>
{
    try
    {
        var result = await twitterCrawler.PostTweetAsync(request.Text);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"发送推文失败: {ex.Message}");
    }
})
.WithName("PostTweet")
.WithOpenApi();

// 添加 Twitter 认证端点
app.MapGet("/twitter-auth", async ([FromServices] TwitterHelper crawl) =>
{
    try
    {
        var result = await crawl.GetAuthorizationUrlAsync();
        return Results.Ok(new { Message = "请访问返回的 URL 获取 PIN 码", Url = result });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"获取认证 URL 失败: {ex.Message}");
    }
})
.WithName("GetTwitterAuthUrl")
.WithOpenApi();

// 添加输入 PIN 的端点
app.MapPost("/twitter-auth/pin", async ([FromBody] PinRequest request, [FromServices] TwitterHelper crawl) =>
{
    try
    {
        var result = await crawl.InputPinAndUpdateTokensAsync(request.Pin);
        return Results.Ok(new { Message = result });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"更新令牌失败: {ex.Message}");
    }
})
.WithName("SubmitTwitterPin")
.WithOpenApi();


app.Run();

// 定义请求模型
public class TweetRequest
{
    public string Text { get; set; }
}

public class PinRequest
{
    public string Pin { get; set; }
}

