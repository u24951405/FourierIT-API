using FourierIT_API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FourierIT.API.Tests;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsConsistentSafeErrorShape()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-test-123"
        };
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("database password and internal SQL details"),
            CancellationToken.None);

        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        var body = await reader.ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("An unexpected error occurred", body);
        Assert.Contains("trace-test-123", body);
        Assert.DoesNotContain("database password", body);
        Assert.DoesNotContain("internal SQL details", body);
    }

    [Fact]
    public async Task TryHandleAsync_LogsExceptionAndTraceId()
    {
        var logger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(logger.Object);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-log-456" };
        context.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("diagnostic failure");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        logger.Verify(log => log.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("trace-log-456")),
            exception,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
