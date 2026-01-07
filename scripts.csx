using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
      case "ListTasks":
        return await HandleListTasks().ConfigureAwait(false);
      case "GetDatasetSchema":
        return await HandleGetDatasetSchema().ConfigureAwait(false);
      case "ScrapeSingleUrl":
        return await HandleScrapeSingleUrl().ConfigureAwait(false);
      case "GetKeyValueStoreRecordSchema":
        return await HandleGetKeyValueStoreRecordSchema().ConfigureAwait(false);
      case "DeleteTaskWebhook":
        return await HandleDeleteTaskWebhook().ConfigureAwait(false);
      case "ActorTaskFinishedTrigger":
        return await HandleActorTaskFinishedTrigger().ConfigureAwait(false);
      case "ActorRunFinishedTrigger":
        return await HandleCreateWebhook().ConfigureAwait(false);
      case "DeleteActorWebhook":
        return await HandleDeleteWebhook().ConfigureAwait(false);
      case "RunActor":
      case "RunTask":
      case "GetUserInfo":
      case "ListDatasets":
      case "ListRecordKeys":
      case "ListStoreActors":
      case "GetDatasetItems":
      case "ListRecentActors":
      case "ListKeyValueStores":
      case "GetKeyValueStoreRecord":
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
  /// Validates query parameters before forwarding if validation rules exist for the operation.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message including the status code and data from the forwarded request.
  /// </returns>
  private async Task<HttpResponseMessage> HandlePassthrough() {
    // Validate query parameters if rules exist for this operation
    if (Context.Request?.RequestUri != null) {
      var queryParams = System.Web.HttpUtility.ParseQueryString(Context.Request.RequestUri.Query);
      var validation = ValidateQueryParameters(Context.OperationId, queryParams);
      if (!validation.IsValid) {
        return CreateValidationErrorResponse(validation);
      }
    }
    
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Handles the ScrapeSingleUrl operation by configuring and executing a web scraping request
  /// using the Apify Web Scraper actor for a single URL.
  /// Extracts URL and crawler type from the query string, builds a single-page scrape request, and forwards it.
  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response from the Apify Web Scraper actor,
  /// containing the scraped data and metadata for the specified URL.
  /// </returns>
  private async Task<HttpResponseMessage> HandleScrapeSingleUrl() {
    try {
      var request = Context.Request;
      var queryParams = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);

      // Validate query parameters
      var validation = ValidateQueryParameters("ScrapeSingleUrl", queryParams);
      if (!validation.IsValid) {
        return CreateValidationErrorResponse(validation);
      }

      var url = queryParams["url"];
      var crawlerType = queryParams["crawler_type"];

      // Check for required parameters
      if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(crawlerType)) {
        var errorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
        errorResponse.Content = CreateJsonContent("Missing required parameter: url or crawler_type");
        return errorResponse;
      }

      // Construct the JSON input body for the Web Scraper actor
      var inputBody = new {
        startUrls = new[] { new { url = url } },
        crawlerType = crawlerType,
        maxCrawlDepth = 0,
        maxCrawlPages = 1,
        maxResults = 1,
        proxyConfiguration = new { useApifyProxy = true },
        removeCookieWarnings = true,
        saveHtml = true,
        saveMarkdown = true
      };

      // Set the JSON body on the original request
      var jsonBody = JsonConvert.SerializeObject(inputBody);
      request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

      // Use HandlePassthrough to send the modified request
      return await HandlePassthrough().ConfigureAwait(false);
    }
    catch (Exception ex) {
      // Fallback to passthrough on any error
      return await HandlePassthrough().ConfigureAwait(false);
    }
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
  /// <returns>An <see cref="HttpResponseMessage"/> containing an OpenAPI schema that describes an array of items.</returns>
  private async Task<HttpResponseMessage> HandleGetDatasetSchema() {
    var schemaResponse = await HandleSchemaGeneration(() => ModifyRequestPath("/itemsSchemaHelper", "/items")).ConfigureAwait(false);
    
    if (!schemaResponse.IsSuccessStatusCode) {
      return schemaResponse; // Return error responses as-is
    }

    try {
      // Extract the schema JSON from the response
      var schemaJson = await schemaResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
      var itemSchema = JObject.Parse(schemaJson);
      
      // Modify the schema to describe an array of items (not wrap the schema in an array)
      var arraySchema = new JObject
      {
        ["type"] = "array",
        ["items"] = itemSchema
      };
      
      // Update the response content with the array schema
      schemaResponse.Content = CreateJsonContent(arraySchema.ToString(Newtonsoft.Json.Formatting.None));
      
      return schemaResponse;
    }
    catch (Exception ex) {
      // Return original response on any error
      return schemaResponse;
    }
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
          ["type"] = "array",
          ["items"] = InferOpenApiSchemaFromSample(((JArray)sample).FirstOrDefault()) 
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

  /// <summary>
  /// Handles the creation of webhooks for Power Automate triggers.
  /// Location header is provided by Apify API.
  /// Removes the helper actorScope parameter and forwards the request to Apify API.  /// </summary>
  /// <returns>
  /// An <see cref="HttpResponseMessage"/> representing the HTTP response message with proper Location header for webhook deletion.
  /// </returns>
  private async Task<HttpResponseMessage> HandleCreateWebhook() {
    var originalUri = Context.Request.RequestUri;
    var queryParams = System.Web.HttpUtility.ParseQueryString(originalUri.Query);

    // Remove helper parameter from query string
    queryParams.Remove("actorScope");
    Context.Request.RequestUri = new UriBuilder(originalUri) { Query = queryParams.ToString() }.Uri;

    // Forward request to Apify API (bypass validation in passthrough since we already validated)
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Handles webhook deletion by forwarding the delete request to the Apify API.
  /// Converts 204 No Content responses to 200 OK.
  /// </summary>
  private async Task<HttpResponseMessage> HandleDeleteWebhook() {
    var response = await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);

    // Convert 204 No Content to 200 OK (for Power Automate compatibility)
    if (response.StatusCode == HttpStatusCode.NoContent) {
      response.StatusCode = HttpStatusCode.OK;
    }

    return response;
  }

  /// <summary>
  /// Handles actor task finished trigger by routing to standard webhooks endpoint.
  /// Routes /webhooks/task to /webhooks and applies robust deletion handling.
  /// </summary>
  private async Task<HttpResponseMessage> HandleActorTaskFinishedTrigger() {
    // Update path from /webhooks/task to /webhooks
    ModifyRequestPath("/webhooks/task", "/webhooks");

    return await HandlePassthrough().ConfigureAwait(false);
  }

  /// <summary>
  /// Handles task webhook deletion by routing to standard webhooks endpoint.
  /// Routes /webhooks/task/{webhookId} to /webhooks/{webhookId} and applies robust deletion handling.
  /// </summary>
  private async Task<HttpResponseMessage> HandleDeleteTaskWebhook() {
    ModifyRequestPath("/webhooks/task/{webhookId}", "/webhooks/{webhookId}");

    return await HandleDeleteWebhook().ConfigureAwait(false);
  }

  /// <summary>
  /// Holds validation results with error collection.
  /// </summary>
  private class ValidationResult {
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; }
    
    public ValidationResult() {
      IsValid = true;
      Errors = new List<string>();
    }
    
    public void AddError(string error) {
      IsValid = false;
      Errors.Add(error);
    }
  }

  /// <summary>
  /// Delegate for parameter validation functions.
  /// </summary>
  private delegate ValidationResult ParameterValidator(string paramName, string paramValue);

  /// <summary>
  /// Validates that a parameter value is a valid URL.
  /// </summary>
  /// <param name="paramName">The name of the parameter being validated</param>
  /// <param name="paramValue">The value to validate</param>
  /// <returns>ValidationResult indicating success or failure with error message</returns>
  private ValidationResult ValidateUrl(string paramName, string paramValue) {
    var result = new ValidationResult();
    
    if (Uri.TryCreate(paramValue, UriKind.Absolute, out var uri) && 
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
      return result;
    }
    
    result.AddError($"Parameter '{paramName}' must be a valid URL.");
    return result;
  }

  /// <summary>
  /// Validates that a parameter value is a positive integer (greater than 0).
  /// </summary>
  /// <param name="paramName">The name of the parameter being validated</param>
  /// <param name="paramValue">The value to validate</param>
  /// <returns>ValidationResult indicating success or failure with error message</returns>
  private ValidationResult ValidatePositiveInteger(string paramName, string paramValue) {
    var result = new ValidationResult();
    
    if (int.TryParse(paramValue, out var value) && value > 0) {
      return result;
    }
    
    result.AddError($"Parameter '{paramName}' must be a positive integer");
    return result;
  }

  /// <summary>
  /// Validates that a parameter value is a non-negative integer (greater than or equal to 0).
  /// </summary>
  /// <param name="paramName">The name of the parameter being validated</param>
  /// <param name="paramValue">The value to validate</param>
  /// <returns>ValidationResult indicating success or failure with error message</returns>
  private ValidationResult ValidateNonNegativeInteger(string paramName, string paramValue) {
    var result = new ValidationResult();
    
    if (int.TryParse(paramValue, out var value) && value >= 0) {
      return result;
    }
    
    result.AddError($"Parameter '{paramName}' must be a non-negative integer");
    return result;
  }

  /// <summary>
  /// Validates that a parameter value is an integer within a specified range.
  /// </summary>
  /// <param name="paramName">The name of the parameter being validated</param>
  /// <param name="paramValue">The value to validate</param>
  /// <param name="min">Minimum allowed value (inclusive)</param>
  /// <param name="max">Maximum allowed value (inclusive)</param>
  /// <returns>ValidationResult indicating success or failure with error message</returns>
  private ValidationResult ValidateIntegerRange(string paramName, string paramValue, int min, int max) {
    var result = new ValidationResult();
    
    if (int.TryParse(paramValue, out var value) && value >= min && value <= max) {
      return result;
    }
    
    result.AddError($"Parameter '{paramName}' must be an integer between {min} and {max}");
    return result;
  }

  /// <summary>
  /// Validates that a parameter value is a valid wait for finish value (0-60).
  /// </summary>
  /// <param name="paramName">The name of the parameter being validated</param>
  /// <param name="paramValue">The value to validate</param>
  /// <returns>ValidationResult indicating success or failure with error message</returns>
  private ValidationResult ValidateWaitForFinish(string paramName, string paramValue) {
    return ValidateIntegerRange(paramName, paramValue, 0, 60);
  }

  /// <summary>
  /// Returns validation rules for each operation that requires query parameter validation.
  /// Maps operation IDs to their parameter validation rules.
  /// </summary>
  /// <returns>Dictionary mapping operation IDs to parameter validators</returns>
  private Dictionary<string, Dictionary<string, ParameterValidator>> GetValidationRules() {
    return new Dictionary<string, Dictionary<string, ParameterValidator>> {
      ["RunActor"] = new Dictionary<string, ParameterValidator> {
        ["waitForFinish"] = ValidateWaitForFinish,
        ["timeout"] = ValidatePositiveInteger
      },
      ["RunTask"] = new Dictionary<string, ParameterValidator> {
        ["waitForFinish"] = ValidateWaitForFinish,
        ["timeout"] = ValidatePositiveInteger
      },
      ["ScrapeSingleUrl"] = new Dictionary<string, ParameterValidator> {
        ["url"] = ValidateUrl
      },
      ["GetDatasetItems"] = new Dictionary<string, ParameterValidator> {
        ["limit"] = ValidatePositiveInteger,
        ["offset"] = ValidateNonNegativeInteger
      }
    };
  }

  /// <summary>
  /// Validates query parameters for a given operation based on configured validation rules.
  /// </summary>
  /// <param name="operationId">The operation ID to validate parameters for</param>
  /// <param name="queryParams">The query parameters collection to validate</param>
  /// <returns>ValidationResult containing all validation errors, if any</returns>
  private ValidationResult ValidateQueryParameters(string operationId, System.Collections.Specialized.NameValueCollection queryParams) {
    var result = new ValidationResult();
    var rules = GetValidationRules();
    
    // Check if the operation has validation rules
    if (!rules.ContainsKey(operationId)) {
      return result;
    }
    
    foreach (var rule in rules[operationId]) {
      var paramValue = queryParams[rule.Key];
      if (!string.IsNullOrEmpty(paramValue)) {
        var validationResult = rule.Value(rule.Key, paramValue);
        if (!validationResult.IsValid) {
          result.IsValid = false;
          result.Errors.AddRange(validationResult.Errors);
        }
      }
    }
    
    return result;
  }

  /// <summary>
  /// Creates a standardized HTTP 400 Bad Request response for validation errors.
  /// </summary>
  /// <param name="validation">The validation result containing error details</param>
  /// <returns>HttpResponseMessage with validation error details</returns>
  private HttpResponseMessage CreateValidationErrorResponse(ValidationResult validation) {
    var errorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
    var errorObject = new JObject {
      ["error"] = new JObject {
        ["type"] = "VALIDATION_ERROR",
        ["message"] = $"Query parameter validation failed: {string.Join("; ", validation.Errors)}"
      }
    };
    errorResponse.Content = CreateJsonContent(errorObject.ToString(Newtonsoft.Json.Formatting.None));
    return errorResponse;
  }
}
