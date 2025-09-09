using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Script : ScriptBase
{
  // This method is called before each operation
  public override async Task<HttpResponseMessage> ExecuteAsync()
  {
    switch (Context.OperationId)
    {
      case "ScrapeSingleUrl":
        return await HandleScrapeSingleUrl().ConfigureAwait(false);
      case "GetUserInfo":
        return await HandleGetUserInfo().ConfigureAwait(false) ;
      default:
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        response.Content = CreateJsonContent($"Unknown operation ID '{Context.OperationId}'");
        return response;
    }
  }

    // Handle the ScrapeSingleUrl operation
    private async Task<HttpResponseMessage> HandleScrapeSingleUrl()
    {
      var request = Context.Request;
      var queryParams = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
      
      var url = queryParams["url"];
      var crawlerType = queryParams["crawler_type"];

      // Construct the JSON input body for the Web Scraper actor
      var inputBody = new
      {
        startUrls = new[] { new { url = url } },
        crawlerType = crawlerType,
        maxCrawlDepth = 0,
        maxCrawlPages = 1,
        maxResults = 1,
        proxyConfiguration = new { useApifyProxy = true },
        removeCookieWarnings = true,
        saveHtml = true,
        saveMarkdown = true
      };

      // Set the JSON body
      var jsonBody = JsonConvert.SerializeObject(inputBody);
      request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

      // Set default query parameters
      // var newQuery = System.Web.HttpUtility.ParseQueryString(string.Empty);
      // newQuery["timeout"] = "0";

      // Update the request URI
      var uriBuilder = new UriBuilder(request.RequestUri);
      // uriBuilder.Query = newQuery.ToString();
      request.RequestUri = uriBuilder.Uri;

      return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> HandleGetUserInfo()
    {
      var request = Context.Request;
      
      // Use the Context.SendAsync method to send the request
      return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
    }
}
