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
        case "GetKeyValueStoreRecordSchema":
          return await HandleGetKeyValueStoreRecordSchema().ConfigureAwait(false);
        case "GetKeyValueStoreRecord":
        case "ListDatasets":
        case "GetDatasetItems":
        case "GetUserInfo":
        case "RunActor":
        case "RunTask":
        case "ListMyActors":
        case "ListStoreActors":
        case "ListKeyValueStores":
        case "ListRecordKeys":
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
  /// Generic method to handle formatted responses by applying request modification and response formatting.
  /// </summary>
  /// <param name="requestModifier">Function to modify the request before sending</param>
  /// <param name="responseFormatter">Action to format the response items</param>
  /// <returns>An <see cref="HttpResponseMessage"/> with formatted content</returns>
  private async Task<HttpResponseMessage> HandleFormattedResponse(Func<HttpRequestMessage> requestModifier, Action<JArray> responseFormatter) {
    try {
      var modifiedRequest = requestModifier();
      var response = await Context.SendAsync(modifiedRequest, CancellationToken).ConfigureAwait(false);
      return await FormatApiResponse(response, responseFormatter).ConfigureAwait(false);
    }
    catch (Exception ex) {
      // Fallback to passthrough on any error
      return await HandlePassthrough().ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Handles the ListActorsDropdown operation by routing to the appropriate API endpoint and formatting the response.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message with formatted actor titles.
  /// </returns>
  private async Task<HttpResponseMessage> HandleListActorsDropdown() {
    return await HandleFormattedResponse(BuildActorRequest, items => FormatItems(items, FormatActorTitle)).ConfigureAwait(false);
  }

  /// <summary>
  /// Modifies the existing HTTP request for the actor API by determining the correct endpoint and removing helper parameters.
  /// Includes null safety checks for robustness.
  /// </summary>
  /// <returns>The modified <see cref="HttpRequestMessage"/> configured for the appropriate actor API endpoint.</returns>
  private HttpRequestMessage BuildActorRequest() {
    var request = Context.Request;
    if (request?.RequestUri == null) return request;
    
    var originalUri = request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
    var actorScope = queryParams["actorScope"];

    string newPath = DetermineApiPath(actorScope);
    queryParams.Remove("actorScope");

    var newUri = new UriBuilder(originalUri) { 
      Path = newPath,
      Query = queryParams.ToString()
    }.Uri;

    // Modify the existing request URI instead of creating a new request
    request.RequestUri = newUri;

    return request;
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
    return await HandleFormattedResponse(() => Context.Request, items => FormatItems(items, FormatTaskTitle)).ConfigureAwait(false);
  }

  /// <summary>
  /// Generic method to format items in a JArray by applying a formatting function to each valid item.
  /// </summary>
  /// <param name="items">The JArray of items to format</param>
  /// <param name="formatter">Function to apply formatting to each JObject item</param>
  private void FormatItems(JArray items, Action<JObject> formatter) {
    if (items == null || items.Count == 0) return;
    
    for (int i = 0; i < items.Count; i++) {
      var item = items[i] as JObject;
      if (item == null) continue;
      formatter(item);
    }
  }

  /// <summary>
  /// Formats actor titles by combining title, username, and name for better user experience.
  /// </summary>
  /// <param name="item">The JObject representing an actor item</param>
  private void FormatActorTitle(JObject item) {
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

  /// <summary>
  /// Formats task names by combining name and actName for better user experience.
  /// </summary>
  /// <param name="item">The JObject representing a task item</param>
  private void FormatTaskTitle(JObject item) {
    var name = item["name"]?.Value<string>();
    var actName = item["actName"]?.Value<string>();

    // Only format if we have all required fields
    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(actName)) {
      // Update the name field with formatted string: "name / (actName)"
      item["name"] = $"{name} / ({actName})";
    }
  }

  /// <summary>
  /// Generic method to modify the request path by replacing a pattern with a new value.
  /// Includes null safety checks for robustness.
  /// </summary>
  /// <param name="oldPattern">The pattern to replace in the path</param>
  /// <param name="newPattern">The replacement pattern</param>
  private void ModifyRequestPath(string oldPattern, string newPattern) {
    var request = Context.Request;
    if (request?.RequestUri == null) return;
    
    var originalUri = request.RequestUri;
    var uriBuilder = new UriBuilder(originalUri);
    
    uriBuilder.Path = uriBuilder.Path.Replace(oldPattern, newPattern);
    request.RequestUri = uriBuilder.Uri;
  }

  /// <summary>
  /// Generic method to handle schema generation operations by modifying the request, fetching data, and inferring an OpenAPI schema.
  /// </summary>
  /// <param name="requestModifier">Action to modify the request before sending</param>
  /// <returns>An <see cref="HttpResponseMessage"/> containing the inferred OpenAPI schema as JSON.</returns>
  private async Task<HttpResponseMessage> HandleSchemaGeneration(Action requestModifier) {
    try {
      requestModifier();
      var upstreamResponse = await HandlePassthrough().ConfigureAwait(false);
      
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
  /// Handles the GetDatasetSchema operation by fetching sample dataset items and inferring an OpenAPI schema.
  /// </summary>
  /// <returns>An <see cref="HttpResponseMessage"/> containing the inferred OpenAPI schema as JSON.</returns>
  private async Task<HttpResponseMessage> HandleGetDatasetSchema() {
    return await HandleSchemaGeneration(() => ModifyRequestPath("/itemsSchemaHelper", "/items")).ConfigureAwait(false);
  }

  /// <summary>
  /// Handles the GetKeyValueStoreRecordSchema operation by fetching a key-value store record and inferring an OpenAPI schema.
  /// </summary>
  /// <returns>An <see cref="HttpResponseMessage"/> containing the inferred OpenAPI schema as JSON.</returns>
  private async Task<HttpResponseMessage> HandleGetKeyValueStoreRecordSchema() {
    return await HandleSchemaGeneration(() => ModifyRequestPath("/schemaHelper", "")).ConfigureAwait(false);
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
        return new JObject { ["type"] = "integer", ["format"] = "int64" };
      case JTokenType.Float:
        return new JObject { ["type"] = "number", ["format"] = "double" };
      case JTokenType.Boolean:
        return new JObject { ["type"] = "boolean" };
      case JTokenType.Date:
        return new JObject { ["type"] = "string", ["format"] = "date-time" };
      case JTokenType.String:
        // Check if this is our binary data marker
        if (sample.ToString() == "__BINARY_DATA__") {
          return new JObject { ["type"] = "string", ["format"] = "binary" };
        }
        return new JObject { ["type"] = "string" };
      default:
        return new JObject { ["type"] = "string" };
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
    // Check if this is binary content based on Content-Type
    var contentType = response.Content.Headers.ContentType?.MediaType;
    if (contentType != null && !contentType.StartsWith("text/") && !contentType.Contains("json") && !contentType.Contains("xml")) {
      // For binary content, return a special marker to indicate binary data
      return JToken.FromObject("__BINARY_DATA__");
    }

    var contentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    // Try to parse JSON
    JToken parsed;
    try {
      parsed = string.IsNullOrWhiteSpace(contentString) ? null : JToken.Parse(contentString);
    } catch (Exception) {
      // If it's not JSON, treat it as a string value
      parsed = string.IsNullOrWhiteSpace(contentString) ? null : JToken.FromObject(contentString);
    }

    // Extract first item, if it's an array, otherwise use as-is
    return parsed is JArray arr && arr.Count > 0 ? arr[0] : parsed;
  }

}
