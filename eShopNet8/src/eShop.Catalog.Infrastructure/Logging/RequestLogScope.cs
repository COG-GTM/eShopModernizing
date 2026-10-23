using System.Collections;

namespace eShop.Catalog.Infrastructure.Logging;

// Structured scope state: providers that understand key/value scopes get "activityid" and "requestinfo" as properties,
// text providers (console) print "activityid - requestinfo" like the legacy log4net pattern.
public sealed class RequestLogScope : IReadOnlyList<KeyValuePair<string, object?>>
{
    public const string ActivityIdKey = "activityid";
    public const string RequestInfoKey = "requestinfo";

    public RequestLogScope(string activityId, string requestInfo)
    {
        ActivityId = activityId;
        RequestInfo = requestInfo;
    }

    public string ActivityId { get; }
    public string RequestInfo { get; }

    public int Count => 2;

    public KeyValuePair<string, object?> this[int index] => index switch
    {
        0 => new KeyValuePair<string, object?>(ActivityIdKey, ActivityId),
        1 => new KeyValuePair<string, object?>(RequestInfoKey, RequestInfo),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => ActivityId + " - " + RequestInfo;
}
