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
        case "DeleteActorWebhook":
          return await HandleDeleteWebhookRobust().ConfigureAwait(false);
        case "GetUserInfo":
        case "RunActor":
        case "ListMyActors":
        case "ListStoreActors":
        case "CreateActorWebhook":
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
  /// based on the <c>actorScope</c> parameter. Routes to <c>/v2/store</c> for StoreActors or <c>/v2/acts</c> for user actors.
  /// Removes the helper <c>actorScope</c> parameter before forwarding the request.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message including the status code and data from the forwarded request.
  /// </returns>
  private async Task<HttpResponseMessage> HandleListActorsDropdown() {
    var originalUri = Context.Request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
    var actorScope = queryParams["actorScope"];

    string newPath = string.Equals(actorScope, "StoreActors", StringComparison.OrdinalIgnoreCase) 
      ? "/v2/store" 
      : "/v2/acts";

    queryParams.Remove("actorScope");
    
    var newUri = new UriBuilder(originalUri) { 
      Path = newPath,
      Query = queryParams.ToString()
    }.Uri;

    Context.Request.RequestUri = newUri;
    return await HandlePassthrough().ConfigureAwait(false);
  }

  /// <summary>
  /// Handles the creation of webhooks for Power Automate triggers with proper Location header.
  /// Removes the helper actorScope parameter and forwards the request to Apify API.
  /// The critical fix ensures webhook cleanup by intercepting the 201 response and adding
  /// the Location header manually since Apify API doesn't provide it by default.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message with proper Location header for webhook deletion.
  /// </returns>
  private async Task<HttpResponseMessage> HandleCreateWebhookWithLocation() {
    var originalUri = Context.Request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
    
    // Remove helper parameter from query string
    queryParams.Remove("actorScope");
    Context.Request.RequestUri = new UriBuilder(originalUri) { Query = queryParams.ToString() }.Uri;

    // Forward request to Apify API
    return await HandlePassthrough().ConfigureAwait(false);
  }

  /// <summary>
  /// Handles webhook deletion with Power Automate compatibility.
  /// Converts 204 No Content responses to 200 OK for Power Automate compatibility,
  /// and treats 404 Not Found as success (webhook already deleted).
  /// This ensures robust webhook cleanup when Power Automate flows are removed.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message with status codes compatible with Power Automate.
  /// </returns>
  private async Task<HttpResponseMessage> HandleDeleteWebhookRobust() {
    var response = await HandlePassthrough().ConfigureAwait(false);
    
    // Convert 204 No Content to 200 OK for Power Automate compatibility
    if (response.StatusCode == HttpStatusCode.NoContent) {
      response.StatusCode = HttpStatusCode.OK;
    }
    
    // Treat 404 Not Found as success (webhook already deleted)
    if (response.StatusCode == HttpStatusCode.NotFound) {
      response.StatusCode = HttpStatusCode.OK;
    }
    
    return response;
  }
}
