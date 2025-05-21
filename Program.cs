using Google.Apis.Auth.OAuth2;
using Google.Apis.PhotosLibrary.v1;
using Google.Apis.PhotosLibrary.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Threading;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", async (HttpContext context) =>
{
    // Authenticate with Google Photos
    UserCredential credential;
    using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
    {
        string credPath = "token.json";
        credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
            new[] { PhotosLibraryService.Scope.PhotoslibraryReadonly },
            "user",
            CancellationToken.None,
            new FileDataStore(credPath, true));
    }

    var service = new PhotosLibraryService(new BaseClientService.Initializer()
    {
        HttpClientInitializer = credential,
        ApplicationName = "PhotoManagerWeb",
    });

    int page = 1;
    if (context.Request.Query.ContainsKey("page"))
        int.TryParse(context.Request.Query["page"], out page);
    if (page < 1) page = 1;

    // Default start date to 30 days ago, or use user input
    DateTime startDate = DateTime.UtcNow.AddDays(-30);
    if (context.Request.Query.ContainsKey("startDate"))
    {
        DateTime.TryParse(context.Request.Query["startDate"], out startDate);
    }

    // Remove endDate: only use startDate and go backward in time
    int groupCount = 2;
    if (context.Request.Query.ContainsKey("groupCount"))
        int.TryParse(context.Request.Query["groupCount"], out groupCount);
    if (groupCount < 2) groupCount = 2;

    var request = service.MediaItems.Search(new SearchMediaItemsRequest
    {
        PageSize = 100,
        PageToken = context.Request.Query["pageToken"],
        Filters = new Filters
        {
            DateFilter = new DateFilter
            {
                Ranges = new List<DateRange> {
                    new DateRange {
                        StartDate = new Date { Year = 2008, Month = startDate.Month, Day = startDate.Day },
                        EndDate = new Date { Year = startDate.Year, Month = startDate.Month, Day = startDate.Day }
                    }
                }
            }
        }
    });
    var response = await request.ExecuteAsync();
    var items = response.MediaItems;
    if (items == null || items.Count == 0)
    {
        // If there is a next page, jump to it automatically
        if (!string.IsNullOrEmpty(response.NextPageToken))
        {
            context.Response.Redirect($"/?pageToken={response.NextPageToken}&startDate={startDate:yyyy-MM-dd}");
            return;
        }
        await context.Response.WriteAsync($"<h2>No media items found before {startDate:yyyy-MM-dd}.</h2>" +
            $"<form method='get'><label>Start date: <input type='date' name='startDate' value='{startDate:yyyy-MM-dd}'></label> <button type='submit'>Go</button></form>");
        return;
    }
    // Find burst groups
    var grouped = items
        .Where(i => i.MediaMetadata != null && i.MediaMetadata.CreationTime != null)
        .GroupBy(i => {
            var creationTimeObj = i.MediaMetadata.CreationTime;
            string creationTimeStr = creationTimeObj != null ? creationTimeObj.ToString() ?? string.Empty : string.Empty;
            return creationTimeStr.Length >= 16 ? creationTimeStr.Substring(0, 16) : creationTimeStr;
        })
        .Where(g => g.Count() >= groupCount);

    // If no burst groups found but there is a next page, jump to it automatically
    if (!grouped.Any() && !string.IsNullOrEmpty(response.NextPageToken))
    {
        int nextPage = page + 1;
        context.Response.Redirect($"/?pageToken={response.NextPageToken}&startDate={startDate:yyyy-MM-dd}&page={nextPage}");
        return;
    }

    var sb = new StringBuilder();
    sb.AppendLine("<html><head><title>Burst Shots</title></head><body>");
    sb.AppendLine($"<h1>Burst Groups (before {startDate:yyyy-MM-dd})</h1>");
    // Top controls
    sb.AppendLine("<div style='margin-bottom:20px;'>");
    sb.AppendLine($"<form method='get' style='display:inline;'>"
        + "<label>Start date: <input type='date' name='startDate' value='" + startDate.ToString("yyyy-MM-dd") + "'></label> "
        + "<label style='margin-left:10px;'>Minimum photos in burst group: <input type='number' name='groupCount' min='2' value='" + groupCount + "' style='width:60px;'/></label> "
        + "<input type='number' name='page' min='1' value='" + page + "' style='width:60px;margin-left:10px;'/> "
        + "<button type='submit'>Go</button></form>");
    sb.AppendLine($"<span style='margin-left:20px;'>Page {page}</span>");
    // Previous button
    sb.AppendLine("<form method='get' style='display:inline;margin-left:10px;'>");
    if (page > 1)
    {
        sb.AppendLine($"<input type='hidden' name='page' value='{page - 1}' />");
        sb.AppendLine($"<input type='hidden' name='startDate' value='{startDate:yyyy-MM-dd}' />");
        sb.AppendLine($"<input type='hidden' name='groupCount' value='{groupCount}' />");
        sb.AppendLine("<button type='submit'>&lt; Previous</button>");
    }
    sb.AppendLine("</form>");
    // Next button
    sb.AppendLine("<form method='get' style='display:inline;margin-left:10px;'>");
    if (!string.IsNullOrEmpty(response.NextPageToken))
    {
        sb.AppendLine($"<input type='hidden' name='pageToken' value='{response.NextPageToken}' />");
        sb.AppendLine($"<input type='hidden' name='startDate' value='{startDate:yyyy-MM-dd}' />");
        sb.AppendLine($"<input type='hidden' name='groupCount' value='{groupCount}' />");
        sb.AppendLine($"<input type='hidden' name='page' value='{page + 1}' />");
        sb.AppendLine("<button type='submit'>Next Page &gt;</button>");
    }
    sb.AppendLine("</form>");
    sb.AppendLine("</div>");
    // Burst groups
    foreach (var group in grouped)
    {
        sb.AppendLine($"<h3>Burst group at {group.Key}</h3><div style='display:flex;gap:10px;'>");
        foreach (var item in group)
        {
            var thumbUrl = item.BaseUrl + "=w200-h200";
            sb.AppendLine($"<div style='text-align:center;'>");
            sb.AppendLine($"<a href='{item.ProductUrl}' target='photodedup'><img src='{thumbUrl}' style='border:1px solid #ccc;max-width:200px;max-height:200px;'/></a><br/>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</div>");
    }
    // Bottom controls (duplicate)
    sb.AppendLine("<div style='margin-top:20px;'>");
    sb.AppendLine($"<form method='get' style='display:inline;'>"
        + "<label>Start date: <input type='date' name='startDate' value='" + startDate.ToString("yyyy-MM-dd") + "'></label> "
        + "<label style='margin-left:10px;'>Minimum photos in burst group: <input type='number' name='groupCount' min='2' value='" + groupCount + "' style='width:60px;'/></label> "
        + "<input type='number' name='page' min='1' value='" + page + "' style='width:60px;margin-left:10px;'/> "
        + "<button type='submit'>Go</button></form>");
    sb.AppendLine($"<span style='margin-left:20px;'>Page {page}</span>");
    // Previous button (bottom)
    sb.AppendLine("<form method='get' style='display:inline;margin-left:10px;'>");
    if (page > 1)
    {
        sb.AppendLine($"<input type='hidden' name='page' value='{page - 1}' />");
        sb.AppendLine($"<input type='hidden' name='startDate' value='{startDate:yyyy-MM-dd}' />");
        sb.AppendLine($"<input type='hidden' name='groupCount' value='{groupCount}' />");
        sb.AppendLine("<button type='submit'>&lt; Previous</button>");
    }
    sb.AppendLine("</form>");
    // Next button (bottom)
    sb.AppendLine("<form method='get' style='display:inline;margin-left:10px;'>");
    if (!string.IsNullOrEmpty(response.NextPageToken))
    {
        sb.AppendLine($"<input type='hidden' name='pageToken' value='{response.NextPageToken}' />");
        sb.AppendLine($"<input type='hidden' name='startDate' value='{startDate:yyyy-MM-dd}' />");
        sb.AppendLine($"<input type='hidden' name='groupCount' value='{groupCount}' />");
        sb.AppendLine($"<input type='hidden' name='page' value='{page + 1}' />");
        sb.AppendLine("<button type='submit'>Next Page &gt;</button>");
    }
    sb.AppendLine("</form>");
    sb.AppendLine("</div>");
    sb.AppendLine("</body></html>");
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(sb.ToString());
    return;
});

app.Run();
