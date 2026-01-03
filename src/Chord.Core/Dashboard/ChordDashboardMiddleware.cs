using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chord.Core.Stores;
using Microsoft.AspNetCore.Http;

namespace Chord.Core.Dashboard;

internal sealed class ChordDashboardMiddleware
{
    private readonly RequestDelegate _next;

    public ChordDashboardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider services)
    {
        if (context.Request.Path.Equals("/chord/dashboard", StringComparison.OrdinalIgnoreCase))
        {
            var provider = services.GetService(typeof(IChordStoreSnapshotProvider)) as IChordStoreSnapshotProvider;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(BuildHtml(provider)).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static string BuildHtml(IChordStoreSnapshotProvider? provider)
    {
        var builder = new StringBuilder();
        builder.Append("""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Chord Dashboard</title>
  <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">
</head>
<body class="bg-light">
  <div class="container py-4">
    <h1 class="mb-4">Chord Dashboard</h1>
""");

        if (provider is null)
        {
            builder.Append("""
    <div class="alert alert-warning">The configured store does not expose dashboard snapshots.</div>
""");
        }
        else
        {
            var snapshot = provider.GetSnapshot();
            if (snapshot.Count == 0)
            {
                builder.Append("""
    <div class="alert alert-info">No dispatch records yet.</div>
""");
            }
            else
            {
                foreach (var (correlationId, records) in snapshot.OrderBy(kvp => kvp.Key))
                {
                    builder.Append($"""
    <div class="card mb-4 shadow-sm">
      <div class="card-header">
        <strong>Correlation:</strong> {correlationId}
      </div>
      <div class="card-body p-0">
        <div class="table-responsive">
          <table class="table table-striped mb-0">
            <thead class="table-light">
              <tr>
                <th scope="col">Step</th>
                <th scope="col">Queue</th>
                <th scope="col">Status</th>
                <th scope="col">Started</th>
                <th scope="col">Completed</th>
                <th scope="col">Duration</th>
              </tr>
            </thead>
            <tbody>
""");

                    foreach (var record in records)
                    {
                        builder.Append($"""
              <tr>
                <td>{record.StepId}</td>
                <td>{record.QueueName}</td>
                <td>{record.Status}</td>
                <td>{record.StartedAt:O}</td>
                <td>{(record.CompletedAt?.ToString("O") ?? "-")}</td>
                <td>{(record.Duration == TimeSpan.Zero ? "-" : record.Duration.ToString())}</td>
              </tr>
""");
                    }

                    builder.Append("""
            </tbody>
          </table>
        </div>
      </div>
    </div>
""");
                }
            }
        }

        builder.Append("""
  </div>
</body>
</html>
""");

        return builder.ToString();
    }
}
