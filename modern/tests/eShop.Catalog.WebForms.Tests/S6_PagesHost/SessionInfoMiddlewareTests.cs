using eShop.Catalog.WebForms.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace eShop.Catalog.WebForms.Tests.S6_PagesHost;

public class SessionInfoMiddlewareTests
{
    private static (DefaultHttpContext Context, RecordingSession Session) NewContext(RecordingSession? session = null, string path = "/About")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        session ??= new RecordingSession();
        context.Features.Set<ISessionFeature>(new SessionFeature { Session = session });
        return (context, session);
    }

    [Fact]
    public async Task First_request_sets_machine_name_and_en_US_start_time()
    {
        var clock = new ManualTimeProvider();
        var nextCalled = false;
        var middleware = new SessionInfoMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, clock);
        var (context, session) = NewContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(Environment.MachineName, session.GetString(SessionInfoMiddleware.MachineNameKey));
        Assert.Equal("3/5/2024 2:07:09 PM", session.GetString(SessionInfoMiddleware.SessionStartTimeKey));
        Assert.Equal(2, session.SetCalls);
        Assert.Equal($"{Environment.MachineName}, 3/5/2024 2:07:09 PM", SessionInfoMiddleware.FooterText(session));
    }

    [Fact]
    public async Task Later_requests_in_the_same_session_do_not_overwrite_the_keys()
    {
        var clock = new ManualTimeProvider();
        var middleware = new SessionInfoMiddleware(_ => Task.CompletedTask, clock);
        var (first, session) = NewContext();
        await middleware.InvokeAsync(first);

        for (var i = 0; i < 3; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(5));
            var (next, _) = NewContext(session);
            await middleware.InvokeAsync(next);
        }

        Assert.Equal(2, session.SetCalls);
        Assert.Equal("3/5/2024 2:07:09 PM", session.GetString(SessionInfoMiddleware.SessionStartTimeKey));
    }

    [Fact]
    public async Task A_new_session_gets_its_own_start_time()
    {
        var clock = new ManualTimeProvider();
        var middleware = new SessionInfoMiddleware(_ => Task.CompletedTask, clock);
        var (first, firstSession) = NewContext();
        await middleware.InvokeAsync(first);

        clock.Advance(TimeSpan.FromSeconds(1));
        var (second, secondSession) = NewContext();
        await middleware.InvokeAsync(second);

        Assert.Equal("3/5/2024 2:07:09 PM", firstSession.GetString(SessionInfoMiddleware.SessionStartTimeKey));
        Assert.Equal("3/5/2024 2:07:10 PM", secondSession.GetString(SessionInfoMiddleware.SessionStartTimeKey));
    }

    [Fact]
    public async Task Health_probe_does_not_start_a_session()
    {
        var middleware = new SessionInfoMiddleware(_ => Task.CompletedTask, new ManualTimeProvider());
        var (context, session) = NewContext(path: "/healthz");

        await middleware.InvokeAsync(context);

        Assert.Equal(0, session.SetCalls);
        Assert.Equal(0, session.LoadCalls);
    }
}
