using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace SoapHttp.Reflection
{
    internal static class Utility
    {
        public static Func<object[], object> GetAnonymousConstructor(ConstructorInfo ctor)
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
            return Expression.Lambda<Func<object[], object>>(newExp, param).Compile();
        }

        public static Func<object> GetEmptyConstructor(Type type)
        {
            var constructor = type.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException($"Type {type} has no defauly constructor.");

            NewExpression ctor = Expression.New(constructor);
            return Expression.Lambda<Func<object>>(ctor).Compile();
        }

        internal static Func<object, object?> CreateAnonymousGetter(this FieldInfo field)
        {
            string getterName = field.ReflectedType!.FullName + ".get_" + field.Name;
            DynamicMethod getterMethod = new(getterName, typeof(object), new Type[] { typeof(object) }, true);
            ILGenerator gen = getterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                if (field.DeclaringType != typeof(object))
                    gen.Emit(OpCodes.Castclass, field.DeclaringType!);
                gen.Emit(OpCodes.Ldfld, field);
            }
            if (field.FieldType.IsValueType)
                gen.Emit(OpCodes.Box, field.FieldType);
            gen.Emit(OpCodes.Ret);
            return (Func<object, object?>)getterMethod.CreateDelegate(typeof(Func<object, object?>));
        }

        internal static Action<object, object> CreateAnonymousSetter(this FieldInfo field)
        {
            string setterName = field.ReflectedType!.FullName + ".set_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(setterName, null, new Type[] { typeof(object), typeof(object) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                if (field.DeclaringType != typeof(object))
                    gen.Emit(OpCodes.Castclass, field.DeclaringType!);
                if (field.FieldType.IsValueType)
                    gen.Emit(OpCodes.Unbox_Any, field.FieldType);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, field);
            }
            gen.Emit(OpCodes.Ret);
            return (Action<object, object>)setterMethod.CreateDelegate(typeof(Action<object, object>));
        }

        public static Func<object, object?> CreateAnonymousGetter(this PropertyInfo propertyInfo)
        {
            var t = typeof(object);
            var propertyType = typeof(object);
            ParameterExpression paramExpression = Expression.Parameter(t, "value");
            Expression getArg = propertyInfo.DeclaringType == t ? paramExpression : Expression.Convert(paramExpression, propertyInfo.DeclaringType!);
            Expression propertyGetterExpression = Expression.Property(getArg, propertyInfo);

            if (propertyType != propertyInfo.PropertyType)
                propertyGetterExpression = Expression.Convert(propertyGetterExpression, propertyType);

            return Expression.Lambda<Func<object, object>>(propertyGetterExpression, paramExpression).Compile();
        }

        public static Action<object, object> CreateAnonymousSetter(this PropertyInfo propertyInfo)
        {
            var t = typeof(object);
            var propertyType = typeof(object);
            ParameterExpression paramExpression = Expression.Parameter(t);
            ParameterExpression paramExpression2 = Expression.Parameter(propertyType);

            Expression setInst = propertyInfo.DeclaringType == t ? paramExpression : Expression.Convert(paramExpression, propertyInfo.DeclaringType!);
            Expression setVal = propertyInfo.PropertyType == propertyType ? paramExpression2 : Expression.Convert(paramExpression2, propertyInfo.PropertyType);

            MemberExpression propertySetterExpression = Expression.Property(setInst, propertyInfo);
            return Expression.Lambda<Action<object, object>>(Expression.Assign(propertySetterExpression, setVal), paramExpression, paramExpression2).Compile();
        }
    }
}
