using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace MapEditorPrototype
{
    public class RelayConnectionService : MonoBehaviour
    {
        [SerializeField] private bool preferSecureRelay = true;
        [SerializeField] private string preferredConnectionType = "dtls";

        public bool IsAvailable { get; private set; }
        public string LastError { get; private set; }

        private Type unityServicesType;
        private Type authenticationServiceType;
        private Type relayServiceType;

        private void Awake()
        {
            ResolveTypes();
        }

        public async Task<bool> EnsureReadyAsync()
        {
            ResolveTypes();
            if (!IsAvailable)
            {
                LastError = "Unity Services packages (Core/Auth/Relay) are not installed or not found.";
                return false;
            }

            try
            {
                await InitializeServicesAsync();
                await SignInAnonymouslyAsync();
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogWarning($"RelayConnectionService: {LastError}");
                return false;
            }
        }

        public async Task<RelayAllocationData> CreateRelayHostAsync(int maxConnections)
        {
            bool ready = await EnsureReadyAsync();
            if (!ready)
            {
                return null;
            }

            try
            {
                object relayInstance = relayServiceType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                MethodInfo createMethod = FindMethod(relayInstance.GetType(), "CreateAllocationAsync", 1, 4);
                object allocation = await PackageAsyncReflectionUtility.InvokeTaskMethodAsync(relayInstance, createMethod, BuildMethodArguments(createMethod, maxConnections));
                if (allocation == null)
                {
                    LastError = "Relay allocation creation returned null.";
                    return null;
                }

                object allocationId = GetPropertyValue(allocation, "AllocationId");
                MethodInfo getJoinCodeMethod = FindMethod(relayInstance.GetType(), "GetJoinCodeAsync", 1, 1);
                object joinCodeResult = await PackageAsyncReflectionUtility.InvokeTaskMethodAsync(relayInstance, getJoinCodeMethod, allocationId);
                string joinCode = joinCodeResult as string;

                RelayAllocationData data = ExtractRelayAllocationData(allocation, true);
                data.JoinCode = joinCode;
                LastError = string.Empty;
                return data;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogWarning($"RelayConnectionService.CreateRelayHostAsync: {LastError}");
                return null;
            }
        }

        public async Task<RelayAllocationData> JoinRelayAsync(string joinCode)
        {
            bool ready = await EnsureReadyAsync();
            if (!ready)
            {
                return null;
            }

            try
            {
                object relayInstance = relayServiceType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                MethodInfo joinMethod = FindMethod(relayInstance.GetType(), "JoinAllocationAsync", 1, 2);
                object joinAllocation = await PackageAsyncReflectionUtility.InvokeTaskMethodAsync(relayInstance, joinMethod, BuildMethodArguments(joinMethod, joinCode));
                if (joinAllocation == null)
                {
                    LastError = "Relay join allocation returned null.";
                    return null;
                }

                RelayAllocationData data = ExtractRelayAllocationData(joinAllocation, false);
                data.JoinCode = joinCode;
                LastError = string.Empty;
                return data;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogWarning($"RelayConnectionService.JoinRelayAsync: {LastError}");
                return null;
            }
        }

        private void ResolveTypes()
        {
            unityServicesType = PackageAsyncReflectionUtility.FindType(
                "Unity.Services.Core.UnityServices, Unity.Services.Core",
                "Unity.Services.Core.UnityServices, Unity.Services.Core.Internal");

            authenticationServiceType = PackageAsyncReflectionUtility.FindType(
                "Unity.Services.Authentication.AuthenticationService, Unity.Services.Authentication");

            relayServiceType = PackageAsyncReflectionUtility.FindType(
                "Unity.Services.Relay.RelayService, Unity.Services.Relay");

            IsAvailable = unityServicesType != null && authenticationServiceType != null && relayServiceType != null;
        }

        private async Task InitializeServicesAsync()
        {
            object servicesState = unityServicesType.GetProperty("State", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (servicesState != null && servicesState.ToString().Contains("Initialized"))
            {
                return;
            }

            MethodInfo initializeMethod = unityServicesType.GetMethod("InitializeAsync", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (initializeMethod == null)
            {
                throw new MissingMethodException("UnityServices.InitializeAsync() was not found.");
            }

            await PackageAsyncReflectionUtility.InvokeTaskMethodAsync(null, initializeMethod);
        }

        private async Task SignInAnonymouslyAsync()
        {
            object authInstance = authenticationServiceType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (authInstance == null)
            {
                throw new MissingMemberException("AuthenticationService.Instance was not found.");
            }

            object isSignedInValue = GetPropertyValue(authInstance, "IsSignedIn");
            if (isSignedInValue is bool isSignedIn && isSignedIn)
            {
                return;
            }

            MethodInfo signInMethod = authInstance.GetType().GetMethod("SignInAnonymouslyAsync", BindingFlags.Public | BindingFlags.Instance);
            if (signInMethod == null)
            {
                throw new MissingMethodException("AuthenticationService.SignInAnonymouslyAsync() was not found.");
            }

            await PackageAsyncReflectionUtility.InvokeTaskMethodAsync(authInstance, signInMethod);
        }

        private RelayAllocationData ExtractRelayAllocationData(object allocation, bool isHostAllocation)
        {
            RelayAllocationData data = new RelayAllocationData
            {
                AllocationIdBytes = GetPropertyValue(allocation, "AllocationIdBytes") as byte[],
                ConnectionData = GetPropertyValue(allocation, "ConnectionData") as byte[],
                HostConnectionData = isHostAllocation
                    ? GetPropertyValue(allocation, "ConnectionData") as byte[]
                    : GetPropertyValue(allocation, "HostConnectionData") as byte[],
                KeyBytes = GetPropertyValue(allocation, "Key") as byte[]
            };

            IEnumerable endpoints = GetPropertyValue(allocation, "ServerEndpoints") as IEnumerable;
            object selectedEndpoint = SelectEndpoint(endpoints);
            if (selectedEndpoint != null)
            {
                data.Host = Convert.ToString(GetPropertyValue(selectedEndpoint, "Host") ?? GetPropertyValue(selectedEndpoint, "IpV4"));
                object portValue = GetPropertyValue(selectedEndpoint, "Port");
                if (portValue != null)
                {
                    data.Port = Convert.ToUInt16(portValue);
                }

                string connectionType = Convert.ToString(GetPropertyValue(selectedEndpoint, "ConnectionType"));
                data.IsSecure = !string.IsNullOrWhiteSpace(connectionType)
                    ? connectionType.IndexOf("dtls", StringComparison.OrdinalIgnoreCase) >= 0 || connectionType.IndexOf("wss", StringComparison.OrdinalIgnoreCase) >= 0
                    : preferSecureRelay;
            }
            else
            {
                object relayServer = GetPropertyValue(allocation, "RelayServer");
                if (relayServer != null)
                {
                    data.Host = Convert.ToString(GetPropertyValue(relayServer, "IpV4") ?? GetPropertyValue(relayServer, "Host"));
                    object portValue = GetPropertyValue(relayServer, "Port");
                    if (portValue != null)
                    {
                        data.Port = Convert.ToUInt16(portValue);
                    }
                }
                data.IsSecure = preferSecureRelay;
            }

            return data;
        }

        private object SelectEndpoint(IEnumerable endpoints)
        {
            if (endpoints == null)
            {
                return null;
            }

            object fallback = null;
            foreach (object endpoint in endpoints)
            {
                if (endpoint == null)
                {
                    continue;
                }

                fallback ??= endpoint;
                string connectionType = Convert.ToString(GetPropertyValue(endpoint, "ConnectionType"));
                if (!string.IsNullOrWhiteSpace(connectionType) && string.Equals(connectionType, preferredConnectionType, StringComparison.OrdinalIgnoreCase))
                {
                    return endpoint;
                }
            }

            return fallback;
        }

        private object[] BuildMethodArguments(MethodInfo method, object firstArgument)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            if (parameters.Length > 0)
            {
                args[0] = firstArgument;
            }

            for (int i = 1; i < parameters.Length; i++)
            {
                args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : GetDefault(parameters[i].ParameterType);
            }

            return args;
        }

        private MethodInfo FindMethod(Type type, string name, int minParams, int maxParams)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != name)
                {
                    continue;
                }

                int paramCount = method.GetParameters().Length;
                if (paramCount >= minParams && paramCount <= maxParams)
                {
                    return method;
                }
            }

            throw new MissingMethodException($"Method {name} with {minParams}-{maxParams} parameters was not found on type {type.FullName}.");
        }

        private object GetPropertyValue(object target, string propertyName)
        {
            return target?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)?.GetValue(target);
        }

        private object GetDefault(Type type)
        {
            if (type == null || !type.IsValueType)
            {
                return null;
            }

            return Activator.CreateInstance(type);
        }
    }
}
