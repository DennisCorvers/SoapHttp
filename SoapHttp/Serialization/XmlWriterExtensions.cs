using System.Xml;
using System.Xml.Serialization;

namespace SoapHttp.Serialization
{
    internal static class XmlWriterExtensions
    {
        private const string SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

        internal static async Task WriteEnvelope(this XmlWriter writer, string soapNamespacePrefix)
        {
            await writer.WriteStartElementAsync(soapNamespacePrefix, "Envelope", SoapNamespace);
        }

        internal static async Task WriteHeader(this XmlWriter writer, string soapNamespacePrefix, object? headerData)
        {
            await using var body = await writer.WriteSelfClosingElementAsync(soapNamespacePrefix, "Header", SoapNamespace);
            // Write header object(s) ?
        }

        internal static async Task WriteBody(this XmlWriter writer, string soapNamespacePrefix, object? bodyData)
        {
            await using var body = await writer.WriteSelfClosingElementAsync(soapNamespacePrefix, "Body", SoapNamespace);
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

            //await using var content = await writer.WriteSelfClosingElementAsync(null, "AssortimentSturingBericht", "http://schemas.opg.nl");
            // Write body object
            if (bodyData != null)
            {
                var root = new XmlRootAttribute()
                {
                    ElementName = "AssortimentSturingBericht",
                    Namespace = "http://schemas.opg.nl"
                };
                var xmlSerializer = new XmlSerializer(bodyData.GetType(), root: null);
                xmlSerializer.Serialize(writer, bodyData);
            }
        }

        internal static async Task WriteFault(this XmlWriter writer)
        {

        }

        internal static async Task<SelfClosingElement> WriteSelfClosingElementAsync(this XmlWriter writer, string? prefix, string localName, string? ns)
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
