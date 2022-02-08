using SoapHttp.Reflection;

namespace SoapHttp.Hosting
{
    internal record class ServiceEndpoint(Uri Prefix, object Service, WcfMethodResolver ServiceResolver);
}
