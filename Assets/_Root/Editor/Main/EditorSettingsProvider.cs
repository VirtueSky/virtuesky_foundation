﻿using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pancake.Editor
{
    public sealed class EditorSettingsProvider : SettingsProvider
    {
        private EditorSettings settings;
        private InspectorEditor editor;

        /// <summary>
        /// EditorSettingsProvider constructor.
        /// </summary>
        /// <param name="path">Path used to place the SettingsProvider in the tree view of the Settings window. The path should be unique among all other settings paths and should use "/" as its separator.</param>
        /// <param name="scopes">Scope of the SettingsProvider. The Scope determines whether the SettingsProvider appears in the Preferences window (SettingsScope.User) or the Settings window (SettingsScope.Project).</param>
        /// <param name="keywords">List of keywords to compare against what the user is searching for. When the user enters values in the search box on the Settings window, SettingsProvider.HasSearchInterest tries to match those keywords to this list.</param>
        public EditorSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        /// <summary>
        /// Use this function to implement a handler for when the user clicks on the Settings in the Settings window. You can fetch a settings Asset or set up UIElements UI from this function.
        /// </summary>
        /// <param name="searchContext">Search context in the search box on the Settings window.</param>
        /// <param name="rootElement">Root of the UIElements tree. If you add to this root, the SettingsProvider uses UIElements instead of calling SettingsProvider.OnGUI to build the UI. If you do not add to this VisualElement, then you must use the IMGUI to build the UI.</param>
        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            settings = EditorSettings.Current;
            editor = (InspectorEditor) UnityEditor.Editor.CreateEditor(settings, typeof(InspectorEditor));
            editor.KeepEnable(true);
        }

        /// <summary>
        /// Use this function to override drawing the title for the SettingsProvider using IMGUI. This allows you to add custom UI (such as a toolbar button) next to the title. 
        /// AssetSettingsProvider uses this mechanism to display the "add to preset" and the "help" buttons.
        /// </summary>
        public override void OnTitleBarGUI()
        {
            Rect position = GUILayoutUtility.GetRect(0, 0);
            position.x -= 19.0f;
            position.y += 6.0f;
            position.width = 20;
            position.height = 20;

            Rect popupPosition = new Rect(position.x, position.y, position.width, position.height);
            if (GUI.Button(popupPosition, EditorGUIUtility.IconContent("_Popup"), "IconButton"))
            {
                GenericMenu popupMenu = new GenericMenu();
                popupMenu.AddItem(new GUIContent("Reset", "Reset setting to default."), false, ResetSettingsFunction);

                Rect dropdownPosition = new Rect(popupPosition.x - 88, popupPosition.y, popupPosition.width, popupPosition.height);
                popupMenu.DropDown(dropdownPosition);
            }
        }

        /// <summary>
        /// Use this function to draw the UI based on IMGUI. This assumes you haven't added any children to the rootElement passed to the OnActivate function.
        /// </summary>
        /// <param name="searchContext">Search context for the Settings window. Used to show or hide relevant properties.</param>
        public override void OnGUI(string searchContext)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            EditorGUIUtility.labelWidth = 248;

            if (settings != null)
            {
                EditorGUI.BeginChangeCheck();
                editor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    InspectorEditor.RebuildAllInstances(typeof(EditorSettings).Name);
                }
            }

            GUILayout.EndVertical();
            GUILayout.Space(3);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Reset settings function to use in popup generic menu.
        /// </summary>
        private void ResetSettingsFunction()
        {
            EditorSettings.ResetSettings(settings);
            InspectorEditor.RepaintAllInstances();
        }

        /// <summary>
        /// Register EditorSettingsProvider in Project Settings window.
        /// </summary>
        /// <returns>New EditorSettingsProvider instance.</returns>
        [SettingsProvider]
        public static SettingsProvider RegisterEditorSettingsProvider()
        {
            return new EditorSettingsProvider("Project/Pancake/Editor Setting", SettingsScope.Project, Keywords);
        }

        /// <summary>
        /// Editor setting provider keywords.
        /// <br>
        /// When the user enters values in the search box on the Settings window, 
        /// SettingsProvider.HasSearchInterest tries to match those keywords to this list.
        /// </br>
        /// </summary>
        public static IEnumerable<string> Keywords
        {
            get
            {
                yield return "Attributes";
                yield return "Editor";
            }
        }
    }
}