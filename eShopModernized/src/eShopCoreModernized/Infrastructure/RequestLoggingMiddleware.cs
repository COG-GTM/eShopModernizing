using System.Diagnostics;

namespace eShopCoreModernized.Infrastructure
{
    public sealed partial class RequestLoggingMiddleware
    {
        public const string CorrelationIdHeader = "X-Correlation-ID";
        public const string AppVersionHeader = "X-App-Version";
        public const string DeploymentTrackHeader = "X-Deployment-Track";
        public const string CorrelationIdItemKey = "CorrelationId";
        private const int MaxCorrelationIdLength = 128;

        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private readonly DeploymentInfo _deployment;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, DeploymentInfo deployment)
        {
            _next = next;
            _logger = logger;
            _deployment = deployment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = ResolveCorrelationId(context);
            context.Items[CorrelationIdItemKey] = correlationId;
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers[CorrelationIdHeader] = correlationId;
                headers[AppVersionHeader] = _deployment.Version;
                headers[DeploymentTrackHeader] = _deployment.Track;
                return Task.CompletedTask;
            });

            using var scope = _logger.BeginScope(LogScopeState.ForDeployment(_deployment).Add("CorrelationId", correlationId));

            var start = Stopwatch.GetTimestamp();
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                LogCompleted(context, StatusCodes.Status500InternalServerError, start, ex);
                throw;
            }

            LogCompleted(context, context.Response.StatusCode, start, null);
        }

        public static string? GetCorrelationId(HttpContext context) =>
            context.Items.TryGetValue(CorrelationIdItemKey, out var value) ? value as string : null;

        private void LogCompleted(HttpContext context, int statusCode, long start, Exception? exception)
        {
            var level = statusCode >= StatusCodes.Status500InternalServerError
                ? LogLevel.Error
                : IsHealthProbe(context.Request.Path) ? LogLevel.Debug : LogLevel.Information;

            if (!_logger.IsEnabled(level))
            {
                return;
            }

            LogRequest(
                _logger,
                level,
                exception,
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                statusCode,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                context.GetEndpoint()?.DisplayName ?? "(none)");
        }

        private static bool IsHealthProbe(PathString path) =>
            path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);

        private static string ResolveCorrelationId(HttpContext context)
        {
            var incoming = context.Request.Headers[CorrelationIdHeader].ToString().Trim();
            if (IsValidCorrelationId(incoming))
            {
                return incoming;
            }

            return Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
        }

        private static bool IsValidCorrelationId(string value) =>
            value.Length is > 0 and <= MaxCorrelationIdLength
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

        [LoggerMessage(
            EventId = 1000,
            EventName = "RequestCompleted",
            Message = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMs:0.0} ms (endpoint: {Endpoint})")]
        private static partial void LogRequest(
            ILogger logger,
            LogLevel level,
            Exception? exception,
            string requestMethod,
            string requestPath,
            int statusCode,
            double elapsedMs,
            string endpoint);
    }
}
