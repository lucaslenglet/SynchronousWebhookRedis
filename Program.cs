using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var redis = ConnectionMultiplexer.Connect("localhost:6379");

builder.Services.AddSingleton(redis);

var app = builder.Build();

app.MapPost("/webhook/{agreementId}", async (string agreementId, ConnectionMultiplexer redis, ILogger<Program> logger) =>
{
    // Save to real db here
    var signatureId = Guid.NewGuid().ToString();
    logger.LogInformation("Signature received for agreement '{0}': {1}", agreementId, signatureId);

    // Push into queue
    var db = redis.GetDatabase();
    await db.ListLeftPushExpireAsync(agreementId, signatureId, TimeSpan.FromMinutes(2));
});

app.MapGet("/sign/{agreementId}", async (string agreementId, ConnectionMultiplexer redis, ILogger<Program> logger) =>
{
    var db = redis.GetDatabase();
    var value = await db.BlockingLeftPopAsync(agreementId, timeoutMilliseconds: 1000);
    if (value.HasValue)
    {
        var signatureId = value.ToString();
        logger.LogInformation("Signature retrived by user: {0}", signatureId);
        return new SignResponse("success", signatureId);
    }

    logger.LogWarning("Could't not retrieve signature for agreement '{0}'", agreementId);
    return new SignResponse("fail", null, "Could't not retrieve signature for this agreement.");
});

app.Run();

public record SignResponse(string Status, string? SignatureId, string? Message = null);

public static class DatabaseExtensions
{
    public static Task<bool> ListLeftPushExpireAsync(this IDatabase db, RedisKey key, RedisValue value, TimeSpan ttl)
    {
        var trans = db.CreateTransaction();

        trans.ListLeftPushAsync(key, value);
        trans.KeyExpireAsync(key, ttl);

        var transactionResult = trans.Execute();
        return Task.FromResult(transactionResult);
    }

    public static async Task<RedisValue> BlockingLeftPopAsync(this IDatabase db, RedisKey key, int retryIntervalMilliseconds = 100, int timeoutMilliseconds = 60_000)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(timeoutMilliseconds);

        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            var message = await db.ListLeftPopAsync(key);
            
            if (message.HasValue)
            {
                return message;
            }

            await Task.Delay(retryIntervalMilliseconds);
        }

        return RedisValue.Null;
    }
}