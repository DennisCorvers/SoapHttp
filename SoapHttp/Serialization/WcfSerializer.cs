using SoapHttp.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SoapHttp.Serialization
{
    internal class WcfSerializer
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

        public static Task<object?> Deserialize(Stream stream, WcfMessageInfo messageInfo)
        {
            var xmlStream = XmlReader.Create(stream, xmlReaderSettings);
            return Deserialize(xmlStream, messageInfo);
        }

        private static async Task<object?> Deserialize(XmlReader reader, WcfMessageInfo messageInfo)
        {
            if (!await reader.TryMoveToBody())
                throw new InvalidDataException("No body element is contained in the specified SOAP message.");

            if (messageInfo.IsWrapped)
                return await DeserializeWrapped(reader, messageInfo.MessageType, messageInfo.Namespace);
            else
                return await DeserializeNonWrapped(reader, messageInfo);
        }

        private static async Task<object> DeserializeNonWrapped(XmlReader reader, WcfMessageInfo messageInfo)
        {
            int index = 0;
            var obj = messageInfo.Constructor();
            while (await reader.ReadAsync())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                var currentField = messageInfo.Fields[index];

                // Move through all fields in order
                while (index < messageInfo.Fields.Length)
                {
                    // Handle fault in body.
                    if (reader.NamespaceURI == SoapNamespace && reader.LocalName == "Fault")
                    {
                        await HandleFault(reader);
                        break;
                    }
                    else if (reader.NamespaceURI == currentField.XmlNamespace && reader.LocalName == currentField.XmlName)
                    {
                        var root = new XmlRootAttribute()
                        {
                            ElementName = currentField.XmlName,
                            Namespace = currentField.XmlNamespace
                        };

                        var parameter = new XmlSerializer(currentField.PropertyType, root).Deserialize(reader);
                        currentField.SetValue(obj, parameter);
                        index++;
                        break;
                    }

                    index++;
                }
            }

            return obj;
        }

        private static async Task<object?> DeserializeWrapped(XmlReader reader, Type type, string? xmlNamespace)
        {
            while (await reader.ReadAsync())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                if (reader.NamespaceURI == SoapNamespace && reader.LocalName == "Fault")
                {
                    await HandleFault(reader);
                }
                else if (reader.NamespaceURI == xmlNamespace)
                {
                    var root = new XmlRootAttribute()
                    {
                        ElementName = reader.LocalName,
                        Namespace = xmlNamespace
                    };
                    return new XmlSerializer(type, root).Deserialize(reader);
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
