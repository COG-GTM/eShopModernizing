using System;
using System.Net;

namespace eShopCatalogApi.Client
{
    /// <summary>
    /// Raised when the catalog API returns an unexpected HTTP status.
    /// Plays the role that <c>FaultException</c> played for the WCF client.
    /// </summary>
    public class CatalogServiceException : Exception
    {
        public CatalogServiceException(HttpStatusCode statusCode, string responseBody)
            : base($"Catalog API returned {(int)statusCode} ({statusCode}).")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public HttpStatusCode StatusCode { get; }

        public string ResponseBody { get; }
    }
}
