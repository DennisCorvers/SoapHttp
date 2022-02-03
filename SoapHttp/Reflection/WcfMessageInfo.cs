using System.Reflection;

namespace SoapHttp.Reflection
{
    internal class WcfMessageInfo
    {
        internal bool IsWrapped { get; }
        internal Type MessageType { get; }
        internal Type ParameterType { get; }

        public WcfMessageInfo(Type messageType)
        {
            MessageType = messageType;

            // TODO: In case of the IsWrapped, use the request object instead for (de)serializing.
            var msgContract = MessageType.GetCustomAttribute<System.ServiceModel.MessageContractAttribute>();
            if (msgContract == null)
            {
                IsWrapped = true;
            }
            else
            {
                IsWrapped = msgContract.IsWrapped;
            }
            // Resolve types inside of request object.

            var fields = MessageType.GetFields();

            foreach (var field in fields)
            {
                var bodyMemberAttribute = field.GetCustomAttribute<System.ServiceModel.MessageBodyMemberAttribute>();
                if (bodyMemberAttribute == null)
                    continue;

                var wcfFieldInfo = new WcfFieldInfo(field, bodyMemberAttribute);
            }

            if (fields.Length != 1)
            {
                throw new InvalidOperationException("Only one parameter supported for request message.");
            }

            // TODO: Support for multiple parameters?
            ParameterType = fields[0].FieldType;
        }
    }
}
