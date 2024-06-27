#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
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
        public bool useWriteDefaults = true;

        /// <summary>
        /// The animator controller that we will be generating.
        /// </summary>
        public AnimatorController animatorControllerFX;
        
        /// <summary>
        /// The animator controller that we will be generating.
        /// </summary>
        public AnimatorController animatorControllerAction;

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

        /// <summary>
        /// Layer that manages receiving the animation id and triggering dance animations.
        /// </summary>
        private AacFlLayer _recvLayer;

        /// <summary>
        /// Layer that manages broadcasting the animation id.
        /// </summary>
        private AacFlLayer _sendLayer;

        /// <summary>
        /// The animations that need to be syncable.
        /// </summary>
        private List<SyncedAnimation> _animations => _self.animations;

        /// <summary>
        /// The number of bits that are used to sync with other players.
        /// </summary>
        private int _numberOfBits => Utils.NumberOfBitsToRepresent(_animations.Count);

        /// <summary>
        /// Parameter format of the contact bit receiver. This value needs to be formatted with <tt>string.Format</tt>.
        /// </summary>
        private string _recvBitParamName = "OSD_RecvBit{0}";

        /// <summary>
        /// Parameter name of the animation id that we want to broadcast.
        /// </summary>
        private string _sendAnimIdName = "OSD_SendAnim";

        // TODO: make this a variable that you can set from ui whatever idk??? lol --nara
        /// <summary>
        /// Component that gets plays the audio.
        /// </summary>
        private AudioSource _audioSource => _self.GetComponentInChildren<AudioSource>();

        private AnimatorController _animationController => _self.animatorControllerFX;
        private AacFlBoolParameterGroup _paramRecvBits;
        private AacFlBoolParameterGroup _paramSendBits;
        private AacFlIntParameter _paramSendAnimId;

        private void OnEnable()
        {
            _self = (OpenSyncDance)target;
            if (string.IsNullOrWhiteSpace(_self.assetKey))
                _self.assetKey = GUID.Generate().ToString();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var assetContainer_property = serializedObject.FindProperty("animatorControllerFX");
            EditorGUILayout.PropertyField(assetContainer_property, true);
            
            assetContainer_property = serializedObject.FindProperty("animatorControllerAction");
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

            _recvLayer = _aac.CreateSupportingArbitraryControllerLayer(_self.animatorControllerAction, "recvLayer");
            _sendLayer = _aac.CreateSupportingArbitraryControllerLayer(_animationController, "sendLayer");

            // Create the parameters for recieving the animation index;
            var receiveParamNames = new List<string>();
            for (int i = 0; i < _numberOfBits; i++)
                receiveParamNames.Add(string.Format(_recvBitParamName, i));
            _paramRecvBits = _recvLayer.BoolParameters(receiveParamNames.ToArray());
            _sendLayer.BoolParameters(receiveParamNames.ToArray()); // Ugly thing to get the same params on the Action animator

            _paramSendAnimId = _sendLayer.IntParameter(_sendAnimIdName);
        }

        private void Generate()
        {
            // Destroy children >:)
            // TODO: add method to keep certain objects for e.g. props that may be used
            var contactContainer = _self.transform.Find("OSD_Contacts")?.gameObject;
            if (contactContainer != null)
                DestroyImmediate(contactContainer);

            // Create contacts root object to hold
            contactContainer = new GameObject("OSD_Contacts");
            contactContainer.transform.parent = _self.transform;

            var contactReceivers = new List<VRCContactReceiver>();
            var contactSenders = new List<VRCContactSender>();

            var recvParamNames = _paramRecvBits.ToList();
            for (int i = 0; i < _numberOfBits; i++)
            {
                var recvContact = new GameObject(recvParamNames[i].Name);
                var sendContact = new GameObject($"Send{i}");

                recvContact.transform.parent = contactContainer.transform;
                sendContact.transform.parent = contactContainer.transform;

                var contactReceiver = recvContact.AddComponent<VRCContactReceiver>();
                var contactSender = sendContact.AddComponent<VRCContactSender>();

                // We want the sender to be off by default
                sendContact.SetActive(false);

                contactReceivers.Add(contactReceiver);
                contactSenders.Add(contactSender);

                contactReceiver.allowOthers = true;
                contactReceiver.allowSelf = true;
                contactReceiver.collisionTags.Add(recvParamNames[i].Name);
                contactReceiver.parameter = recvParamNames[i].Name;
                contactReceiver.radius = 5;

                contactSender.collisionTags.Add(recvParamNames[i].Name);
                contactSender.radius = 5;
            }

            // TODO: move this to function idk whatever
            {   // This is scoped so we can use nicer variable names

                var readyState = _sendLayer.NewState("Ready");
                var exitState = _sendLayer.NewState("Done");

                readyState.TransitionsFromEntry();
                exitState.Exits().Automatically().WithTransitionDurationSeconds(0.5f);

                for (int i = 1; i < _animations.Count+1; i++)
                {
                    var currentSyncedAnimation = _animations[i-1];
                    // TODO: add animation that toggles the senders
                    var danceState = _sendLayer.NewState($"Dance {(currentSyncedAnimation.animation == null ? i : currentSyncedAnimation.animation.name)}");
                    var musicState = _sendLayer.NewState($"Music {(currentSyncedAnimation.audio == null ? i : currentSyncedAnimation.audio.name)}");

                    // Set the audio clip on the audio source
                    if (currentSyncedAnimation.audio != null) 
                    {
                        musicState.Audio(_audioSource,
                            (a) => {
                                a.SelectsClip(VRC_AnimatorPlayAudio.Order.Roundabout,
                                    new[] { currentSyncedAnimation.audio });
                                a.PlayAudio.PlayOnEnter = true;
                                a.PlayAudio.StopOnExit = true;
                                a.SetsLooping();
                            });
                    }

                    readyState.TransitionsTo(danceState).When(_paramSendAnimId.IsEqualTo(i));
                    var musicConditions = danceState.TransitionsTo(musicState).WhenConditions();
                    
                    // Ew: Toggles the bits for the animations
                    var toggleClip = _aac.NewClip().Animating(a => {
                        for (int j = 0; j < recvParamNames.Count; j++) 
                        {
                            var wantedParamState = (i & (1 << j)) > 0;
                            musicConditions = musicConditions.And(recvParamNames[j].IsEqualTo(wantedParamState));
                            a.Animates(contactSenders[j].gameObject).WithOneFrame(wantedParamState ? 1 : 0);
                        }
                    });
                    
                    danceState.WithAnimation(toggleClip);
                    musicState.WithAnimation(toggleClip);
                    
                    musicState.TransitionsTo(exitState).When(_paramSendAnimId.IsNotEqualTo(i));
                }
            }

            {   // This is scoped so we can use nicer variable names
                var readyState = _recvLayer.NewState("Ready");
                var danceState = _recvLayer.NewSubStateMachine("Dance");
                var doneState = _recvLayer.NewState("Done");
                
                // Ugly thing to not IK sync the animation
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.Head);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.LeftHand);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.RightHand);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.Hip);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.LeftFoot);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.RightFoot);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.LeftFingers);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.RightFingers);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.Eyes);
                doneState.TrackingTracks(AacAv3.Av3TrackingElement.Mouth);

                danceState.PlayableEnables(VRC_PlayableLayerControl.BlendableLayer.Action);
                doneState.PlayableDisables(VRC_PlayableLayerControl.BlendableLayer.Action);
                
                doneState.Exits().Automatically();

                // Transition to dance blend tree whenever an animation is triggered
                readyState.TransitionsFromEntry();
                readyState.TransitionsTo(danceState).When(_paramRecvBits.IsAnyTrue());
                danceState.TransitionsTo(doneState); 

                var animationStates = new List<AacFlState>();
                Utils.CreateBinarySearchTree(new AacFlStateMachineWrapped(danceState), _paramRecvBits, _numberOfBits, ref animationStates);

                for (int i = 1; i < _animations.Count+1; i++)
                {
                    var currentState = animationStates[i];
                    var currentSyncedAnimation = _animations[i-1];
                    if (currentSyncedAnimation.animation != null) 
                    {
                        currentState.WithAnimation(currentSyncedAnimation.animation);
                        
                        // Ugly thing to turn IK sync back on
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.Head);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftHand);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.RightHand);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.Hip);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftFoot);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.RightFoot);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftFingers);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.RightFingers);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.Eyes);
                        currentState.TrackingAnimates(AacAv3.Av3TrackingElement.Mouth);
                    }
                }
            }
        }
    }
}

#endif