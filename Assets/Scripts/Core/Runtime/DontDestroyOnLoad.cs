using UnityEngine;

namespace Arcontio.Core
{
    public sealed class DontDestroyRuntime : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
