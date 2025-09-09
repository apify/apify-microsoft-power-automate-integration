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
        case "RunTask":
           return await HandleRunTask().ConfigureAwait(false);
        case "ListTasks":
           return await HandleListTasks().ConfigureAwait(false);
        case "GetUserInfo":
          return await HandleGetUserInfo().ConfigureAwait(false) ;
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

  private async Task<HttpResponseMessage> HandleRunTask()
  {
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
}
