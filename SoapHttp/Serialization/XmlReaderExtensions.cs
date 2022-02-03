using System.Xml;

namespace SoapHttp.Serialization
{
    internal static class XmlReaderExtensions
    {
        internal static async Task<bool> TryMoveToNextElement(this XmlReader reader)
        {
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == "http://schemas.xmlsoap.org/soap/envelope/")
                    return true;
            }

            return false;
        }

        internal static async Task<bool> TryMoveToHeader(this XmlReader reader)
        {
            while (await reader.TryMoveToNextElement())
            {
                if (reader.LocalName == "Header")
                    return true;
                if (reader.LocalName == "Body")
                    return false;
            }

            return false;
        }

        internal static async Task<bool> TryMoveToBody(this XmlReader reader)
        {
            while (await reader.TryMoveToNextElement())
            {
                if (reader.LocalName == "Body")
                    return true;
            }

            return false;
        }
    }
}
