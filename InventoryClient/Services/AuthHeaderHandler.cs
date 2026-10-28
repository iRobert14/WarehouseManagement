using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace InventoryClient.Services;

public class AuthHeaderHandler(UserSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = session.AccessToken;
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && token is not null)
        {
            session.End();
        }

        return response;
    }
}