using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var redis = ConnectionMultiplexer.Connect("localhost:6379");

builder.Services.AddSingleton(redis);

var app = builder.Build();

app.MapPost("/webhook/{id}", async (string id, ConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    await db.ListLeftPushAsync(id, Guid.NewGuid().ToString());
});

app.MapGet("/sign/{id}", async (string id, ConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    var message = await db.ListLeftPopAsync(id);
    return message.ToString();
});

app.Run();