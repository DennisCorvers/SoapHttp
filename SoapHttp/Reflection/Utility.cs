using System.Linq.Expressions;
using System.Reflection;

namespace SoapHttp.Reflection
{
    internal static class Utility
    {
        public delegate object ExpressionCtor(params object[] args);
        public static ExpressionCtor GetConstructor(ConstructorInfo ctor)
        {
            // Source: https://rogerjohansson.blog/2008/02/28/linq-expressions-creating-objects/
            ParameterInfo[] paramsInfo = ctor.GetParameters();

            ParameterExpression param = Expression.Parameter(typeof(object[]), "args");
            Expression[] argsExp = new Expression[paramsInfo.Length];

            for (int i = 0; i < paramsInfo.Length; i++)
            {
                Expression index = Expression.Constant(i);
                Type paramType = paramsInfo[i].ParameterType;

                Expression paramAccessorExp = Expression.ArrayIndex(param, index);
                Expression paramCastExp = Expression.Convert(paramAccessorExp, paramType);

                argsExp[i] = paramCastExp;
            }

            NewExpression newExp = Expression.New(ctor, argsExp);
            LambdaExpression lambda = Expression.Lambda(typeof(ExpressionCtor), newExp, param);

            return (ExpressionCtor)lambda.Compile();
        }
    }
}
