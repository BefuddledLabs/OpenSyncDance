#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AnimatorAsCode.V1;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;

namespace OpenSyncDance
{

    [Serializable]
    public class SyncedAnimation
    {
        public AnimationClip animation;
        public AudioClip audio;
    }

    [CustomPropertyDrawer(typeof(SyncedAnimation))]
    public class SyncAnimationDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect space, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(space, label, property);

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // TODO: this renders like peepee poopoo
            // space = EditorGUI.PrefixLabel(space, GUIUtility.GetControlID(FocusType.Passive), label);

            float halfWidth = space.width / 2f;
            EditorGUI.PropertyField(new Rect(space.x, space.y, halfWidth, space.height), property.FindPropertyRelative("animation"), GUIContent.none);
            space.x += halfWidth;
            EditorGUI.PropertyField(new Rect(space.x, space.y, halfWidth, space.height), property.FindPropertyRelative("audio"), GUIContent.none);

            EditorGUI.EndProperty();

            EditorGUI.indentLevel = oldIndent;
        }
    }

    [RequireComponent(typeof(Animator))]
    public class OpenSyncDance : MonoBehaviour, IEditorOnly
    {
        /// <summary>
        /// UUID used for tracking purposes. Mainly used by Animator as Code.
        /// </summary>
        public string assetKey;

        /// <summary>
        /// Do we use write defaults? Yeag or nope?
        /// </summary>
        public bool useWriteDefaults;

        /// <summary>
        /// The animator controller that we will be generating.
        /// </summary>
        public AnimatorController assetContainer;

        /// <summary>
        /// The animations that we want to sync!
        /// </summary>
        public List<SyncedAnimation> animations = new List<SyncedAnimation>();
    }

    [CustomEditor(typeof(OpenSyncDance))]
    public class OpenSyncDanceEditor : Editor
    {
        // Warning: the variables here are not serialized and thus not persistent!

        /// <summary>
        /// Reference to the underlying open sync dance component that stores releveant data.
        /// </summary>
        private OpenSyncDance _self;

        private AacFlBase _aac;
        private AacFlLayer _mainLayer;

        /// <summary>
        /// The animations that need to be syncable.
        /// </summary>
        private List<SyncedAnimation> _animations => _self.animations;

        /// <summary>
        /// The number of bits that are used to sync with other players.
        /// </summary>
        private int _numberOfBits => Utils.NumberOfBitsToRepresent(_animations.Count);

        /// <summary>
        /// Parameter name of the contact receiver. This value needs to be formatted with <tt>string.Format</tt>.
        /// </summary>
        private string _recvParamName = "OSD_RecvBit{0}";

        /// <summary>
        /// Parameter name of the contact sender. This value needs to be formatted with <tt>string.Format</tt>.
        /// </summary>
        private string _sendParamName = "OSD_SendBit{0}";
        private AnimatorController _animationController => _self.assetContainer;
        private AacFlBoolParameterGroup _paramRecvBits;
        private AacFlBoolParameterGroup _paramSendBits;

        private void OnEnable()
        {
            _self = (OpenSyncDance)target;
            if (string.IsNullOrWhiteSpace(_self.assetKey))
                _self.assetKey = GUID.Generate().ToString();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var assetContainer_property = serializedObject.FindProperty("assetContainer");
            EditorGUILayout.PropertyField(assetContainer_property, true);

            var animation_property = serializedObject.FindProperty("animations");
            EditorGUILayout.PropertyField(animation_property, true);

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("AAA"))
            {
                // TODO: create new animation controller if it doesn't exist
                if (!_animationController)
                    throw new ArgumentNullException();

                AnimatorSetup();
                Generate();
            }
        }

        private void AnimatorSetup()
        {
            _aac = AacV1.Create(new AacConfiguration
            {
                SystemName = "OpenSyncDance",
                AnimatorRoot = _self.transform,
                DefaultValueRoot = _self.transform,
                AssetKey = _self.assetKey,
                AssetContainer = _animationController,
                ContainerMode = AacConfiguration.Container.Everything,
                DefaultsProvider = new AacDefaultsProvider(_self.useWriteDefaults),
            });

            _mainLayer = _aac.CreateMainArbitraryControllerLayer(_animationController);

            // Create the parameters for recieving the animtion index;
            var receiveParamNames = new List<string>();
            for (int i = 0; i < _numberOfBits; i++)
                receiveParamNames.Add(string.Format(_recvParamName, i));
            _paramRecvBits = _mainLayer.BoolParameters(receiveParamNames.ToArray());

            // Create the parameters for sending the animtion index;
            var sendParamNames = new List<string>();
            for (int i = 0; i < _numberOfBits; i++)
                sendParamNames.Add(string.Format(_sendParamName, i));
            _paramSendBits = _mainLayer.BoolParameters(sendParamNames.ToArray());
        }

        private void Generate()
        {
            // Destroy children >:)
            while (_self.transform.childCount > 0)
                DestroyImmediate(_self.transform.GetChild(0).gameObject);

            // Create contacts root object to hold
            var contactContainer = new GameObject("OSD_Contacts");
            contactContainer.transform.parent = _self.transform;

            List<VRCContactReceiver> _contentReceivers = new List<VRCContactReceiver>();
            List<VRCContactSender> _contentSenders = new List<VRCContactSender>();

            var recvParamNames = _paramRecvBits.ToList();
            var sendParamNames = _paramSendBits.ToList();
            for (int i = 0; i < _numberOfBits; i++)
            {
                var recvContact = new GameObject(recvParamNames[i].Name);
                var sendContact = new GameObject(sendParamNames[i].Name);

                recvContact.transform.parent = contactContainer.transform;
                sendContact.transform.parent = contactContainer.transform;

                var contactReceiver = recvContact.AddComponent<VRCContactReceiver>();
                var contactSender = sendContact.AddComponent<VRCContactSender>();

                _contentReceivers.Add(contactReceiver);
                _contentSenders.Add(contactSender);

                contactReceiver.allowOthers = true;
                contactReceiver.allowSelf = true;
                contactReceiver.collisionTags.Add(recvParamNames[i].Name);
                contactReceiver.parameter = recvParamNames[i].Name;

                contactSender.collisionTags.Add(recvParamNames[i].Name);
                contactSender.enabled = false;
            }

            // layers:
            // - send layer
            //   - broadcasts animation id via contacts
            //   - encoded as binary
            // - receive layer
            //   - detects broadcasted animation id via contacts
            //   - also detects own broadcast to account for round-trip-latency for better sync
            // - config layer
            //   - used blendtrees to enable and disable stuff


            // Enocde binary animation to toggle senders
        }
    }
}

#endif