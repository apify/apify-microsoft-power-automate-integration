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
        case "RunTask":
           return await HandleRunTask().ConfigureAwait(false);
        case "ListTasks":
           return await HandleListTasks().ConfigureAwait(false);
        case "GetUserInfo":
        case "RunActor":
        case "ListMyActors":
        case "ListStoreActors":
          return await HandlePassthrough().ConfigureAwait(false);
        default:
          HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
          response.Content = CreateJsonContent($"Unknown operation ID '{Context.OperationId}'");
          return response;
      }
   }

   private async Task<HttpResponseMessage> HandleGetUserInfo()
   {
      var request = Context.Request;
      
      // Use the Context.SendAsync method to send the request
      return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
   }

  private async Task<HttpResponseMessage> HandleRunTask() {
     var request = Context.Request;
     var queryParams = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
     var taskId = queryParams["task_id"];

     if (string.IsNullOrWhiteSpace(taskId))
     {
        var error = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
        {
           Content = new StringContent(JsonConvert.SerializeObject(new
           {
              error = new { type = "invalid_request", message = "task_id must be provided" }
           }), Encoding.UTF8, "application/json")
        };
        return error;
     }

     // Build /v2/actor-tasks/{taskId}/runs
     var uriBuilder = new UriBuilder(request.RequestUri);
     uriBuilder.Path = "/v2/actor-tasks/" + Uri.EscapeDataString(taskId) + "/runs";

     // Recompose query without helper keys
     var newQuery = System.Web.HttpUtility.ParseQueryString(string.Empty);
     foreach (string key in queryParams.AllKeys)
     {
        if (string.IsNullOrEmpty(key)) continue;
        if (key.Equals("task_id", StringComparison.OrdinalIgnoreCase)) continue;
        if (key.Equals("taskId", StringComparison.OrdinalIgnoreCase)) continue;
        newQuery[key] = queryParams[key];
     }
     uriBuilder.Query = newQuery.ToString();

     request.RequestUri = uriBuilder.Uri;
     return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
  }

  private async Task<HttpResponseMessage> HandleListTasks() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
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
