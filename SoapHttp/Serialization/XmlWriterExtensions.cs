using System.Xml;
using System.Xml.Serialization;

namespace SoapHttp.Serialization
{
    internal static class XmlWriterExtensions
    {
        private const string SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

        internal static Task<SelfClosingElement> WriteEnvelope(this XmlWriter writer, string soapNamespacePrefix)
        {
            return writer.WriteSelfClosingElementAsync(soapNamespacePrefix, "Envelope", SoapNamespace);
        }

        internal static async Task WriteHeader(this XmlWriter writer, string soapNamespacePrefix, object? headerData)
        {
            await writer.WriteStartElementAsync(soapNamespacePrefix, "Header", SoapNamespace);

            // Write header objects?
            await writer.WriteEndElementAsync();
        }

        internal static async Task WriteBody(this XmlWriter writer, string soapNamespacePrefix, Func<XmlWriter, Task> bodyContentCallback)
        {
            await writer.WriteStartElementAsync(soapNamespacePrefix, "Body", SoapNamespace);
            await writer.WriteAttributeStringAsync(
                "xmlns",
                "xsi",
                null,
                "http://www.w3.org/2001/XMLSchema-instance"
                );

            await writer.WriteAttributeStringAsync(
                "xmlns",
                "xsd",
                null,
                "http://www.w3.org/2001/XMLSchema"
                );

            if (bodyContentCallback != null)
                await bodyContentCallback(writer);

            await writer.WriteEndElementAsync();
        }

        internal static Task WriteFault(this XmlWriter writer, string soapNamespacePrefix, Exception exception)
        {
            return WriteBody(writer, soapNamespacePrefix, (writer) => WriteFaultAsync(writer, soapNamespacePrefix, exception));

            static async Task WriteFaultAsync(XmlWriter writer, string soapNamespacePrefix, Exception exception)
            {
                await writer.WriteStartElementAsync(soapNamespacePrefix, "Fault", SoapNamespace);
                await writer.WriteElementStringAsync(null, "faultcode", null, exception.GetType().ToString());
                await writer.WriteElementStringAsync(null, "faultstring", null, exception.Message);
                await writer.WriteEndElementAsync();
            }
        }

        private static async Task<SelfClosingElement> WriteSelfClosingElementAsync(this XmlWriter writer, string? prefix, string localName, string? ns)
        {
            await writer.WriteStartElementAsync(prefix, localName, ns);
            return new SelfClosingElement(writer);
        }
    }

    internal struct SelfClosingElement : IAsyncDisposable
    {
        private readonly XmlWriter writer;

        internal SelfClosingElement(XmlWriter writer)
        {
            this.writer = writer;
        }

        public async ValueTask DisposeAsync()
        {
            await writer.WriteEndElementAsync();
        }
    }
}
