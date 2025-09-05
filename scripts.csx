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
      var request = Context.Request;
      var queryParams = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
      var actorScope = queryParams["actor_scope"];
      var myActorId = queryParams["my_actor_id"];
      var storeActorId = queryParams["store_actor_id"];

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

      // Create a new uri: /v2/acts/{actorId}/runs
      var uriBuilder = new UriBuilder(request.RequestUri);
      uriBuilder.Path = "/v2/acts/" + Uri.EscapeDataString(finalActorId) + "/runs";

      // Recompose query
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
