using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class ObjectPoolingManager : MonoBehaviour, INetworkObjectProvider
{
    // Key = the prefab source, Value = the instances spawned from it
    private readonly Dictionary<INetworkPrefabSource, List<NetworkObject>> prefabsThatHadBeenInstantiated = new();

    private void Start()
    {
        if (GlobalManagers.Instance != null)
        {
            GlobalManagers.Instance.ObjectPoolingManager = this;
        }
    }

    public NetworkObjectAcquireResult AcquireInstance(NetworkRunner runner, in NetworkObjectAcquireContext context, out NetworkObject result)
    {
        result = null;

        // Scene objects are not prefabs. Calling AsPrefabId on them throws,
        // which is what broke Player 2. Leave them to the scene manager.
        if (!context.TypeId.IsPrefab)
        {
            return NetworkObjectAcquireResult.Ignore;
        }

        NetworkPrefabId prefabID = context.TypeId.AsPrefabId;
        INetworkPrefabSource prefabSource = NetworkProjectConfig.Global.PrefabTable.GetSource(prefabID);

        if (prefabSource == null)
        {
            Debug.LogError($"No prefab source found for prefab id {prefabID}");
            return NetworkObjectAcquireResult.Failed;
        }

        prefabsThatHadBeenInstantiated.TryGetValue(prefabSource, out var networkObjects);

        NetworkObject networkObject = null;

        // Recycle an inactive instance if there is one
        if (networkObjects != null)
        {
            foreach (var item in networkObjects)
            {
                if (item != null && !item.gameObject.activeSelf)
                {
                    networkObject = item;
                    break;
                }
            }
        }

        // Nothing to recycle, so create a new one
        if (networkObject == null)
        {
            networkObject = CreateObjectInstance(prefabSource);
        }

        if (networkObject == null)
        {
            return NetworkObjectAcquireResult.Failed;
        }

        result = networkObject;
        return NetworkObjectAcquireResult.Success;
    }

    private NetworkObject CreateObjectInstance(INetworkPrefabSource prefab)
    {
        prefab.Acquire(synchronous: true);

        if (!prefab.IsCompleted)
        {
            Debug.LogError($"Prefab {prefab.Description} not immediately available");
            return null;
        }

        var obj = Instantiate(prefab.WaitForResult(), Vector3.zero, Quaternion.identity);

        if (prefabsThatHadBeenInstantiated.TryGetValue(prefab, out var instanceData))
        {
            instanceData.Add(obj);
        }
        else
        {
            prefabsThatHadBeenInstantiated.Add(prefab, new List<NetworkObject> { obj });
        }

        return obj;
    }

    // Called once runner.Despawn is called
    public void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
    {
        if (context.Object != null)
        {
            context.Object.gameObject.SetActive(false);
        }
    }

    public NetworkPrefabId GetPrefabId(NetworkRunner runner, NetworkObjectGuid prefabGuid)
    {
        return runner.Prefabs.GetId(prefabGuid);
    }

    public void RemoveNetworkObjectFromDic(NetworkObject obj)
    {
        foreach (var item in prefabsThatHadBeenInstantiated)
        {
            if (item.Value.Remove(obj))
            {
                break;
            }
        }
    }
}