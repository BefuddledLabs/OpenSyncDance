#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;


namespace BefuddledLabs.OpenSyncDance
{
    static class ExtraGUI
    {
        public static IEnumerable<Rect> DistributeHorizontally(int num, Rect space, float padding)
        {
            float w = (space.width - (num - 1) * padding) / num;
            float dx = w + padding;

            return Enumerable.Range(0, num).Select(i => new Rect(space.x + dx * i, space.y, w, space.height));
        }

        public static IEnumerable<Rect> DistributeVertically(int num, Rect space, float padding)
        {
            float h = (space.height - (num - 1) * padding) / num;
            float dy = h + padding;

            return Enumerable.Range(0, num).Select(i => new Rect(space.x, space.y + dy * i, space.width, h));
        }

        public static void DrawPropertyFieldWithLabel(Rect rect, SerializedProperty property, string label, GUIStyle labelStyle)
        {
            if (labelStyle != GUIStyle.none)
            {
                EditorGUI.LabelField(rect, label, labelStyle);
                rect.x += EditorGUIUtility.labelWidth;
                rect.width -= EditorGUIUtility.labelWidth;
            }
            EditorGUI.PropertyField(rect, property, GUIContent.none);
        }

        public static void SliderFieldWithLabel(Rect rect, SerializedProperty property, float leftValue, float rightValue, string label, GUIStyle labelStyle)
        {
            if (labelStyle != GUIStyle.none)
            {
                EditorGUI.LabelField(rect, label, labelStyle);
                rect.x += EditorGUIUtility.labelWidth;
                rect.width -= EditorGUIUtility.labelWidth;
            }
            EditorGUI.Slider(rect, property, leftValue, rightValue, GUIContent.none);
        }

        static public GUIBuilder Builder(SerializedProperty property)
            => new(property);

        public class GUIBuilderElement
        {
            public GUIBuilderElement(Action<Rect> action, float height = 20f)
            {
                _action = action;
                this.height = height;
            }

            readonly Action<Rect> _action;
            public float height;

            public void Draw(Rect space)
                => _action(space);
        }

        public class GUIBuilder
        {
            public GUIBuilder(SerializedProperty property)
            {
                _property = property;
            }

            SerializedProperty _property;

            readonly List<GUIBuilderElement> _itemsToDraw = new();

            public GUIBuilder Draw(Action<Rect> action, float height = 20f)
            {
                _itemsToDraw.Add(new GUIBuilderElement(action, height));
                return this;
            }

            public GUIBuilder Draw(Func<GUIBuilder, GUIBuilderElement> builder)
            {
                _itemsToDraw.Add(builder(new GUIBuilder(_property)));
                return this;
            }
            
            public GUIBuilder DrawField(string field, string label, GUIStyle style)
                => DrawField(_property.FindPropertyRelative(field), label, style);

            public GUIBuilder DrawField(SerializedProperty property, string label, GUIStyle style)
                => Draw((Rect space) => DrawPropertyFieldWithLabel(space, property, label, style), EditorGUI.GetPropertyHeight(property));
                
            public GUIBuilder DrawSlider(string field, float min, float max, string label, GUIStyle style)
                => DrawSlider(_property.FindPropertyRelative(field), min, max, label, style);

            public GUIBuilder DrawSlider(SerializedProperty property, float min, float max, string label, GUIStyle style)
                => Draw((Rect space) => SliderFieldWithLabel(space, property, min, max, label, style), EditorGUI.GetPropertyHeight(property));

            public GUIBuilderElement DrawHorizontally(float padding = 4f)
            {
                return new GUIBuilderElement((Rect space) => {
                    var dist = DistributeHorizontally(_itemsToDraw.Count, space, padding).ToList();
                    for (int i = 0; i < dist.Count; i++)
                        _itemsToDraw[i].Draw(dist[i]);
                }, _itemsToDraw.Max(x => x.height));
            }

            public GUIBuilderElement DrawVertically(float padding = 4f)
            {
                return new GUIBuilderElement((Rect space) => {
                    for (int i = 0; i < _itemsToDraw.Count; i++)
                    {
                        var item = _itemsToDraw[i];
                        item.Draw(new Rect(space.x, space.y, space.width, item.height));
                        space.y += item.height + padding;
                    }
                }, _itemsToDraw.Sum(x => x.height + padding * (_itemsToDraw.Count - 1)));
            }
        }
    }
}

#endif