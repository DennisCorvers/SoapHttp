namespace SoapHttp.Hosting
{
    public class ServiceConfig
    {
        public Uri UriPrefix { get; }
        internal object Service { get; }

        private ServiceConfig(Uri uri, object service)
        {
            UriPrefix = uri;
            Service = service;
        }

        public static ServiceConfig Create<T>(string uriPrefix, T service) where T : class
        {
            return new ServiceConfig(new Uri(uriPrefix), service);
        }

        public static ServiceConfig Create<T>(Uri uriPrefix, T service) where T : class
        {
            return new ServiceConfig(uriPrefix, service);
        }
    }
}
