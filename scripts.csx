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
        
        case "ActorTaskFinishedTrigger":
          return await HandleCreateTaskWebhookWithLocation().ConfigureAwait(false);
        case "DeleteTaskWebhook":
          return await HandleDeleteTaskWebhook().ConfigureAwait(false);
        case "ListTasks":
          return await HandleListTasks().ConfigureAwait(false);
        case "GetUserInfo":
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
  /// Handles task webhook creation with Location header fix.
  /// </summary>
  private async Task<HttpResponseMessage> HandleCreateTaskWebhookWithLocation() {
    var originalUri = Context.Request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
    
    // Extract values from query parameters
    var taskId = queryParams["taskId"];
    var eventTypesParam = queryParams["eventTypes"];
    
    // Parse eventTypes (can be comma-separated or multiple parameters)
    var eventTypes = new List<string>();
    if (!string.IsNullOrEmpty(eventTypesParam)) {
      eventTypes.AddRange(eventTypesParam.Split(',').Select(e => e.Trim()));
    }
    
    // Read existing body or create new one
    var bodyContent = string.Empty;
    if (Context.Request.Content != null) {
      bodyContent = await Context.Request.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
    
    JObject bodyJson;
    if (string.IsNullOrWhiteSpace(bodyContent)) {
      bodyJson = new JObject();
    } else {
      bodyJson = JsonConvert.DeserializeObject<JObject>(bodyContent) ?? new JObject();
    }
    
    // Populate body with values from query parameters
    if (!string.IsNullOrEmpty(taskId)) {
      if (bodyJson["condition"] == null) {
        bodyJson["condition"] = new JObject();
      }
      bodyJson["condition"]["actorTaskId"] = taskId;
    }
    
    if (eventTypes.Count > 0) {
      bodyJson["eventTypes"] = new JArray(eventTypes);
    }
    
    // Update request body
    var updatedBodyContent = JsonConvert.SerializeObject(bodyJson);
    Context.Request.Content = new StringContent(updatedBodyContent, Encoding.UTF8, "application/json");
    
    // Remove helper parameters from query string and update path to standard webhooks endpoint
    queryParams.Remove("task_id");
    queryParams.Remove("eventTypes");
    Context.Request.RequestUri = new UriBuilder(originalUri) { 
      Path = "/v2/webhooks",
      Query = queryParams.ToString() 
    }.Uri;

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

  /// <summary>
  /// Handles webhook deletion with Power Automate compatibility.
  /// Converts 204 No Content responses to 200 OK for Power Automate compatibility.
  /// </summary>
  private async Task<HttpResponseMessage> HandleDeleteWebhookRobust() {
    var response = await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
    
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

  /// <summary>
  /// Handles task webhook deletion by routing to standard webhooks endpoint.
  /// Routes /webhooks/task/{webhookId} to /webhooks/{webhookId} and applies robust deletion handling.
  /// </summary>
  private async Task<HttpResponseMessage> HandleDeleteTaskWebhook() {
    var originalUri = Context.Request.RequestUri;
    
    // Update path from /webhooks/task/{webhookId} to /webhooks/{webhookId}
    Context.Request.RequestUri = new UriBuilder(originalUri) { 
      Path = originalUri.AbsolutePath.Replace("/webhooks/task/", "/webhooks/")
    }.Uri;

    return await HandleDeleteWebhookRobust().ConfigureAwait(false);
  }

  /// <summary>
  /// Handles the ListTasks operation by formatting task names to include actor names.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message with formatted task names.
  /// </returns>
  private async Task<HttpResponseMessage> HandleListTasks() {
    try {
      var response = await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
      return await FormatApiResponse(response, FormatTaskTitles).ConfigureAwait(false);
    }
    catch (Exception ex) {
      // Fallback to passthrough on any error
      return await HandlePassthrough().ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Formats API response by applying a formatting function to the data.items array.
  /// </summary>
  /// <param name="response">The HTTP response to format</param>
  /// <param name="formatter">The formatting function to apply to the items array</param>
  /// <returns>A formatted HTTP response</returns>
  private async Task<HttpResponseMessage> FormatApiResponse(HttpResponseMessage response, Action<JArray> formatter) {
    if (response.StatusCode != HttpStatusCode.OK) {
      return response;
    }

    try {
      var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
      if (string.IsNullOrWhiteSpace(content)) {
        return response;
      }

      var json = JsonConvert.DeserializeObject<JObject>(content);
      var items = json?["data"]?["items"] as JArray;
      
      if (items != null) {
        formatter(items);
        var updatedContent = JsonConvert.SerializeObject(json);
        response.Content = new StringContent(updatedContent, Encoding.UTF8, "application/json");
      }

      return response;
    } catch {
      // Return original response on any error
      return response;
    }
  }

  /// <summary>
  /// Formats task names by combining name and actName for better user experience.
  /// </summary>
  /// <param name="items">The JArray of task items to format</param>
  private void FormatTaskTitles(JArray items) {
    if (items == null || items.Count == 0) return;
    
    for (int i = 0; i < items.Count; i++) {
      var item = items[i] as JObject;
      if (item == null) continue;

      var name = item["name"]?.Value<string>();
      var actName = item["actName"]?.Value<string>();

      // Only format if we have all required fields
      if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(actName)) {
        // Update the name field with formatted string: "name / ({actName})"
        item["name"] = $"{name} / ({actName})";
      }
    }
  }

  /// <summary>
  /// Creates JSON content for HTTP responses.
  /// </summary>
  /// <param name="message">The message to include in the JSON response</param>
  /// <returns>StringContent with JSON formatted message</returns>
  private StringContent CreateJsonContent(string message) {
    var json = JsonConvert.SerializeObject(new { error = message });
    return new StringContent(json, Encoding.UTF8, "application/json");
  }
}
