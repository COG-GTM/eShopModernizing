using System.Diagnostics;
using eShop.Catalog.WebForms.Hosting.Observability;
using Microsoft.AspNetCore.Http;

namespace eShop.Catalog.WebForms.Tests.S9_Ops;

public class RequestLogScopeTests
{
    private const string ParentTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    [Fact]
    public void Scope_carries_w3c_trace_id_span_id_and_request_info()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/Catalog/Edit/3";
        using var activity = new Activity("request").SetParentId($"00-{ParentTraceId}-00f067aa0ba902b7-01");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        var scope = RequestLogScope.Create(context, activity).ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal(ParentTraceId, scope[RequestLogScope.TraceId]);
        Assert.Equal(activity.SpanId.ToHexString(), scope[RequestLogScope.SpanId]);
        Assert.Equal("POST", scope[RequestLogScope.RequestMethod]);
        Assert.Equal("/Catalog/Edit/3", scope[RequestLogScope.RequestPath]);
    }

    [Fact]
    public void Scope_renders_readable_text_for_plain_text_sinks()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/Catalog";
        using var activity = new Activity("request").SetParentId($"00-{ParentTraceId}-00f067aa0ba902b7-01").Start();

        var text = RequestLogScope.Create(context, activity).ToString();

        Assert.StartsWith($"TraceId:{ParentTraceId}, SpanId:", text);
        Assert.EndsWith("RequestMethod:GET, RequestPath:/Catalog", text);
    }

    [Fact]
    public void Without_an_activity_the_trace_id_falls_back_to_the_request_identifier()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "0HMVFE0A0K2Q1:00000001" };
        context.Request.Method = "GET";
        context.Request.Path = "/";

        var scope = RequestLogScope.Create(context, activity: null).ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal("0HMVFE0A0K2Q1:00000001", scope[RequestLogScope.TraceId]);
        Assert.Null(scope[RequestLogScope.SpanId]);
        Assert.Equal("GET", scope[RequestLogScope.RequestMethod]);
        Assert.Equal("/", scope[RequestLogScope.RequestPath]);
    }
}
