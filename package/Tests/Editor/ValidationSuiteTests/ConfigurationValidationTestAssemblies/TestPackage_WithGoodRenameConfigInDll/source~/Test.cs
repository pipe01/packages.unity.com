#if UNITY_2018_1_OR_NEWER
namespace TestPackage_WithGoodRenameConfigInDll
{
    public class Foo
    {
        [System.Obsolete("(UnityUpgradable) -> Baz(UnityEngine.MonoBehaviour)")]
        public void Bar(UnityEngine.MonoBehaviour m) {}
        public void Baz(UnityEngine.MonoBehaviour m) {}
    }
}
#endif