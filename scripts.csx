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
        case "GetUserInfo":
          return await HandleGetUserInfo().ConfigureAwait(false) ;
        case "ListKeyValueStores":
          return await HandleListKeyValueStores().ConfigureAwait(false);
        case "ListRecordKeys":
          return await HandleListRecordKeys().ConfigureAwait(false);
        case "GetKeyValueStoreRecord":
          return await HandleGetKeyValueStoreRecord().ConfigureAwait(false);
        default:
          HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
          response.Content = CreateJsonContent($"Unknown operation ID '{Context.OperationId}'");
          return response;
    }
  }

  private async Task<HttpResponseMessage> HandleGetUserInfo() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
  }

  private async Task<HttpResponseMessage> HandleListKeyValueStores() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
  }

  private async Task<HttpResponseMessage> HandleListRecordKeys() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
  }

  private async Task<HttpResponseMessage> HandleGetKeyValueStoreRecord() {
    return await Context.SendAsync(Context.Request, CancellationToken).ConfigureAwait(false);
  }
}
