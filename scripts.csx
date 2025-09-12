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
   public override async Task<HttpResponseMessage> ExecuteAsync() {
      switch (Context.OperationId) {
        case "ListActorsUnified":
          return await HandleListActorsUnified().ConfigureAwait(false);
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

   private async Task<HttpResponseMessage> HandlePassthrough() {
      return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
   }

  private async Task<HttpResponseMessage> HandleListActorsUnified() {
    var request = Context.Request;
    var originalUri = request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
    var actorScope = queryParams["actor_scope"];

    // Decide target path based on scope
    var uriBuilder = new UriBuilder(originalUri);
    if (string.Equals(actorScope, "StoreActors", StringComparison.OrdinalIgnoreCase)) {
      uriBuilder.Path = "/v2/store";
    } else {
      uriBuilder.Path = "/v2/acts";
    }

    // Add query parameters to the request URI
    uriBuilder.Query = queryParams.ToString();

    // Forward the request to fetch raw list
    request.RequestUri = uriBuilder.Uri;
    return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
  }
}
