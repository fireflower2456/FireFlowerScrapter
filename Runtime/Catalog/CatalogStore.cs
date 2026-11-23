using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace FireFlower
{
    // Referencing it from a scene would bake it into both the scene and the bundle, so load only by address. The address is the type name
    public static class CatalogStore
    {
        static class Slot<T>
        {
            public static AsyncOperationHandle<T> handle;
        }

        static readonly List<AsyncOperationHandle> loading = new();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLoading() => loading.Clear();

        // Only hits here when entering without the loading step (running a scene alone)
        public static T Get<T>()
        {
            if (!Slot<T>.handle.IsValid()) Load<T>();
            return Slot<T>.handle.IsDone ? Slot<T>.handle.Result : Slot<T>.handle.WaitForCompletion();
        }

        // Used until the game ends, so the handle is never released
        public static void Load<T>()
        {
            Slot<T>.handle = Addressables.LoadAssetAsync<T>(typeof(T).Name);
            loading.Add(Slot<T>.handle);
        }

        // Use an asset in hand instead of an address (a catalog the scene references, samples, etc.)
        public static void Set<T>(T asset) where T : UnityEngine.Object
        {
            Slot<T>.handle = Addressables.ResourceManager.CreateCompletedOperation(asset, null);
        }

        public static IEnumerator Wait()
        {
            foreach (AsyncOperationHandle handle in loading)
                while (!handle.IsDone) yield return null;
        }
    }
}
