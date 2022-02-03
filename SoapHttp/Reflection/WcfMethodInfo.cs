using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SoapHttp.Reflection
{
    internal class WcfMethodInfo
    {
        public bool HasParameters
            => WcfRequestMessageInfo != null;
        public bool IsAsync 
        { get; }
        public bool HasReturnValue
            => WcfResponseMessageInfo != null;
        public string SoapActionName
        { get; }

        public WcfMessageInfo? WcfRequestMessageInfo { get; }
        public WcfMessageInfo? WcfResponseMessageInfo { get; }

        private readonly MethodInfo m_methodInfo;

        public WcfMethodInfo(MethodInfo methodInfo, System.ServiceModel.OperationContractAttribute operationContract)
        {
            m_methodInfo = methodInfo;
            SoapActionName = operationContract.Action;

            var returnType = methodInfo.ReturnType;

            var isTask = returnType == typeof(Task);
            var isGenericTask = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>);

            if (isTask || isGenericTask)
            {
                SoapActionName += "Async";
                IsAsync = true;

                // Grab returntype from Task<>
                if (isGenericTask)
                    returnType = returnType.GenericTypeArguments[0];

                // Transform returntype from Task to void
                else if (isTask)
                    returnType = typeof(void);
            }

            var requestParam = methodInfo.GetParameters().FirstOrDefault();
            if (requestParam != null)
                WcfRequestMessageInfo = new WcfMessageInfo(requestParam.ParameterType);

            if (returnType != typeof(void))
                WcfResponseMessageInfo = new WcfMessageInfo(returnType);
        }

        public bool TryGetParameterType([NotNullWhen(true)] out Type? value)
        {
            if (WcfRequestMessageInfo == null)
            {
                value = default;
                return false;
            }

            value = WcfRequestMessageInfo.ParameterType;
            return true;
        }

        internal async Task<object?> InvokeSoapMethodAsync(object soapObject, object target)
        {
            if (WcfRequestMessageInfo == null)
                throw new InvalidOperationException("SoapAction does not contain parameters.");

            var requestObject = Activator.CreateInstance(WcfRequestMessageInfo.MessageType, soapObject);

            if (!IsAsync)
            {
                return m_methodInfo.Invoke(target, new[] { requestObject });
            }
            else
            {
                var task = m_methodInfo.Invoke(target, new[] { requestObject })!;
                return await (Task<object?>)task;
            }
        }

        internal async Task<object?> InvokeSoapMethodAsync(object target)
        {
            if (!IsAsync)
            {
                return m_methodInfo.Invoke(target, null);
            }
            else
            {
                var task = m_methodInfo.Invoke(target, null)!;
                return await (Task<object?>)task;
            }
        }
    }
}
