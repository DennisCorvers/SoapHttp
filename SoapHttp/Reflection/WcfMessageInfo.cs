using System.Reflection;

namespace SoapHttp.Reflection
{
    internal class WcfMessageInfo
    {
        internal bool IsWrapped { get; }
        internal Type MessageType { get; }

        internal Utility.ExpressionCtor Constructor { get; }
        internal WcfFieldInfo[] Fields;

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
                Fields = Array.Empty<WcfFieldInfo>();

            Constructor = MakeConstructor(messageType, Fields);
        }

        private static Utility.ExpressionCtor MakeConstructor(Type messageType, WcfFieldInfo[] wcfFields)
        {
            var contructorInfo = messageType.GetConstructor(wcfFields.Select(x => x.PropertyType).ToArray());

            // Do we always have a constructor that accepts all members of the message?
            if (contructorInfo == null)
                throw new InvalidOperationException($"Constructor for type {messageType.Name} was not found with the following parameters {wcfFields}.");

            return Utility.GetConstructor(contructorInfo);
        }

        private static WcfFieldInfo[] CollectFields(Type messageType)
        {
            var fields = messageType.GetFields();

            var xmlFields = new List<WcfFieldInfo>(fields.Length);
            foreach (var field in fields)
            {
                var bodyMemberAttribute = field.GetCustomAttribute<System.ServiceModel.MessageBodyMemberAttribute>();
                if (bodyMemberAttribute == null)
                    continue;

                xmlFields.Add(new WcfFieldInfo(field, bodyMemberAttribute));
            }

            xmlFields.OrderBy(x => x.Order);
            return xmlFields.ToArray();
        }
    }
}
