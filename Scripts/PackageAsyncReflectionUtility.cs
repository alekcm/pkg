using System;
using System.Reflection;
using System.Threading.Tasks;

namespace MapEditorPrototype
{
    public static class PackageAsyncReflectionUtility
    {
        public static async Task<object> InvokeTaskMethodAsync(object target, MethodInfo method, params object[] args)
        {
            if (method == null)
            {
                return null;
            }

            object invocationResult = method.Invoke(target, args);
            if (invocationResult == null)
            {
                return null;
            }

            if (invocationResult is Task task)
            {
                await task;
                PropertyInfo resultProperty = invocationResult.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                return resultProperty != null ? resultProperty.GetValue(invocationResult) : null;
            }

            return invocationResult;
        }

        public static Type FindType(params string[] assemblyQualifiedNames)
        {
            for (int i = 0; i < assemblyQualifiedNames.Length; i++)
            {
                Type type = Type.GetType(assemblyQualifiedNames[i]);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
