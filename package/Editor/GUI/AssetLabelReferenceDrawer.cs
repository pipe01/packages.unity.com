using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System.Linq;

namespace UnityEditor.AddressableAssets
{
    [CustomPropertyDrawer(typeof(AssetLabelReference), true)]
    internal class AssetLabelReferenceDrawer : PropertyDrawer
    {

        AddressableAssetSettings settings = null;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            if (settings == null)
                settings = AddressableAssetSettings.GetDefault(false, false);
            if(settings == null)
            {

            }
            else
            {

                var currentLabel = property.FindPropertyRelative("m_labelString");
                var currIndex = settings.labelTable.labelNames.IndexOf(currentLabel.stringValue);
                var labelList = settings.labelTable.labelNames.ToArray();
                var smallPos = EditorGUI.PrefixLabel(position, label);
                var newIndex = EditorGUI.Popup(smallPos, currIndex, labelList);
                if(newIndex != currIndex)
                {
                    currentLabel.stringValue = labelList[newIndex];
                }
            }
            EditorGUI.EndProperty();
        }

    }

}
