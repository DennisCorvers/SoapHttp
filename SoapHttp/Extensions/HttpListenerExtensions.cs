using SoapHttp.Reflection;
using SoapHttp.Serialization;
using System.Net;

namespace SoapHttp.Extensions
{
    internal static class HttpListenerExtensions
    {
        internal static async Task RespondWithWcfMessage(this HttpListenerResponse response, object? responseMessage, WcfMessageInfo messageInfo)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            var serializer = new WcfSerializer();

            if (responseMessage == null)
                await serializer.Serialize(response.OutputStream, messageInfo);
            else
                await serializer.Serialize(response.OutputStream, responseMessage, messageInfo);

            response.Close();
        }

        internal static async Task RespondWithException(this HttpListenerResponse response, Exception exception)
        {
            response.StatusCode = (int)ResolveStatusCode(exception);

            var serializer = new WcfSerializer();
            await serializer.SerializeException(response.OutputStream, exception);

            response.Close();
        }


        internal static void RespondWithEmpty(this HttpListenerResponse response, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            response.SendChunked = false;
            response.StatusCode = (int)statusCode;
            response.ContentLength64 = 0;
            response.Close();
        }

        private static HttpStatusCode ResolveStatusCode(Exception exception)
        {
            return exception switch
            {
                NotImplementedException => HttpStatusCode.NotImplemented,
                _ => HttpStatusCode.BadRequest
            };
        }
    }
}
