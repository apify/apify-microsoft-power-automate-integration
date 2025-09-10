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
   public override async Task<HttpResponseMessage> ExecuteAsync() {
      switch (Context.OperationId) {
        case "GetUserInfo":
          return await HandleGetUserInfo().ConfigureAwait(false) ;
        case "ListDatasets":
          return await HandleListDatasets().ConfigureAwait(false);
        case "GetDatasetSchema":
          return await HandleGetDatasetSchema().ConfigureAwait(false);
        case "GetDatasetItems":
          return await HandleGetDatasetItems().ConfigureAwait(false);
        default:
          HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
          response.Content = CreateJsonContent($"Unknown operation ID '{Context.OperationId}'");
          return response;
      }
   }

   private async Task<HttpResponseMessage> HandleGetUserInfo() {
      var request = Context.Request;
      
      // Use the Context.SendAsync method to send the request
      return await Context.SendAsync(request, CancellationToken).ConfigureAwait(false);
   }

  private async Task<HttpResponseMessage> HandleListDatasets() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
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

  private async Task<HttpResponseMessage> HandleGetDatasetItems() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
  }
}
