using System.Reflection;

namespace SoapHttp.Reflection
{
    internal class WcfFieldInfo
    {
        public string XmlName
        { get; }
        public string XmlNamespace
        { get; }
        public Type PropertyType
        { get; }
        public int Order
        { get; }

        public WcfFieldInfo(FieldInfo fieldInfo, System.ServiceModel.MessageBodyMemberAttribute memberAttribute)
        {
            PropertyType = fieldInfo.FieldType;
            Order = memberAttribute.Order;
            XmlNamespace = memberAttribute.Namespace;
            XmlName = string.IsNullOrEmpty(memberAttribute.Name) ? fieldInfo.Name : memberAttribute.Name;
        }
    }
}
