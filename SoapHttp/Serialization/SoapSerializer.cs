using System.Net;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace SoapHttp.Serialization
{
    internal class SoapSerializer
    {
        private const string SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
        private const string SoapBodyElementName = "Body";
        private const string SoapFaultElementName = "Fault";
        private const string FaultCodeElementName = "faultcode";
        private const string FaultStringElementName = "faultstring";
        private const string SoapEnvelopeElementName = "Envelope";

        internal static async Task<object?> Deserialize(XmlReader reader, Type type)
        {
            bool bodyInitialized = await TryMoveToBody(reader);

            if (!bodyInitialized)
                throw new InvalidDataException("No body element is contained in the specified SOAP message.");

            XmlRootAttribute? rootAttribute = type.GetCustomAttribute<XmlRootAttribute>(true);
            XmlTypeAttribute? typeAttribute = type.GetCustomAttribute<XmlTypeAttribute>(true);
            string rootName = rootAttribute?.ElementName ?? typeAttribute?.TypeName ?? type.Name;
            string? rootNamespace = rootAttribute?.Namespace ?? typeAttribute?.Namespace;

            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.NamespaceURI == SoapNamespace && reader.LocalName == SoapFaultElementName)
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

        private static async Task<bool> TryMoveToBody(XmlReader reader)
        {
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == SoapNamespace && reader.LocalName == SoapEnvelopeElementName)
                {
                    while (await reader.ReadAsync())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == SoapNamespace && reader.LocalName == SoapBodyElementName)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
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

