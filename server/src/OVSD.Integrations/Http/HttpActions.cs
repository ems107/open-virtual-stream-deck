using System.Net.Http.Headers;
using System.Text;
using OVSD.Core.Actions;

namespace OVSD.Integrations.Http;

/// <summary>Generic HTTP requests: webhooks, REST APIs, Home Assistant, etc.</summary>
public sealed class HttpActions : IActionProvider
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public IEnumerable<IActionHandler> GetActions() =>
    [
        new DelegateAction(new ActionDescriptor
        {
            Id = "http.request", Category = "http", Name = "HTTP request", Icon = "mdi:web-sync",
            Description = "The response body is stored in the variable; <variable>.status holds the status code.",
            Params =
            [
                new ParamDescriptor
                {
                    Name = "method", Label = "Method", Type = ParamType.Select, Default = "GET",
                    Options = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }.Select(m => new OptionItem(m, m)).ToList(),
                },
                new ParamDescriptor { Name = "url", Label = "URL", Required = true, Placeholder = "http://homeassistant.local:8123/api/services/light/toggle" },
                new ParamDescriptor { Name = "headers", Label = "Headers (one per line, Name: value)", Type = ParamType.MultilineText },
                new ParamDescriptor { Name = "body", Label = "Body", Type = ParamType.MultilineText, ShowIf = "method=POST,PUT,PATCH,DELETE" },
                new ParamDescriptor { Name = "contentType", Label = "Content type", Default = "application/json", ShowIf = "method=POST,PUT,PATCH,DELETE" },
                new ParamDescriptor { Name = "resultVariable", Label = "Store response in variable", Type = ParamType.Variable, Placeholder = "user.weather" },
                new ParamDescriptor { Name = "timeout", Label = "Timeout (s)", Type = ParamType.Number, Default = "10" },
            ],
        }, async (ctx, args, ct) =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(args.String("method") ?? "GET"), args.Required("url"));
            if (args.String("body") is { Length: > 0 } body)
                request.Content = new StringContent(body, Encoding.UTF8, args.String("contentType") ?? "application/json");

            foreach (var line in (args.String("headers") ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var (name, value) = (line[..colon].Trim(), line[(colon + 1)..].Trim());
                if (!request.Headers.TryAddWithoutValidation(name, value))
                    request.Content?.Headers.TryAddWithoutValidation(name, value);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(args.Int("timeout", 10), 1, 300)));
            HttpResponseMessage response;
            try
            {
                response = await Client.SendAsync(request, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new ActionException($"HTTP request timed out: {request.RequestUri}");
            }
            catch (HttpRequestException ex)
            {
                throw new ActionException($"HTTP request failed: {ex.Message}");
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                if (args.String("resultVariable") is { Length: > 0 } variable)
                {
                    ctx.SetVariable(variable, text);
                    ctx.SetVariable(variable + ".status", (int)response.StatusCode);
                }
                if (!response.IsSuccessStatusCode)
                    throw new ActionException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }),
    ];
}
