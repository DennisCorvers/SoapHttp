using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SoapHttp.Serialization
{
    public class SoapSerializer
    {
        private const string SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
        private const string FaultCodeElementName = "faultcode";
        private const string FaultStringElementName = "faultstring";

        private readonly static XmlReaderSettings xmlReaderSettings = new()
        {
            Async = true,
        };

        private readonly static XmlWriterSettings xmlWriterSettings = new()
        {
            CloseOutput = false,
            Async = true,
            WriteEndDocumentOnClose = true,
            Encoding = new UTF8Encoding(false)
        };

        public string SoapNamespacePrefix { get; set; } = "s";

        public SoapSerializer()
        {

        }

        public Task Serialize(Stream stream)
            => Serialize(stream, null!);

        public async Task Serialize(Stream stream, object value)
        {
            using XmlWriter writer = XmlWriter.Create(stream, xmlWriterSettings);
            await writer.WriteEnvelope(SoapNamespacePrefix);
            await writer.WriteHeader(SoapNamespacePrefix, null);
            await writer.WriteBody(SoapNamespacePrefix, value);

            // Write closing element for the envelope.
            await writer.WriteEndElementAsync();
        }

        public async Task SerializeException(Stream stream, Exception exception)
        {
            using XmlWriter writer = XmlWriter.Create(stream, xmlWriterSettings);
            //await WriteDocumentTop(writer);

            await writer.WriteStartElementAsync(SoapNamespacePrefix, "Fault", SoapNamespace);
            await writer.WriteElementStringAsync(null, FaultCodeElementName, null, exception.GetType().ToString());
            await writer.WriteElementStringAsync(null, FaultStringElementName, null, exception.Message);
            await writer.WriteEndElementAsync();

            await WriteDocumentBottom(writer);
        }


        private async Task WriteDocumentBottom(XmlWriter writer)
        {
            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
        }

        public static Task<object?> Deserialize(Stream stream, Type type)
        {
            var xmlStream = XmlReader.Create(stream, xmlReaderSettings);
            return Deserialize(xmlStream, type);
        }

        private static async Task<object?> Deserialize(XmlReader reader, Type type)
        {
            if (!await reader.TryMoveToBody())
                throw new InvalidDataException("No body element is contained in the specified SOAP message.");

            XmlRootAttribute? rootAttribute = type.GetCustomAttribute<XmlRootAttribute>(true);
            XmlTypeAttribute? typeAttribute = type.GetCustomAttribute<XmlTypeAttribute>(true);
            // string rootName = rootAttribute?.ElementName ?? typeAttribute?.TypeName ?? type.Name;
            string? rootNamespace = rootAttribute?.Namespace ?? typeAttribute?.Namespace;

            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.NamespaceURI == SoapNamespace && reader.LocalName == "Fault")
                    {
                        await HandleFault(reader);
                    }
                    else if (reader.NamespaceURI == rootNamespace)
                    {
                        var root = new XmlRootAttribute()
                        {
                            ElementName = reader.LocalName,
                            Namespace = rootNamespace
                        };
                        return new XmlSerializer(type, root).Deserialize(reader);
                    }
                }
            }

            throw new InvalidDataException("Could not parse Soap message.");
        }

        private static async Task HandleFault(XmlReader reader)
        {
            string? faultCode = null;
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element)
                    switch (reader.LocalName)
                    {
                        case FaultCodeElementName:
                            await reader.ReadAsync();
                            faultCode = await reader.ReadContentAsStringAsync();
                            break;

                        case FaultStringElementName:
                            await reader.ReadAsync();
                            throw new ProtocolViolationException($"The following error was received: {(faultCode == null ? string.Empty : $"{faultCode}: ")}{await reader.ReadContentAsStringAsync()}");
                    }
            }
        }
    }
}

