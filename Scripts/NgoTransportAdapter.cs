using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MapEditorPrototype
{
    public class NgoTransportAdapter : MonoBehaviour
    {
        [SerializeField] private GameObject networkManagerObject;

        public bool IsAvailable => ResolveNetworkManager() != null && ResolveTransport() != null;

        public bool StartHostWithRelay(RelayAllocationData relayData)
        {
            if (relayData == null || !ConfigureRelayAsHost(relayData))
            {
                return false;
            }

            return InvokeNetworkManagerBoolMethod("StartHost");
        }

        public bool StartClientWithRelay(RelayAllocationData relayData)
        {
            if (relayData == null || !ConfigureRelayAsClient(relayData))
            {
                return false;
            }

            return InvokeNetworkManagerBoolMethod("StartClient");
        }

        public bool StartHostLan(ushort port, string listenAddress = "0.0.0.0")
        {
            if (!ConfigureLanAsHost(port, listenAddress))
            {
                return false;
            }

            return InvokeNetworkManagerBoolMethod("StartHost");
        }

        public bool StartClientLan(string address, ushort port)
        {
            if (!ConfigureLanAsClient(address, port))
            {
                return false;
            }

            return InvokeNetworkManagerBoolMethod("StartClient");
        }

        public void Shutdown()
        {
            object networkManager = ResolveNetworkManager();
            networkManager?.GetType().GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Instance)?.Invoke(networkManager, null);
        }

        public ulong GetServerClientId()
        {
            object networkManager = ResolveNetworkManager();
            object value = networkManager?.GetType().GetProperty("ServerClientId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)?.GetValue(networkManager);
            return value != null ? Convert.ToUInt64(value) : 0UL;
        }

        public List<ulong> GetConnectedClientIds(bool includeServer = false)
        {
            List<ulong> results = new List<ulong>();
            object networkManager = ResolveNetworkManager();
            if (networkManager == null)
            {
                return results;
            }

            object collection = networkManager.GetType().GetProperty("ConnectedClientsIds", BindingFlags.Public | BindingFlags.Instance)?.GetValue(networkManager);
            if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    ulong clientId = Convert.ToUInt64(item);
                    if (includeServer || clientId != GetServerClientId())
                    {
                        results.Add(clientId);
                    }
                }
            }

            return results;
        }

        private bool ConfigureRelayAsHost(RelayAllocationData data)
        {
            object transport = ResolveTransport();
            if (transport == null)
            {
                return false;
            }

            MethodInfo method = transport.GetType().GetMethod("SetHostRelayData", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(transport, new object[]
                {
                    data.Host,
                    data.Port,
                    data.AllocationIdBytes,
                    data.KeyBytes,
                    data.ConnectionData,
                    data.IsSecure
                });
                return true;
            }

            MethodInfo fallback = transport.GetType().GetMethod("SetRelayServerData", BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(string), typeof(ushort), typeof(byte[]), typeof(byte[]), typeof(byte[]), typeof(byte[]), typeof(bool) }, null);
            if (fallback != null)
            {
                fallback.Invoke(transport, new object[]
                {
                    data.Host,
                    data.Port,
                    data.AllocationIdBytes,
                    data.KeyBytes,
                    data.ConnectionData,
                    data.HostConnectionData,
                    data.IsSecure
                });
                return true;
            }

            Debug.LogWarning("NgoTransportAdapter: no relay host transport configuration method was found.");
            return false;
        }

        private bool ConfigureRelayAsClient(RelayAllocationData data)
        {
            object transport = ResolveTransport();
            if (transport == null)
            {
                return false;
            }

            MethodInfo method = transport.GetType().GetMethod("SetClientRelayData", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(transport, new object[]
                {
                    data.Host,
                    data.Port,
                    data.AllocationIdBytes,
                    data.KeyBytes,
                    data.ConnectionData,
                    data.HostConnectionData,
                    data.IsSecure
                });
                return true;
            }

            MethodInfo fallback = transport.GetType().GetMethod("SetRelayServerData", BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(string), typeof(ushort), typeof(byte[]), typeof(byte[]), typeof(byte[]), typeof(byte[]), typeof(bool) }, null);
            if (fallback != null)
            {
                fallback.Invoke(transport, new object[]
                {
                    data.Host,
                    data.Port,
                    data.AllocationIdBytes,
                    data.KeyBytes,
                    data.ConnectionData,
                    data.HostConnectionData,
                    data.IsSecure
                });
                return true;
            }

            Debug.LogWarning("NgoTransportAdapter: no relay client transport configuration method was found.");
            return false;
        }

        private bool ConfigureLanAsHost(ushort port, string listenAddress)
        {
            object transport = ResolveTransport();
            MethodInfo method = transport?.GetType().GetMethod("SetConnectionData", BindingFlags.Public | BindingFlags.Instance);
            if (transport == null || method == null)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            if (parameters.Length > 0) args[0] = "0.0.0.0";
            if (parameters.Length > 1) args[1] = port;
            if (parameters.Length > 2) args[2] = listenAddress;
            method.Invoke(transport, args);
            return true;
        }

        private bool ConfigureLanAsClient(string address, ushort port)
        {
            object transport = ResolveTransport();
            MethodInfo method = transport?.GetType().GetMethod("SetConnectionData", BindingFlags.Public | BindingFlags.Instance);
            if (transport == null || method == null)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            if (parameters.Length > 0) args[0] = address;
            if (parameters.Length > 1) args[1] = port;
            if (parameters.Length > 2) args[2] = address;
            method.Invoke(transport, args);
            return true;
        }

        private bool InvokeNetworkManagerBoolMethod(string methodName)
        {
            object networkManager = ResolveNetworkManager();
            MethodInfo method = networkManager?.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (networkManager == null || method == null)
            {
                return false;
            }

            object result = method.Invoke(networkManager, null);
            return result == null || (result is bool success && success);
        }

        private object ResolveNetworkManager()
        {
            if (networkManagerObject == null)
            {
                return null;
            }

            Component[] components = networkManagerObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().FullName == "Unity.Netcode.NetworkManager")
                {
                    return components[i];
                }
            }

            return null;
        }

        private object ResolveTransport()
        {
            object networkManager = ResolveNetworkManager();
            if (networkManager == null)
            {
                return null;
            }

            object networkConfig = networkManager.GetType().GetProperty("NetworkConfig", BindingFlags.Public | BindingFlags.Instance)?.GetValue(networkManager);
            object networkTransport = networkConfig?.GetType().GetProperty("NetworkTransport", BindingFlags.Public | BindingFlags.Instance)?.GetValue(networkConfig);
            return networkTransport;
        }
    }
}
