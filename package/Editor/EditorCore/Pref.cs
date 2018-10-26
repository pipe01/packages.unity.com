using UnityEditor.SettingsManagement;

namespace UnityEditor.ProBuilder
{
	public class Pref<T> : UserSetting<T>
	{
		public Pref(string key, T value, SettingsScopes scope = SettingsScopes.Project)
		: base(ProBuilderSettings.instance, key, value, scope)
		{}

		public Pref(Settings settings, string key, T value, SettingsScopes scope = SettingsScopes.Project)
			: base(settings, key, value, scope) { }
	}
}
