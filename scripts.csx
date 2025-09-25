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
        case "GetDatasetSchema":
          return await HandleGetDatasetSchema().ConfigureAwait(false);
        case "ListDatasets":
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

  /// <summary>
  /// Builds a new HTTP request for the dataset items API by modifying the path from schema helper to items endpoint.
  /// Follows the same pattern as BuildActorRequest for consistency.
  /// </summary>
  /// <returns>An <see cref="HttpRequestMessage"/> configured for the dataset items API endpoint.</returns>
  private HttpRequestMessage BuildDatasetItemsRequest() {
    var originalUri = Context.Request.RequestUri;
    var uriBuilder = new UriBuilder(originalUri);
    
    // Correct path to get items
    uriBuilder.Path = uriBuilder.Path.Replace("/itemsSchemaHelper", "/items");

    // Create a new request instead of modifying the original
    var newRequest = new HttpRequestMessage(Context.Request.Method, uriBuilder.Uri);
    
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
  /// Handles the GetDatasetSchema operation by fetching sample dataset items and inferring an OpenAPI schema.
  /// Follows the established error handling pattern used by other handler methods.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> containing the inferred OpenAPI schema as JSON.
  /// </returns>
  private async Task<HttpResponseMessage> HandleGetDatasetSchema() {
    try {
      var modifiedRequest = BuildDatasetItemsRequest();
      var upstreamResponse = await Context.SendAsync(modifiedRequest, CancellationToken).ConfigureAwait(false);
      
      if (!upstreamResponse.IsSuccessStatusCode) {
        return upstreamResponse; // Return error responses as-is
      }

      var sample = await ExtractSampleFromResponse(upstreamResponse).ConfigureAwait(false);
      var schema = InferOpenApiSchemaFromSample(sample);

      var response = new HttpResponseMessage(HttpStatusCode.OK);
      response.Content = CreateJsonContent(schema.ToString(Newtonsoft.Json.Formatting.None));
      return response;
    }
    catch (Exception ex) {
      // Fallback to passthrough on any error
      return await HandlePassthrough().ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Infers an OpenAPI (Swagger 2.0) schema from a sample JSON token.
  /// Recursively analyzes the structure and data types to generate appropriate schema definitions.
  /// </summary>
  /// <param name="sample">The JSON token to analyze for schema inference.</param>
  /// <returns>
  /// A <see cref="JObject"/> representing the OpenAPI schema definition.
  /// </returns>
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
 
  /// <summary>
  /// Infers an OpenAPI object schema from a JSON object by analyzing its properties.
  /// Recursively processes each property to build a complete schema definition.
  /// </summary>
  /// <param name="obj">The JSON object to analyze for schema inference.</param>
  /// <returns>
  /// A <see cref="JObject"/> representing the OpenAPI object schema with properties.
  /// </returns>
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

  /// <summary>
  /// Extracts a sample JSON token from the HTTP response for schema inference.
  /// Handles JSON parsing errors gracefully and extracts the first item from arrays.
  /// </summary>
  /// <param name="response">The HTTP response containing JSON data.</param>
  /// <returns>
  /// A <see cref="JToken"/> representing the sample data, or null if parsing fails.
  /// </returns>
  private async Task<JToken> ExtractSampleFromResponse(HttpResponseMessage response) {
    var contentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    // Try to parse JSON
    JToken parsed;
    try {
      parsed = string.IsNullOrWhiteSpace(contentString) ? null : JToken.Parse(contentString);
    } catch (Exception) {
      parsed = null;
    }

    // Extract first item, if it's an array, otherwise use as-is
    return parsed is JArray arr && arr.Count > 0 ? arr[0] : parsed;
  }

  /// <summary>
  /// Wraps primitive types in an object schema with a "value" property.
  /// This ensures all schemas return objects, which is required for Power Automate compatibility.
  /// </summary>
  /// <param name="type">The OpenAPI type (e.g., "string", "integer", "boolean").</param>
  /// <param name="format">The optional OpenAPI format (e.g., "date-time", "int64").</param>
  /// <returns>
  /// A <see cref="JObject"/> representing an object schema containing the primitive type as a "value" property.
  /// </returns>
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
