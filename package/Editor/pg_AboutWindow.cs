using UnityEngine;
using UnityEditor;

namespace ProGrids.Editor
{
	/// <summary>
	/// Used to pop up the window on import.
	/// </summary>
	[InitializeOnLoad]
	static class pg_AboutWindowSetup
	{
		static pg_AboutWindowSetup()
		{
			EditorApplication.delayCall += () => { pg_AboutWindow.Init(false); };
		}
	}

	class pg_AboutWindow : EditorWindow
	{
		GUIContent m_LearnContent = new GUIContent("Learn ProGrids", "Documentation");
		GUIContent m_ForumLinkContent = new GUIContent("Support Forum", "ProCore Support Forum");
		GUIContent m_ContactContent = new GUIContent("Contact Us", "Send us an email!");
		GUIContent m_BannerContent = new GUIContent("", "ProGrids Quick-Start Video");

		const string k_VideoUrl = @"https://youtu.be/_BLArJ63_QU";
		const string k_LearnUrl = @"http://procore3d.com/docs/progrids";
		const string k_SupportUrl = @"http://www.procore3d.com/forum/";
		const string k_ContactEmailUrl = @"http://www.procore3d.com/about/";
		const float k_BannerWidth = 480f;
		const float k_BannerHeight = 270f;

		// relative to ProGrids root directory.
		const string k_ChangeLogPath = "CHANGELOG.md";

		const string k_AboutWindowVersionPref = "ProGrids_AboutWindowIdentifier";
		const string k_AboutPrefFormat = "M.m.p-t.b";

		// suffixed with ProGrids because Unity doesn't like two fonts of the same name in a project (probuilder conflict)
		internal const string k_FontRegular = "Asap-Regular-ProGrids.otf";
		internal const string k_FontMedium = "Asap-Medium-ProGrids.otf";

		// Use less contast-y white and black font colors for better readabililty
		public static readonly Color k_FontWhite = HexToColor(0xCECECE);
		public static readonly Color k_FontBlack = HexToColor(0x545454);
		public static readonly Color k_FontBlueNormal = HexToColor(0x00AAEF);
		public static readonly Color k_FontBlueHover = HexToColor(0x008BEF);

		const string k_ProductName = "ProGrids";
		pg_VersionInfo m_ChangeLogVersionInfo;
		string m_ChangeLogRichText = "";

		internal static GUIStyle bannerStyle,
								header1Style,
								versionInfoStyle,
								linkStyle,
								separatorStyle,
								changelogStyle,
								changelogTextStyle;

		Vector2 scroll = Vector2.zero;

		/// <summary>
		/// Return true if Init took place, false if not.
		/// </summary>
		/// <param name="fromMenu"></param>
		/// <returns></returns>
		public static bool Init (bool fromMenu)
		{
			if(fromMenu || EditorPrefs.GetString(k_AboutWindowVersionPref) != pg_Version.Current.ToString(k_AboutPrefFormat))
			{
				EditorPrefs.SetString(k_AboutWindowVersionPref, pg_Version.Current.ToString(k_AboutPrefFormat));
				GetWindow(typeof(pg_AboutWindow), true, "ProGrids", true).ShowUtility();
				return true;
			}

			return false;
		}

		static Color HexToColor(uint x)
		{
			return new Color( 	((x >> 16) & 0xFF) / 255f,
								((x >> 8) & 0xFF) / 255f,
								(x & 0xFF) / 255f,
								1f);
		}

		internal static void InitGuiStyles()
		{
			bannerStyle = new GUIStyle()
			{
				// RectOffset(left, right, top, bottom)
				margin = new RectOffset(12, 12, 12, 12),
				normal = new GUIStyleState() {
					background = pg_EditorUtility.LoadInternalAsset<Texture2D>("GUI/About/Banner_Normal.png")
				},
				hover = new GUIStyleState() {
					background = pg_EditorUtility.LoadInternalAsset<Texture2D>("GUI/About/Banner_Hover.png")
				},
			};

			header1Style = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				alignment = TextAnchor.MiddleCenter,
				fontSize = 24,
				// fontStyle = FontStyle.Bold,
				font = pg_EditorUtility.LoadInternalAsset<Font>("Font/" + k_FontMedium),
				normal = new GUIStyleState() { textColor = EditorGUIUtility.isProSkin ? k_FontWhite : k_FontBlack }
			};

			versionInfoStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				fontSize = 14,
				font = pg_EditorUtility.LoadInternalAsset<Font>("Font/" + k_FontRegular),
				normal = new GUIStyleState() { textColor = EditorGUIUtility.isProSkin ? k_FontWhite : k_FontBlack }
			};

			linkStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				alignment = TextAnchor.MiddleCenter,
				fontSize = 16,
				font = pg_EditorUtility.LoadInternalAsset<Font>("Font/" + k_FontRegular),
				normal = new GUIStyleState() {
					textColor = k_FontBlueNormal,
					background = pg_EditorUtility.LoadInternalAsset<Texture2D>(
						string.Format("GUI/About/ScrollBackground_{0}.png", EditorGUIUtility.isProSkin ? "Pro" : "Light"))
				},
				hover = new GUIStyleState() {
					textColor = k_FontBlueHover,
					background = pg_EditorUtility.LoadInternalAsset<Texture2D>(
						string.Format("GUI/About/ScrollBackground_{0}.png", EditorGUIUtility.isProSkin ? "Pro" : "Light"))
				}
			};

			separatorStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				alignment = TextAnchor.MiddleCenter,
				fontSize = 16,
				font = pg_EditorUtility.LoadInternalAsset<Font>("Font/" + k_FontRegular),
				normal = new GUIStyleState() { textColor = EditorGUIUtility.isProSkin ? k_FontWhite : k_FontBlack }
			};

			changelogStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				font = pg_EditorUtility.LoadInternalAsset<Font>("Font/" + k_FontRegular),
				richText = true,
				normal = new GUIStyleState() { background = pg_EditorUtility.LoadInternalAsset<Texture2D>(
					string.Format("GUI/About/ScrollBackground_{0}.png",
						EditorGUIUtility.isProSkin ? "Pro" : "Light"))
				}
			};

			changelogTextStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				font = pg_EditorUtility.LoadInternalAsset<Font>("Font/" + k_FontRegular),
				fontSize = 14,
				normal = new GUIStyleState() { textColor = EditorGUIUtility.isProSkin ? k_FontWhite : k_FontBlack },
				richText = true,
				wordWrap = true
			};
		}

		void OnEnable()
		{
			InitGuiStyles();

			Texture2D banner = bannerStyle.normal.background;

			if(banner == null)
			{
				Debug.LogWarning("Could not load About window resources");
				EditorApplication.delayCall += Close;
			}
			else
			{
				bannerStyle.fixedWidth = k_BannerWidth; // banner.width;
				bannerStyle.fixedHeight = k_BannerHeight; // banner.height;

				this.wantsMouseMove = true;

				this.minSize = new Vector2(k_BannerWidth + 24, k_BannerHeight * 2.5f);
				this.maxSize = new Vector2(k_BannerWidth + 24, k_BannerHeight * 2.5f);
			}

			TextAsset changeText = pg_EditorUtility.LoadInternalAsset<TextAsset>(k_ChangeLogPath);

			string raw = changeText != null ? changeText.text : "";

			if (!string.IsNullOrEmpty(raw))
			{
				// todo
				pg_VersionUtil.FormatChangelog(raw, out m_ChangeLogVersionInfo, out m_ChangeLogRichText);

#if !(DEBUG || DEVELOPMENT || PB_DEBUG)
				if(!pb_Version.Current.Equals(m_ChangeLogVersionInfo))
					pb_Log.Info("Changelog version does not match internal version. {0} != {1}",
						m_ChangeLogVersionInfo.ToString(k_AboutPrefFormat),
						pb_Version.Current.ToString(k_AboutPrefFormat));
#endif
			}

			if (m_ChangeLogVersionInfo == null)
			{
				Debug.LogWarning("Could not open ProGrids changelog, exiting About Window!");
				EditorApplication.delayCall += Close;
			}
		}

		void OnGUI()
		{
			if (bannerStyle.normal.background == null)
			{
				GUILayout.Label("Could Not Load About Window", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
				return;
			}

			Vector2 mousePosition = Event.current.mousePosition;

			if( GUILayout.Button(m_BannerContent, bannerStyle) )
				Application.OpenURL(k_VideoUrl);

			if(GUILayoutUtility.GetLastRect().Contains(mousePosition))
				Repaint();

			GUILayout.BeginVertical(changelogStyle);

			GUILayout.Label(k_ProductName, header1Style);

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

				if(GUILayout.Button(m_LearnContent, linkStyle))
					Application.OpenURL(k_LearnUrl);

				GUILayout.Label("|", separatorStyle);

				if(GUILayout.Button(m_ForumLinkContent, linkStyle))
					Application.OpenURL(k_SupportUrl);

				GUILayout.Label("|", separatorStyle);

				if(GUILayout.Button(m_ContactContent, linkStyle))
					Application.OpenURL(k_ContactEmailUrl);

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			if(GUILayoutUtility.GetLastRect().Contains(mousePosition))
				Repaint();

			GUILayout.EndVertical();

			// always bold the first line (cause it's the version info stuff)
			scroll = EditorGUILayout.BeginScrollView(scroll, changelogStyle);
			GUILayout.Label(string.Format("Version: {0}", m_ChangeLogVersionInfo.ToString("M.m.p")), versionInfoStyle);
			GUILayout.Label("\n" + m_ChangeLogRichText, changelogTextStyle);
			EditorGUILayout.EndScrollView();

			GUILayout.Label(pg_Version.Current.ToString());
		}

		/// <summary>
		/// Draw a horizontal line across the screen and update the guilayout.
		/// </summary>
		void HorizontalLine()
		{
			Rect r = GUILayoutUtility.GetLastRect();
			Color og = GUI.backgroundColor;
			GUI.backgroundColor = Color.black;
			GUI.Box(new Rect(0f, r.y + r.height + 2, Screen.width, 2f), "");
			GUI.backgroundColor = og;

			GUILayout.Space(6);
		}
	}
}
