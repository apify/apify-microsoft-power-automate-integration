using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
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
        case "ListTasks":
          return await HandleListTasks().ConfigureAwait(false);
        case "ListDatasets":
          return await HandleListDatasets().ConfigureAwait(false);
        case "GetDatasetSchema":
          return await HandleGetDatasetSchema().ConfigureAwait(false);
        case "GetDatasetItems":
        case "GetUserInfo":
        case "RunActor":
        case "RunTask":
        case "ListMyActors":
        case "ListStoreActors":
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
  /// Handles the ListActorsDropdown operation by routing to the appropriate API endpoint and formatting the response.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message with formatted actor titles.
  /// </returns>
  private async Task<HttpResponseMessage> HandleListActorsDropdown() {
    try {
      var modifiedRequest = BuildActorRequest();
      var response = await Context.SendAsync(modifiedRequest, CancellationToken).ConfigureAwait(false);
      return await FormatApiResponse(response, FormatActorTitles).ConfigureAwait(false);
    }
    catch (Exception ex) {
      // Fallback to passthrough on any error
      return await HandlePassthrough().ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Builds a new HTTP request for the actor API by determining the correct endpoint and removing helper parameters.
  /// </summary>
  /// <returns>An <see cref="HttpRequestMessage"/> configured for the appropriate actor API endpoint.</returns>
  private HttpRequestMessage BuildActorRequest() {
    var originalUri = Context.Request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
    var actorScope = queryParams["actorScope"];

    string newPath = DetermineApiPath(actorScope);
    queryParams.Remove("actorScope");

    var newUri = new UriBuilder(originalUri) { 
      Path = newPath,
      Query = queryParams.ToString()
    }.Uri;

    // Create a new request instead of modifying the original
    var newRequest = new HttpRequestMessage(Context.Request.Method, newUri);
    
    // Copy headers from original request
    foreach (var header in Context.Request.Headers) {
      newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    // Copy content if present
    if (Context.Request.Content != null) {
      newRequest.Content = Context.Request.Content;
    }

    return newRequest;
  }

  /// <summary>
  /// Determines the appropriate API path based on the actor scope parameter.
  /// </summary>
  /// <param name="actorScope">The actor scope from the query parameters.</param>
  /// <returns>The API path to use for the request.</returns>
  private static string DetermineApiPath(string actorScope) {
    return string.Equals(actorScope, "StoreActors", StringComparison.OrdinalIgnoreCase) 
      ? "/v2/store" 
      : "/v2/acts";
  }

  /// <summary>
  /// Generic method to format API responses by applying a transformation function to the items array.
  /// Handles JSON parsing, error checking, and response reconstruction.
  /// </summary>
  /// <param name="response">The original HTTP response from the API</param>
  /// <param name="formatAction">Action to apply to the items array for formatting</param>
  /// <returns>A new HttpResponseMessage with the formatted content</returns>
  private async Task<HttpResponseMessage> FormatApiResponse(HttpResponseMessage response, Action<JArray> formatAction) {
    if (!response.IsSuccessStatusCode) {
      return response; // Return error responses as-is
    }

    try {
      // Read and parse the JSON response
      var jsonContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
      var jsonObject = JObject.Parse(jsonContent);
      
      // Apply formatting to items array if it exists
      var items = jsonObject["data"]?["items"] as JArray;
      if (items != null) {
        formatAction(items);
      }

      // Create new response with formatted content
      var formattedContent = jsonObject.ToString(Newtonsoft.Json.Formatting.None);
      var newResponse = new HttpResponseMessage(response.StatusCode) {
        Content = new StringContent(formattedContent, Encoding.UTF8, "application/json")
      };

      // Copy headers from original response
      foreach (var header in response.Headers) {
        newResponse.Headers.TryAddWithoutValidation(header.Key, header.Value);
      }

      return newResponse;
    }
    catch (Exception ex) {
      // Return original response on any formatting error
      return response;
    }
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
  /// Formats actor titles by combining title, username, and name for better user experience.
  /// </summary>
  /// <param name="items">The JArray of actor items to format</param>
  private void FormatActorTitles(JArray items) {
    if (items == null || items.Count == 0) return;
    
    for (int i = 0; i < items.Count; i++) {
      var item = items[i] as JObject;
      if (item == null) continue;

      var title = item["title"]?.Value<string>();
      var name = item["name"]?.Value<string>();
      var username = item["username"]?.Value<string>();

      // Only format if we have all required fields
      if (!string.IsNullOrEmpty(title) && 
          !string.IsNullOrEmpty(name) && 
          !string.IsNullOrEmpty(username)) {
        // Update the title field with formatted string
        item["title"] = $"{title} ({username}/{name})";
      }
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
        // Update the name field with formatted string: "name / (actName)"
        item["name"] = $"{name} / ({actName})";
      }
    }
  }

  private async Task<HttpResponseMessage> HandleGetDatasetSchema() {
    var request = Context.Request;
    var uriBuilder = new UriBuilder(request.RequestUri);

    // Correct path to get items
    uriBuilder.Path = uriBuilder.Path.Replace("/items-schema-helper", "/items");

    // Set limit to 1 for schema inference
    var queryParams = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
    queryParams["limit"] = "1";
    uriBuilder.Query = queryParams.ToString();

    // Correct request URI
    request.RequestUri = uriBuilder.Uri;

    // Call Apify API to get a sample item
    var upstreamResponse = await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
    var contentString = await upstreamResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

    // Try to parse JSON
    JToken parsed;
    try {
      parsed = string.IsNullOrWhiteSpace(contentString) ? null : JToken.Parse(contentString);
    } catch (Exception) {
      parsed = null;
    }

    // Extract first item, if it's an array, otherwise use as-is
    var sample = parsed is JArray arr && arr.Count > 0 ? arr[0] : parsed;

    // Infer OpenAPI (Swagger 2.0) schema from the sample
    var schema = InferOpenApiSchemaFromSample(sample);

    var response = new HttpResponseMessage(HttpStatusCode.OK);
    response.Content = CreateJsonContent(schema.ToString(Newtonsoft.Json.Formatting.None));
    return response;
  }

  private JObject InferOpenApiSchemaFromSample(JToken sample) {
    // If no sample, return very permissive object schema
    if (sample == null || sample.Type == JTokenType.Null || sample.Type == JTokenType.Undefined) {
      return new JObject
      {
        ["type"] = "object",
        ["additionalProperties"] = true
      };
    }

    switch (sample.Type) {
      case JTokenType.Object:
        return InferObjectSchema((JObject)sample);
      case JTokenType.Array:
        // Wrap arrays in an object to ensure the returned schema is always an object
        return new JObject
        {
          ["type"] = "object",
          ["properties"] = new JObject
          {
            ["value"] = new JObject
            {
              ["type"] = "array",
              ["items"] = InferOpenApiSchemaFromSample(((JArray)sample).FirstOrDefault())
            }
          }
        };
      case JTokenType.Integer:
        return WrapPrimitive("integer", "int64");
      case JTokenType.Float:
        return WrapPrimitive("number", "double");
      case JTokenType.Boolean:
        return WrapPrimitive("boolean", null);
      case JTokenType.Date:
        return WrapPrimitive("string", "date-time");
      case JTokenType.String:
        return WrapPrimitive("string", null);
      default:
        return WrapPrimitive("string", null);
    }
  }
 
  private JObject InferObjectSchema(JObject obj) {
    var properties = new JObject();
    foreach (var prop in obj.Properties()) {
      properties[prop.Name] = InferOpenApiSchemaFromSample(prop.Value);
    }
    return new JObject
    {
      ["type"] = "object",
      ["properties"] = properties
    };
  }

  // Wrap primitive types in an object with a "value" property
  private JObject WrapPrimitive(string type, string format) {
    var inner = new JObject { ["type"] = type };
    if (!string.IsNullOrEmpty(format)) {
      inner["format"] = format;
    }
    return new JObject
    {
      ["type"] = "object",
      ["properties"] = new JObject { ["value"] = inner }
    };
  }
}
