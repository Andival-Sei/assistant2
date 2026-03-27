using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Assistant.WinUI.Auth
{
    internal sealed class LoopbackOAuthListener : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly string _prefix;

        public LoopbackOAuthListener(string redirectUri)
        {
            var normalized = redirectUri.EndsWith("/", StringComparison.Ordinal)
                ? redirectUri
                : $"{redirectUri}/";
            _prefix = normalized;
            _listener.Prefixes.Add(_prefix);
        }

        public async Task<Uri> WaitForCallbackAsync(TimeSpan timeout)
        {
            _listener.Start();
            try
            {
                var contextTask = _listener.GetContextAsync();
                var completedTask = await Task.WhenAny(contextTask, Task.Delay(timeout));
                if (completedTask != contextTask)
                {
                    throw new TimeoutException("OAuth callback timeout.");
                }

                var context = await contextTask;
                var callbackUri = context.Request.Url
                    ?? throw new InvalidOperationException("OAuth callback URL is missing.");

                await WriteResponseAsync(context.Response);
                return callbackUri;
            }
            finally
            {
                _listener.Stop();
                _listener.Close();
            }
        }

        private static async Task WriteResponseAsync(HttpListenerResponse response)
        {
            const string html = """
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>Assistant</title>
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <style>
      body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:#111315;color:#f5f7fb;display:grid;place-items:center;min-height:100vh}
      .card{max-width:420px;padding:28px 24px;border-radius:22px;background:#1a1d21;border:1px solid rgba(255,255,255,.08);text-align:center}
      h1{margin:0 0 8px;font-size:24px}
      p{margin:0;color:#b2b8c4;line-height:1.5}
    </style>
  </head>
  <body>
    <div class="card">
      <h1>Assistant</h1>
      <p>Sign-in completed. You can return to the app.</p>
    </div>
  </body>
</html>
""";

            var bytes = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        public void Dispose()
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Close();
        }
    }
}
