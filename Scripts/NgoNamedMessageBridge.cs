using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace MapEditorPrototype
{
    public class NgoNamedMessageBridge : MonoBehaviour
    {
        public const string SnapshotRequestMessage = "world_snapshot_request";
        public const string SnapshotMessage = "world_snapshot";
        public const string PatchMessage = "world_patch";
        public const string EditApplyRequestMessage = "edit_apply_request";
        public const string EditApplyResultMessage = "edit_apply_result";

        [SerializeField] private GameObject networkManagerObject;

        public event Action<ulong> SnapshotRequestReceived;
        public event Action<ulong, string> SnapshotReceived;
        public event Action<ulong, string> PatchReceived;
        public event Action<ulong, string> EditApplyRequestReceived;
        public event Action<ulong, string> EditApplyResultReceived;

        private readonly Dictionary<string, Delegate> registeredHandlers = new Dictionary<string, Delegate>();

        private void OnEnable()
        {
            RegisterDefaultHandlers();
        }

        private void OnDisable()
        {
            UnregisterDefaultHandlers();
        }

        public void RequestSnapshotFromServer()
        {
            SendToServer(SnapshotRequestMessage, string.Empty);
        }

        public void SendSnapshotToClient(ulong clientId, string json)
        {
            SendToClient(clientId, SnapshotMessage, json);
        }

        public void BroadcastPatch(string json, NgoTransportAdapter transportAdapter)
        {
            List<ulong> clientIds = transportAdapter != null ? transportAdapter.GetConnectedClientIds(false) : new List<ulong>();
            for (int i = 0; i < clientIds.Count; i++)
            {
                SendToClient(clientIds[i], PatchMessage, json);
            }
        }

        public void SendEditApplyRequestToServer(string json)
        {
            SendToServer(EditApplyRequestMessage, json);
        }

        public void SendEditApplyResultToClient(ulong clientId, string json)
        {
            SendToClient(clientId, EditApplyResultMessage, json);
        }

        public void RegisterDefaultHandlers()
        {
            RegisterNamedHandler(SnapshotRequestMessage, (senderId, payload) => SnapshotRequestReceived?.Invoke(senderId));
            RegisterNamedHandler(SnapshotMessage, (senderId, payload) => SnapshotReceived?.Invoke(senderId, payload));
            RegisterNamedHandler(PatchMessage, (senderId, payload) => PatchReceived?.Invoke(senderId, payload));
            RegisterNamedHandler(EditApplyRequestMessage, (senderId, payload) => EditApplyRequestReceived?.Invoke(senderId, payload));
            RegisterNamedHandler(EditApplyResultMessage, (senderId, payload) => EditApplyResultReceived?.Invoke(senderId, payload));
        }

        public void UnregisterDefaultHandlers()
        {
            string[] keys = new string[registeredHandlers.Count];
            registeredHandlers.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                UnregisterNamedHandler(keys[i]);
            }
        }

        private bool RegisterNamedHandler(string messageName, Action<ulong, string> callback)
        {
            object customMessagingManager = GetCustomMessagingManager();
            if (customMessagingManager == null || registeredHandlers.ContainsKey(messageName))
            {
                return false;
            }

            MethodInfo registerMethod = customMessagingManager.GetType().GetMethod("RegisterNamedMessageHandler", BindingFlags.Public | BindingFlags.Instance);
            if (registerMethod == null)
            {
                return false;
            }

            Type delegateType = registerMethod.GetParameters()[1].ParameterType;
            ParameterInfo[] invokeParams = delegateType.GetMethod("Invoke").GetParameters();
            if (invokeParams.Length != 2)
            {
                return false;
            }

            MethodInfo proxyMethod = GetType().GetMethod(nameof(HandleIncomingMessageProxy), BindingFlags.NonPublic | BindingFlags.Instance);
            ConstantExpression callbackConst = Expression.Constant(callback, typeof(Action<ulong, string>));
            ParameterExpression senderParam = Expression.Parameter(invokeParams[0].ParameterType, "senderId");
            ParameterExpression readerParam = Expression.Parameter(invokeParams[1].ParameterType, "reader");

            MethodCallExpression body = Expression.Call(
                Expression.Constant(this),
                proxyMethod,
                callbackConst,
                Expression.Convert(senderParam, typeof(ulong)),
                Expression.Convert(readerParam, typeof(object)));

            Delegate handlerDelegate = Expression.Lambda(delegateType, body, senderParam, readerParam).Compile();
            registerMethod.Invoke(customMessagingManager, new object[] { messageName, handlerDelegate });
            registeredHandlers[messageName] = handlerDelegate;
            return true;
        }

        private void UnregisterNamedHandler(string messageName)
        {
            object customMessagingManager = GetCustomMessagingManager();
            if (customMessagingManager == null || !registeredHandlers.ContainsKey(messageName))
            {
                return;
            }

            MethodInfo unregisterMethod = customMessagingManager.GetType().GetMethod("UnregisterNamedMessageHandler", BindingFlags.Public | BindingFlags.Instance);
            if (unregisterMethod != null)
            {
                unregisterMethod.Invoke(customMessagingManager, new object[] { messageName });
            }

            registeredHandlers.Remove(messageName);
        }

        private void HandleIncomingMessageProxy(Action<ulong, string> callback, ulong senderClientId, object fastBufferReader)
        {
            string payload = ReadStringFromReader(fastBufferReader);
            callback?.Invoke(senderClientId, payload);
        }

        private void SendToServer(string messageName, string json)
        {
            object networkManager = ResolveNetworkManager();
            object customMessagingManager = GetCustomMessagingManager();
            if (networkManager == null || customMessagingManager == null)
            {
                return;
            }

            object serverClientIdValue = networkManager.GetType().GetProperty("ServerClientId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)?.GetValue(networkManager);
            ulong serverClientId = serverClientIdValue != null ? Convert.ToUInt64(serverClientIdValue) : 0UL;
            SendToClient(serverClientId, messageName, json);
        }

        private void SendToClient(ulong clientId, string messageName, string json)
        {
            object customMessagingManager = GetCustomMessagingManager();
            if (customMessagingManager == null)
            {
                return;
            }

            object writer = CreateWriter(json);
            if (writer == null)
            {
                return;
            }

            MethodInfo sendMethod = customMessagingManager.GetType().GetMethod("SendNamedMessage", BindingFlags.Public | BindingFlags.Instance, null, null, null);
            MethodInfo resolvedMethod = FindSendMethod(customMessagingManager.GetType(), typeof(ulong));
            if (resolvedMethod == null)
            {
                DisposeWriter(writer);
                return;
            }

            object networkDelivery = Enum.Parse(resolvedMethod.GetParameters()[3].ParameterType, "ReliableSequenced");
            resolvedMethod.Invoke(customMessagingManager, new[] { (object)messageName, clientId, writer, networkDelivery });
            DisposeWriter(writer);
        }

        private object CreateWriter(string json)
        {
            Type writerType = PackageAsyncReflectionUtility.FindType(
                "Unity.Netcode.FastBufferWriter, Unity.Netcode.Runtime",
                "Unity.Netcode.FastBufferWriter, Unity.Netcode");
            Type allocatorType = PackageAsyncReflectionUtility.FindType(
                "Unity.Collections.Allocator, Unity.Collections");
            if (writerType == null || allocatorType == null)
            {
                return null;
            }

            object allocatorTemp = Enum.Parse(allocatorType, "Temp");
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json ?? string.Empty);
            int capacity = Mathf.Max(128, utf8.Length + 64);

            object writer = null;
            ConstructorInfo ctor = writerType.GetConstructor(new[] { typeof(int), allocatorType, typeof(int) });
            if (ctor != null)
            {
                writer = ctor.Invoke(new object[] { capacity, allocatorTemp, capacity * 2 });
            }
            else
            {
                ctor = writerType.GetConstructor(new[] { typeof(int), allocatorType });
                if (ctor != null)
                {
                    writer = ctor.Invoke(new[] { (object)capacity, allocatorTemp });
                }
            }

            if (writer == null)
            {
                return null;
            }

            MethodInfo writeString = writerType.GetMethod("WriteValueSafe", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            if (writeString == null)
            {
                DisposeWriter(writer);
                return null;
            }

            writeString.Invoke(writer, new object[] { json ?? string.Empty });
            return writer;
        }

        private string ReadStringFromReader(object reader)
        {
            if (reader == null)
            {
                return string.Empty;
            }

            MethodInfo method = reader.GetType().GetMethod("ReadValueSafe", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string).MakeByRefType() }, null);
            if (method == null)
            {
                return string.Empty;
            }

            object[] args = new object[] { string.Empty };
            method.Invoke(reader, args);
            return args[0] as string ?? string.Empty;
        }

        private void DisposeWriter(object writer)
        {
            writer?.GetType().GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance)?.Invoke(writer, null);
        }

        private MethodInfo FindSendMethod(Type customMessagingManagerType, Type recipientType)
        {
            MethodInfo[] methods = customMessagingManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "SendNamedMessage")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 4 && parameters[1].ParameterType == recipientType)
                {
                    return method;
                }
            }

            return null;
        }

        private object GetCustomMessagingManager()
        {
            object networkManager = ResolveNetworkManager();
            return networkManager?.GetType().GetProperty("CustomMessagingManager", BindingFlags.Public | BindingFlags.Instance)?.GetValue(networkManager);
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
    }
}
