using SoapHttp.Serialization;
using System.Net;
using System.Reflection;
using System.Xml;

namespace SoapHttp
{
    public class Listener<T> : IDisposable where T : class
    {
        private readonly T m_service;
        private readonly HttpListener m_listener;
        private readonly ICollection<string> m_prefixes;
        private readonly TypeResolver<T> m_typeResolver;
        private bool m_isDisposed;

        public Listener(string prefix, T service)
            : this(new List<string>(1) { prefix }, service)
        { }

        public Listener(ICollection<string> uriPrefixes, T service)
        {
            m_service = service;
            m_typeResolver = new TypeResolver<T>();
            m_listener = new HttpListener();
            m_prefixes = uriPrefixes;
            foreach (var prefix in uriPrefixes)
                m_listener.Prefixes.Add(prefix);
        }

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
                        var response = await ParseRequest(context.Request);
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



        private async Task<object?> ParseRequest(HttpListenerRequest request)
        {
            // Validate request?
            var targetMethod = request.Headers.Get("soapAction");
            if (targetMethod == null)
                throw new InvalidOperationException("Soap message does not contain a soapAction.");

            if (!m_typeResolver.TryResolveType(targetMethod, out WcfMethodInfo? methodInfo))
                throw new InvalidOperationException($"Could not resolve the SoapAction: {targetMethod}.");

            if (methodInfo.TryGetParameterType(out Type? type))
            {
                // Invoke with deserialized object.
                var obj = await SoapSerializer.Deserialize(request.InputStream, type)
                    ?? throw new InvalidOperationException("Could not deserialize Soap message.");

                return methodInfo.InvokeSoapMethod(obj, m_service);
            }
            else
            {
                return methodInfo.InvokeSoapMethod(m_service);
            }
        }

        private static void RespondWithException(HttpListenerResponse response, Exception exception)
        {
            response.Headers.Clear();
            response.SendChunked = false;
            response.StatusCode = 400;
            response.ContentLength64 = 0;
            response.Close();
        }

        private static void RespondWithObject(HttpListenerResponse response, object responseObject)
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
                if (disposing)
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
