using SoapHttp.Reflection;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SoapHttp.Serialization
{
    internal class WcfSerializer
    {
        private const string SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
        private const string FaultCodeElementName = "faultcode";
        private const string FaultStringElementName = "faultstring";

        #region Serialize
        private readonly static XmlWriterSettings xmlWriterSettings = new()
        {
            CloseOutput = false,
            Async = true,
            WriteEndDocumentOnClose = true,
            Encoding = new UTF8Encoding(false)
        };

        public string SoapNamespacePrefix { get; set; } = "s";

        public Task Serialize(Stream stream, WcfMessageInfo messageInfo)
            => InnerSerialize(stream, null, messageInfo);

        public Task Serialize(Stream stream, object value, WcfMessageInfo messageInfo)
            => InnerSerialize(stream, value, messageInfo);

        private async Task InnerSerialize(Stream stream, object? value, WcfMessageInfo messageInfo)
        {
            using XmlWriter writer = XmlWriter.Create(stream, xmlWriterSettings);
            await using var envelope = await writer.WriteEnvelope(SoapNamespacePrefix);
            await writer.WriteHeader(SoapNamespacePrefix, null);
            await writer.WriteBody(SoapNamespacePrefix, (writer) =>
            {
                SerializeBodyContent(writer, value, messageInfo);
                return Task.CompletedTask;
            });
        }

        private static void SerializeBodyContent(XmlWriter writer, object? value, WcfMessageInfo messageInfo)
        {
            if (value == null)
                return;

            if (messageInfo.IsWrapped)
            {
                WriteBodyRootElement(writer, messageInfo.Name, messageInfo.Namespace, messageInfo.MessageType, value);
                return;
            }

            foreach (var field in messageInfo.Fields)
            {
                WriteBodyRootElement(writer, field.XmlName, field.XmlNamespace, field.PropertyType, field.GetValue(value));
            }

            static void WriteBodyRootElement(XmlWriter writer, string elementName, string? elementNamespace, Type dataType, object? bodyData)
            {
                var root = new XmlRootAttribute()
                {
                    ElementName = elementName,
                    Namespace = elementNamespace
                };
                var xmlSerializer = new XmlSerializer(dataType, root);
                xmlSerializer.Serialize(writer, bodyData);
            }
        }

        public Task SerializeException(Stream stream, Exception exception)
        {
            using XmlWriter writer = XmlWriter.Create(stream, xmlWriterSettings);
            return SerializeException(writer, exception);
        }

        private async Task SerializeException(XmlWriter writer, Exception exception)
        {
            await using var envelope = await writer.WriteEnvelope(SoapNamespacePrefix);
            await writer.WriteHeader(SoapNamespacePrefix, null);
            await writer.WriteFault(SoapNamespacePrefix, exception);
        }

        #endregion

        #region Deserialize
        private readonly static XmlReaderSettings xmlReaderSettings = new()
        {
            Async = true,
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
        #endregion
    }
}
