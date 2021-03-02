using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;


namespace Scream.UniUIP
{
    [CustomEditor(typeof(UIPlayable))]
    public class UIPlayableInspector : Editor
    {

        UIPlayable playable => target as UIPlayable;
        ReorderableList m_ReorderableList;

        public void OnEnable()
        {
            m_ReorderableList = new ReorderableList(playable.States, typeof(State), true, true, true, true);
            m_ReorderableList.drawHeaderCallback = OnDrawHeader;
            m_ReorderableList.drawElementCallback = OnDrawElement;
            m_ReorderableList.elementHeightCallback = GetElementHeight;
            m_ReorderableList.onAddCallback = OnAddElement;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultState();
            m_ReorderableList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawDefaultState()
        {
            State defaultState = playable.DefaultState;
            int defaultStateIndex = playable.States.IndexOf(defaultState);
            List<string> options = new List<string>() { "None" };
            foreach (State state in playable.States)
            {
                if (string.IsNullOrEmpty(state.Name))
                {
                    options.Add("");
                    options.Add("");
                }
                else
                {
                    options.Add(state.Name);
                    options.Add(state.Name + " (End)");
                }
            }

            int currentIndex = 0;
            if (defaultStateIndex >= 0)
            {
                currentIndex = defaultStateIndex * 2 + 1;
                if (playable.DefaultStateAnimation == UIPlayable.StateAnimationType.Loop)
                    currentIndex += 1;
            }

            int newIndex = EditorGUILayout.Popup("Default State", currentIndex, options.ToArray());
            if (newIndex != currentIndex)
            {
                if (newIndex == 0)
                {
                    playable.DefaultState = null;
                    playable.DefaultStateAnimation = UIPlayable.StateAnimationType.Enter;
                }
                else
                {
                    var state = playable.States[(newIndex - 1) / 2];
                    playable.DefaultState = state;
                    playable.DefaultStateAnimation = newIndex % 2 == 1 ? UIPlayable.StateAnimationType.Enter : UIPlayable.StateAnimationType.Loop;
                }
                EditorUtility.SetDirty(playable);
            }
        }

        void OnDrawHeader(Rect rect)
        {
            GUI.Label(rect, "States");
        }

        void OnDrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty state = serializedObject.FindProperty("States").GetArrayElementAtIndex(index);
            float y = rect.yMin + 2;

            DrawPropertyField("Name");
            DrawPropertyField("Animation");
            DrawPropertyField("LoopAnimation");
            DrawPropertyField("OnAnimationEnd");

            void DrawPropertyField(string name)
            {
                SerializedProperty property = state.FindPropertyRelative(name);
                float h = EditorGUI.GetPropertyHeight(property);
                float w = rect.width;
                if (Application.isPlaying && name == "Name")
                {
                    w -= 80;
                    if (GUI.Button(new Rect(rect.xMax - 80, y, 80, 16), "Play"))
                        playable.Play(playable.States[index]);
                }
                EditorGUI.PropertyField(new Rect(rect.x, y, w, h), property);
                y += h + 2;
            }
        }

        float GetElementHeight(int index)
        {
            SerializedProperty state = serializedObject.FindProperty("States").GetArrayElementAtIndex(index);
            return 2 + 18 * 3 + EditorGUI.GetPropertyHeight(state.FindPropertyRelative("OnAnimationEnd")) + 10;
        }

        void OnAddElement(ReorderableList list)
        {
            playable.States.Add(new State());
        }
    }
}

