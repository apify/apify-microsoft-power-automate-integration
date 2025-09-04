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
        case "RunActor":
           return await HandleRunActor().ConfigureAwait(false);
        case "GetUserInfo":
          return await HandleGetUserInfo().ConfigureAwait(false) ;
        case "ListMyActors":
          return await HandleListMyActors().ConfigureAwait(false);
        case "ListStoreActors":
          return await HandleListStoreActors().ConfigureAwait(false);
        default:
          HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
          response.Content = CreateJsonContent($"Unknown operation ID '{Context.OperationId}'");
          return response;
      }
   }

   // Handle the RunActor operation
   private async Task<HttpResponseMessage> HandleRunActor()
   {
      // Map UI parameters (actor_scope, my_actor_id, store_actor_id) to the path parameter {actorId}
      var request = Context.Request;

      // Read query parameters
      var queryParams = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
      var actorScope = queryParams["actor_scope"]; // my_actors | store_actors
      var myActorId = queryParams["my_actor_id"]; // user's actor id
      var storeActorId = queryParams["store_actor_id"]; // store actor name (e.g., apify/website-content-crawler)

      string finalActorId = null;
      if (string.Equals(actorScope, "my_actors", StringComparison.OrdinalIgnoreCase))
      {
         finalActorId = myActorId;
      }
      else if (string.Equals(actorScope, "store_actors", StringComparison.OrdinalIgnoreCase))
      {
         finalActorId = storeActorId;
      }

      if (string.IsNullOrWhiteSpace(finalActorId))
      {
         var error = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
         {
            Content = new StringContent(JsonConvert.SerializeObject(new
            {
               error = new { type = "invalid_request", message = "actor_scope and corresponding actor id must be provided" }
            }), Encoding.UTF8, "application/json")
         };
         return error;
      }

      // Build the new URI: /v2/acts/{actorId}/runs with same query params but without the UI-only ids
      var uriBuilder = new UriBuilder(request.RequestUri);
      uriBuilder.Path = "/v2/acts/" + Uri.EscapeDataString(finalActorId) + "/runs";

      // Recompose query without UI-only params
      var newQuery = System.Web.HttpUtility.ParseQueryString(string.Empty);
      foreach (string key in queryParams.AllKeys)
      {
         if (string.IsNullOrEmpty(key)) continue;
         if (key.Equals("my_actor_id", StringComparison.OrdinalIgnoreCase)) continue;
         if (key.Equals("store_actor_id", StringComparison.OrdinalIgnoreCase)) continue;
         if (key.Equals("actorId", StringComparison.OrdinalIgnoreCase)) continue;
         newQuery[key] = queryParams[key];
      }
      uriBuilder.Query = newQuery.ToString();

      // Replace the request URI
      request.RequestUri = uriBuilder.Uri;

      return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
   }

   private async Task<HttpResponseMessage> HandleGetUserInfo()
   {
      var request = Context.Request;
   
      // // Create a new request with the correct Apify API endpoint
      // var apiRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.apify.com/v2/users/me"));
      
      // // Copy headers from the original request
      // foreach (var header in request.Headers)
      // {
      //     apiRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
      // }
      
      // Use the Context.SendAsync method to send the request
      return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
   }

   private async Task<HttpResponseMessage> HandleListMyActors() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
   }

   private async Task<HttpResponseMessage> HandleListStoreActors() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
   }
}
