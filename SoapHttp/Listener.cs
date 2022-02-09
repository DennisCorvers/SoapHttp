using SoapHttp.Serialization;
using SoapHttp.Reflection;
using System.Net;
using SoapHttp.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace SoapHttp
{
    public class Listener : IDisposable
    {
        private readonly IReadOnlyDictionary<Uri, ServiceEndpoint> m_services;
        private readonly HttpListener m_listener;
        private bool m_isDisposed;

        public Listener(ServiceConfig serviceConfig)
            : this(new[] { serviceConfig })
        { }

        public Listener(ICollection<ServiceConfig> serviceConfigs)
        {
            var services = new Dictionary<Uri, ServiceEndpoint>();
            m_listener = new HttpListener();

            foreach (var serviceConfig in serviceConfigs)
            {
                var prefix = serviceConfig.UriPrefix;
                if (services.ContainsKey(prefix))
                    throw new ArgumentException($"Service with prefix of {prefix} already exists.");

                var resolver = new WcfMethodResolver(serviceConfig.Service.GetType());
                services.Add(prefix, new ServiceEndpoint(prefix, serviceConfig.Service, resolver));

                var listenerPrefix = prefix.GetLeftPart(UriPartial.Authority) + '/';
                m_listener.Prefixes.Add(listenerPrefix);
            }

            m_services = services;
        }

        ~Listener()
            => Dispose(false);

        public void Start(CancellationToken token)
        {
            if (m_listener.IsListening)
                throw new InvalidOperationException();

            if (m_isDisposed)
                throw new ObjectDisposedException(nameof(Listener));

            m_listener.Start();
            Task.Factory.StartNew(async () => await DoWork(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Stop()
            => m_listener.Stop();

        private async Task DoWork(CancellationToken token)
        {
            Console.WriteLine("Listener listening on the following prefixes:");
            foreach (var service in m_services.Values)
                Console.WriteLine(service.Prefix);

            try
            {
                while (m_listener.IsListening)
                {
                    var context = await m_listener.GetContextAsync();
                    // Handle request

                    try
                    {
                        if (!TryResolveServiceEndpoint(context, out ServiceEndpoint? service))
                        {
                            RespondWithEmpty(context.Response, HttpStatusCode.BadRequest);
                            continue;
                        }

                        var soapMethod = ResolveSoapAction(context.Request, service);
                        var responseObject = await HandleRequest(context.Request.InputStream, soapMethod, service);

                        if (soapMethod.HasReturnValue)
                            await RespondWithMessage(context.Response, responseObject, soapMethod.WcfResponseMessageInfo!);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error in handling request: {e}");
                        // Return fault.
                        await RespondWithException(context.Response, e);
                        continue;
                    }
                }
            }
            catch (HttpListenerException)
            {
                // Occurs when the HttpListener is closed while it's waiting for a message.
            }

            Console.WriteLine("Listener stopped listening.");
        }

        private bool TryResolveServiceEndpoint(HttpListenerContext context, [NotNullWhen(true)] out ServiceEndpoint? serviceEndpoint)
        {
            var url = context.Request.Url;
            if (url == null)
            {
                serviceEndpoint = null;
                return false;
            }

            return m_services.TryGetValue(url, out serviceEndpoint);
        }

        private static WcfMethodInfo ResolveSoapAction(HttpListenerRequest request, ServiceEndpoint serviceEndpoint)
        {
            var targetMethod = request.Headers.Get("soapAction");
            if (targetMethod == null)
                throw new InvalidOperationException("Soap message does not contain a soapAction.");

            if (!serviceEndpoint.ServiceResolver.TryResolve(targetMethod, out WcfMethodInfo? methodInfo))
                throw new InvalidOperationException($"Could not resolve the SoapAction: {targetMethod}.");

            return methodInfo;
        }

        private static async Task<object?> HandleRequest(Stream inputStream, WcfMethodInfo methodInfo, ServiceEndpoint serviceEndpoint)
        {
            if (methodInfo.HasParameters)
            {
                // Invoke with deserialized object.
                var obj = await WcfSerializer.Deserialize(inputStream, methodInfo.WcfRequestMessageInfo!)
                    ?? throw new InvalidOperationException("Could not deserialize Soap message.");

                return await methodInfo.InvokeSoapMethodAsync(obj, serviceEndpoint.Service);
            }
            else
            {
                return await methodInfo.InvokeSoapMethodAsync(serviceEndpoint.Service);
            }
        }

        private static async Task RespondWithMessage(HttpListenerResponse response, object? responseMessage, WcfMessageInfo messageInfo)
        {
            var serializer = new WcfSerializer();

            if (responseMessage == null)
                await serializer.Serialize(response.OutputStream, messageInfo);
            else
                await serializer.Serialize(response.OutputStream, responseMessage, messageInfo);

            response.StatusCode = (int)HttpStatusCode.OK;
            response.Close();
        }

        private static async Task RespondWithException(HttpListenerResponse response, Exception exception)
        {
            var serializer = new WcfSerializer();
            await serializer.SerializeException(response.OutputStream, exception);

            response.StatusCode = (int)ResolveStatusCode(exception);
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

        private static void RespondWithEmpty(HttpListenerResponse response, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            response.Headers.Clear();
            response.SendChunked = false;
            response.StatusCode = (int)statusCode;
            response.ContentLength64 = 0;
            response.Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_isDisposed)
            {
                if (m_listener.IsListening)
                {
                    m_listener.Stop();
                    m_listener.Close();
                }

                m_isDisposed = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
