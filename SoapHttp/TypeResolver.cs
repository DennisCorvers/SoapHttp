using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SoapHttp
{
    internal class TypeResolver<T> where T : class
    {
        public int ServiceMethodCount
            => m_wcfMethodInfo.Count;

        private Dictionary<string, WcfMethodInfo> m_wcfMethodInfo;

        public TypeResolver()
        {
            if (!TryResolveServiceContract(typeof(T), out Type? serviceContractType))
                throw new ArgumentException($"Type {typeof(T)} does not implement a valid service contract.");

            m_wcfMethodInfo = new();
            foreach (var method in serviceContractType.GetMethods())
                ResolveSoapAction(method);
        }

        private void ResolveSoapAction(MethodInfo method)
        {
            var soapActionAttribute = method.GetCustomAttribute<System.ServiceModel.OperationContractAttribute>();
            // Is not a SoapAction
            if (soapActionAttribute == null)
                return;

            var wcfMethodInfo = new WcfMethodInfo(method, soapActionAttribute);
            m_wcfMethodInfo.Add(wcfMethodInfo.SoapActionName, wcfMethodInfo);
        }

        private bool TryResolveServiceContract(Type type, [NotNullWhen(true)] out Type? serviceContractType)
        {
            if (type.GetCustomAttribute<System.ServiceModel.ServiceContractAttribute>() != null)
            {
                serviceContractType = type;
                return true;
            }
            else
            {
                foreach (var baseType in type.GetInterfaces())
                {
                    if (TryResolveServiceContract(baseType, out serviceContractType))
                        return true;
                }
            }

            serviceContractType = null;
            return false;
        }

        public bool TryResolveType(string soapAction, [NotNullWhen(true)] out WcfMethodInfo? wcfMethodInfo)
        {
            return m_wcfMethodInfo.TryGetValue(soapAction, out wcfMethodInfo);
        }
    }

    internal class WcfMethodInfo
    {
        public bool HasParameters
            => m_requestObjectType != null;
        public string SoapActionName { get; }
        public bool IsAsync { get; }
        public Type? ParameterType { get; }

        private MethodInfo m_methodInfo;
        private Type? m_requestObjectType;

        public WcfMethodInfo(MethodInfo methodInfo, System.ServiceModel.OperationContractAttribute operationContract)
        {
            m_methodInfo = methodInfo;

            // Resolve method naming.
            SoapActionName = operationContract.Action;
            var returnType = methodInfo.ReturnType;
            if (returnType == typeof(Task) ||
                (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                SoapActionName += "Async";
                IsAsync = true;
            }

            // Resolve method parameters.
            var requestParam = methodInfo.GetParameters().FirstOrDefault();
            if (requestParam == null)
                return;

            // Resolve types inside of request object.
            m_requestObjectType = requestParam.ParameterType;
            var fields = m_requestObjectType.GetFields();

            if (fields.Length > 0)
            {
                if (fields.Length > 1)
                    throw new InvalidOperationException("Multiple parameters not supported for request type.");

                ParameterType = fields[0].FieldType;
            }
        }

        public bool TryGetParameterType([NotNullWhen(true)] out Type? value)
        {
            if (ParameterType == null)
            {
                value = default;
                return false;
            }

            value = ParameterType;
            return true;
        }

        internal object? InvokeSoapMethod(object soapObject, object target)
        {
            if (m_requestObjectType == null)
                throw new InvalidOperationException("SoapAction does not contain parameters.");

            var requestObject = Activator.CreateInstance(m_requestObjectType, soapObject);
            return m_methodInfo.Invoke(target, new[] { requestObject });
        }

        internal object? InvokeSoapMethod(object target)
        {
            return m_methodInfo.Invoke(target, null);
        }
    }
}
