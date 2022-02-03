using SoapHttp.Serialization;
using SoapHttp.Reflection;
using System.Net;

namespace SoapHttp
{
    public class Listener<T> : IDisposable where T : class
    {
        private readonly T m_service;
        private readonly HttpListener m_listener;
        private readonly ICollection<string> m_prefixes;
        private readonly WcfMethodResolver<T> m_wcfMethodResolver;
        private bool m_isDisposed;

        public Listener(string prefix, T service)
            : this(new List<string>(1) { prefix }, service)
        { }

        public Listener(ICollection<string> uriPrefixes, T service)
        {
            m_service = service;
            m_wcfMethodResolver = new WcfMethodResolver<T>();
            m_listener = new HttpListener();
            m_prefixes = uriPrefixes;
            foreach (var prefix in uriPrefixes)
                m_listener.Prefixes.Add(prefix);
        }

        ~Listener()
            => Dispose(false);

        public void Start(CancellationToken token)
        {
            if (m_listener.IsListening)
                throw new InvalidOperationException();

            if (m_isDisposed)
                throw new ObjectDisposedException(nameof(Listener<T>));

            m_listener.Start();
            Task.Factory.StartNew(async () => await DoWork(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Stop()
            => m_listener.Stop();

        private async Task DoWork(CancellationToken token)
        {
            Console.WriteLine("Listener listening on the following prefixes:");
            foreach (var prefix in m_prefixes)
                Console.WriteLine(prefix);

            try
            {
                while (m_listener.IsListening)
                {
                    var context = await m_listener.GetContextAsync();
                    // Handle request

                    try
                    {
                        var soapMethod = ResolveSoapAction(context.Request.Headers);
                        var response = await HandleRequest(context.Request, soapMethod);

                        await RespondWithMessage(context.Response, response, soapMethod);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error in handling request: {e}");
                        // Return fault.
                        RespondWithException(context.Response, e);
                        continue;
                    }

                    // Respond
                    RespondWithEmpty(context.Response);
                }
            }
            catch (HttpListenerException)
            {
                // Occurs when the HttpListener is closed while it's waiting for a message.
            }

            Console.WriteLine("Listener stopped listening.");
        }

        private WcfMethodInfo ResolveSoapAction(System.Collections.Specialized.NameValueCollection headers)
        {
            var targetMethod = headers.Get("soapAction");
            if (targetMethod == null)
                throw new InvalidOperationException("Soap message does not contain a soapAction.");

            if (!m_wcfMethodResolver.TryResolve(targetMethod, out WcfMethodInfo? methodInfo))
                throw new InvalidOperationException($"Could not resolve the SoapAction: {targetMethod}.");

            return methodInfo;
        }

        private async Task<object?> HandleRequest(HttpListenerRequest request, WcfMethodInfo methodInfo)
        {
            if (methodInfo.TryGetParameterType(out Type? type))
            {
                // Invoke with deserialized object.
                var obj = await SoapSerializer.Deserialize(request.InputStream, type)
                    ?? throw new InvalidOperationException("Could not deserialize Soap message.");

                return await methodInfo.InvokeSoapMethodAsync(obj, m_service);
            }
            else
            {
                return await methodInfo.InvokeSoapMethodAsync(m_service);
            }
        }

        private static async Task RespondWithMessage(HttpListenerResponse response, object? responseMessage, WcfMethodInfo methodInfo)
        {

        }

        private static void RespondWithException(HttpListenerResponse response, Exception exception)
        {

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
