using System.Reflection;

namespace SoapHttp.Reflection
{
    internal class WcfMessageInfo
    {
        internal bool IsWrapped { get; }
        internal Type MessageType { get; }

        internal Func<object> Constructor { get; }
        internal WcfMemberInfo[] Fields;

        public WcfMessageInfo(Type messageType)
        {
            MessageType = messageType;

            var msgContract = MessageType.GetCustomAttribute<System.ServiceModel.MessageContractAttribute>();
            if (msgContract == null)
                IsWrapped = true;
            else
                IsWrapped = msgContract.IsWrapped;

            // The WCFMessage is wrapped. We need to make a constructor and gather appropriate 
            // info of the fields in order to (de)serialize the message properly.
            if (!IsWrapped)
                Fields = CollectFields(messageType);
            else
                Fields = Array.Empty<WcfMemberInfo>();

            // Always assume the Type has an empty (default) constructor
            Constructor = Utility.GetEmptyConstructor(messageType);
        }

        public object ConstructMessage(object[] parameters)
        {
            if (IsWrapped)
                throw new InvalidOperationException("Only non-wrapped messages can be created.");

            if (parameters.Length > Fields.Length)
                throw new InvalidOperationException("Too many parameters were supplied.");

            var message = Constructor();
            // Parameters NEEDS to be in the same order as WcfMemberInfo!
            for (int i = 0; i < parameters.Length; i++)
                Fields[i].SetValue(message, parameters[i]);

            return message;
        }

        private static WcfMemberInfo[] CollectFields(Type messageType)
        {
            var fields = messageType.GetFields();
            var properties = messageType.GetProperties();

            var xmlFields = new List<WcfMemberInfo>();

            // Collect fields
            foreach (var field in fields)
            {
                var bodyMemberAttribute = field.GetCustomAttribute<System.ServiceModel.MessageBodyMemberAttribute>();
                if (bodyMemberAttribute == null)
                    continue;

                xmlFields.Add(new WcfMemberInfo(field, bodyMemberAttribute));
            }

            // Collect properties
            foreach (var property in properties)
            {
                var bodyMemberAttribute = property.GetCustomAttribute<System.ServiceModel.MessageBodyMemberAttribute>();
                if (bodyMemberAttribute == null)
                    continue;

                xmlFields.Add(new WcfMemberInfo(property, bodyMemberAttribute));
            }

            xmlFields.OrderBy(x => x.Order).ThenBy(x => x.XmlName);
            return xmlFields.ToArray();
        }
    }
}
