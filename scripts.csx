using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

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
        case "GetUserInfo":
        case "RunActor":
        case "RunTask":
        case "ListMyActors":
        case "ListStoreActors":
        case "ListTasks":
          return await HandlePassthrough().ConfigureAwait(false);
        default:
          HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
          response.Content = CreateJsonContent($"Unknown operation ID '{Context.OperationId}'");
          return response;
      }
   }

   /// <summary>
   /// Handles the GetUserInfo operation by forwarding the request to the Apify API.
   /// This is a passthrough operation that retrieves information about the authenticated user.
   /// </summary>
   /// <returns>
   /// An <see cref="HttpResponseMessage"/> representing the HTTP response message including the status code and user data from the Apify API.
   /// </returns>
   private async Task<HttpResponseMessage> HandleGetUserInfo()
   {
      var request = Context.Request;
      
      // Use the Context.SendAsync method to send the request
      return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
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
}
