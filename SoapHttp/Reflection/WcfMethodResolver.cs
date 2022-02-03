using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SoapHttp.Reflection
{
    internal class WcfMethodResolver<T> where T : class
    {
        public int ServiceMethodCount
            => m_wcfMethodInfo.Count;

        private readonly Dictionary<string, WcfMethodInfo> m_wcfMethodInfo;

        public WcfMethodResolver()
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

        public bool TryResolve(string soapAction, [NotNullWhen(true)] out WcfMethodInfo? wcfMethodInfo)
        {
            return m_wcfMethodInfo.TryGetValue(soapAction, out wcfMethodInfo);
        }
    }
}
