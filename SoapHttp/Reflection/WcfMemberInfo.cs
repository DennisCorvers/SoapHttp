using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SoapHttp.Reflection
{
    internal class WcfMemberInfo
    {
        public string XmlName
        { get; private set; }
        public string XmlNamespace
        { get; private set; }
        public Type PropertyType
        { get; }
        public int Order
        { get; private set; }

        private readonly Action<object, object> m_setter;
        private readonly Func<object, object?> m_getter;

        public WcfMemberInfo(FieldInfo fieldInfo, System.ServiceModel.MessageBodyMemberAttribute memberAttribute)
        {
            PropertyType = fieldInfo.FieldType;
            SetAttributeInfo(fieldInfo.Name, memberAttribute);
            m_setter = fieldInfo.CreateAnonymousSetter();
            m_getter = fieldInfo.CreateAnonymousGetter();
        }

        public WcfMemberInfo(PropertyInfo propertyInfo, System.ServiceModel.MessageBodyMemberAttribute memberAttribute)
        {
            PropertyType = propertyInfo.PropertyType;
            SetAttributeInfo(propertyInfo.Name, memberAttribute);
            m_setter = propertyInfo.CreateAnonymousSetter();
            m_getter = propertyInfo.CreateAnonymousGetter();
        }

        [MemberNotNull(nameof(XmlName), nameof(XmlNamespace))]
        private void SetAttributeInfo(string name, System.ServiceModel.MessageBodyMemberAttribute memberAttribute)
        {
            Order = memberAttribute.Order;
            XmlNamespace = memberAttribute.Namespace;
            XmlName = string.IsNullOrEmpty(memberAttribute.Name) ? name : memberAttribute.Name;
        }

        public void SetValue(object typeValue, object memberValue) 
            => m_setter(typeValue, memberValue);

        public object? GetValue(object typeValue) 
            => m_getter(typeValue);
    }
}
