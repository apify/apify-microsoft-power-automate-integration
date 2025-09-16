using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Script : ScriptBase {
   /// <summary>
   /// Main entry point for the Power Automate custom connector script.
   /// Routes incoming requests to appropriate handlers based on the operation ID.
   /// </summary>
   /// <returns>
   /// An <see cref="HttpResponseMessage"/> representing the HTTP response message including the status code and data.
   /// </returns>
   public override async Task<HttpResponseMessage> ExecuteAsync() {
      switch (Context.OperationId) {
        case "ListActorsDropdown":
          return await HandleListActorsDropdown().ConfigureAwait(false);
        case "ActorRunFinishedTrigger":
          return await HandleCreateWebhookWithLocation().ConfigureAwait(false);
        case "GetUserInfo":
        case "RunActor":
        case "ListMyActors":
        case "ListStoreActors":
        case "CreateActorWebhook":
        case "DeleteActorWebhook":
          return await HandlePassthrough().ConfigureAwait(false);
        default:
          HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
          response.Content = CreateJsonContent($"Unknown operation ID '{Context.OperationId}'");
          return response;
      }
   }

   /// <summary>
   /// Handles passthrough operations by forwarding the original request to the Apify API.
   /// Used for operations that don't require any special processing or transformation.
   /// </summary>
   /// <returns>
   /// An <see cref="HttpResponseMessage"/> representing the HTTP response message including the status code and data from the forwarded request.
   /// </returns>
   private async Task<HttpResponseMessage> HandlePassthrough() {
      return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
   }

  /// <summary>
  /// Handles the ListActorsDropdown operation by dynamically routing to the appropriate Apify API endpoint
  /// based on the <c>actor_scope</c> parameter. Routes to <c>/v2/store</c> for StoreActors or <c>/v2/acts</c> for user actors.
  /// Removes the helper <c>actor_scope</c> parameter before forwarding the request.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message including the status code and data from the forwarded request.
  /// </returns>
  private async Task<HttpResponseMessage> HandleListActorsDropdown() {
    var originalUri = Context.Request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
    var actorScope = queryParams["actor_scope"];

    string newPath = string.Equals(actorScope, "StoreActors", StringComparison.OrdinalIgnoreCase) 
      ? "/v2/store" 
      : "/v2/acts";

    queryParams.Remove("actor_scope");
    
    var newUri = new UriBuilder(originalUri) { 
      Path = newPath,
      Query = queryParams.ToString()
    }.Uri;

    Context.Request.RequestUri = newUri;
    return await HandlePassthrough().ConfigureAwait(false);
  }

  private async Task<HttpResponseMessage> HandleCreateWebhookWithLocation() {
    var originalUri = Context.Request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
    if (queryParams["actor_scope"] != null) {
      queryParams.Remove("actor_scope");
      Context.Request.RequestUri = new UriBuilder(originalUri) { Query = queryParams.ToString() }.Uri;
    }

    var response = await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.Created && !response.Headers.Contains("Location")) {
      try {
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(content)) {
          var json = JsonConvert.DeserializeObject<JObject>(content);
          var id = json?["data"]?["id"]?.ToString();
          if (!string.IsNullOrEmpty(id)) {
            response.Headers.Location = new Uri($"https://api.apify.com/v2/webhooks/{id}");
          }
        }
      } catch {
      }
    }

    return response;
  }
}
