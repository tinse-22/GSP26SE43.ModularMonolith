using ClassifiedAds.Infrastructure.Web.ExceptionHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Infrastructure;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_Should_MapUniqueDbUpdateException_ToConflict()
    {
        var handler = new GlobalExceptionHandler(
            new Mock<ILogger<GlobalExceptionHandler>>().Object,
            Options.Create(new GlobalExceptionHandlerOptions()));

        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var postgresException = new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            "23505");
        var exception = new DbUpdateException("write failed", postgresException);
        using var activity = new Activity("unit-test");
        activity.Start();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        var payload = await reader.ReadToEndAsync();

        payload.Should().Contain("UNIQUE_CONSTRAINT_VIOLATION");
        payload.Should().Contain("Conflict");
    }
}
