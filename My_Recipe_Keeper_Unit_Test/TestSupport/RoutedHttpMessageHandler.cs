using System.Net;
using System.Net.Http.Headers;

namespace My_Recipe_Keeper.Tests.TestSupport
{
    /// <summary>Test double for HttpClient: matches requests by (method, url-prefix) and returns a canned response.</summary>
    public sealed class RoutedHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<(HttpMethod Method, string UrlPrefix, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = new();

        public List<HttpRequestMessage> Requests { get; } = new();

        public RoutedHttpMessageHandler When(HttpMethod method, string urlPrefix, Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _routes.Add((method, urlPrefix, respond));
            return this;
        }

        // Buffers the body/status up front and builds a fresh HttpResponseMessage per call —
        // a route hit more than once (e.g. the same "list files" call during backup and cleanup)
        // would otherwise hand back an already-disposed HttpContent on the second send.
        public RoutedHttpMessageHandler When(HttpMethod method, string urlPrefix, HttpResponseMessage template)
        {
            var status = template.StatusCode;
            var body = template.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            var contentType = template.Content?.Headers.ContentType?.ToString();
            template.Dispose();

            return When(method, urlPrefix, _ =>
            {
                var response = new HttpResponseMessage(status);
                if (body is not null)
                {
                    response.Content = new StringContent(body);
                    if (contentType is not null)
                    {
                        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                    }
                }

                return response;
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            var url = request.RequestUri!.ToString();
            var match = _routes.FirstOrDefault(r => r.Method == request.Method && url.Contains(r.UrlPrefix, StringComparison.Ordinal));

            if (match.Respond is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent($"No route configured for {request.Method} {url}")
                });
            }

            return Task.FromResult(match.Respond(request));
        }
    }
}
