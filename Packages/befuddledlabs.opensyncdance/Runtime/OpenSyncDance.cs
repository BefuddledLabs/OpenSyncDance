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

    public class OpenSyncDance : MonoBehaviour, IEditorOnly
    {
        /// <summary>
        /// UUID used for tracking purposes. Mainly used by Animator as Code.
        /// </summary>
        public string assetKey;
        
        /// <summary>
        /// Prefix string used for the contacts.
        /// </summary>
        public string contactPrefix = "OSDDefault_";

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
        /// Layer that manages syncing the current animation with the fewest amount of bits needed.
        /// </summary>
        private AacFlLayer _bitLayer;

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
        private string _recvBitParamName => _contactPrefix + "OSD_RecvBit{0}";

        /// <summary>
        /// Parameter name of the animation id that we want to broadcast.
        /// </summary>
        private string _sendAnimIdName = "OSD_SendAnim";

        /// <summary>
        /// Prefix string used for the contacts.
        /// </summary>
        private string _contactPrefix => _self.contactPrefix;

        // TODO: make this a variable that you can set from ui whatever idk??? lol --nara
        /// <summary>
        /// Component that gets plays the audio.
        /// </summary>
        private AudioSource _audioSource => _self.GetComponentInChildren<AudioSource>();


        private List<VRCContactReceiver> _contactReceivers;
        private List<VRCContactSender> _contactSenders;

        private AnimatorController _animationControllerFX => _self.animatorControllerFX;
        private AacFlBoolParameterGroup _paramRecvBits;
        private AacFlBoolParameterGroup _paramSendBits;
        private AacFlIntParameter _paramSendAnimId;
        private AacFlBoolParameterGroup _paramSendAnimIdBits;

        private void OnEnable()
        {
            _self = (OpenSyncDance)target;
            if (string.IsNullOrWhiteSpace(_self.assetKey))
                _self.assetKey = GUID.Generate().ToString();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var contactPrefix_property = serializedObject.FindProperty("contactPrefix");
            EditorGUILayout.PropertyField(contactPrefix_property, true);

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
                if (!_animationControllerFX)
                    throw new ArgumentNullException();

                AnimatorSetup();
                CreateMenu();
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
                AssetContainer = _animationControllerFX,
                ContainerMode = AacConfiguration.Container.Everything,
                DefaultsProvider = new AacDefaultsProvider(_self.useWriteDefaults),
            });

            _recvLayer = _aac.CreateSupportingArbitraryControllerLayer(_self.animatorControllerAction, "recvLayer");
            _sendLayer = _aac.CreateSupportingArbitraryControllerLayer(_animationControllerFX, "sendLayer");
            _bitLayer = _aac.CreateSupportingArbitraryControllerLayer(_animationControllerFX, "BitConverter");

            // Create the parameters for recieving the animation index;
            var receiveParamNames = new List<string>();
            var paramSendAnimIdBitsNames = new List<string>();
            for (int i = 0; i < _numberOfBits; i++){
                receiveParamNames.Add(string.Format(_recvBitParamName, i));
                paramSendAnimIdBitsNames.Add($"paramSendAnimIdBits_{i}");
            }
            _paramRecvBits = _recvLayer.BoolParameters(receiveParamNames.ToArray());
            _sendLayer.BoolParameters(receiveParamNames.ToArray()); // Ugly thing to get the same params on the Action animator
            _paramSendAnimIdBits = _bitLayer.BoolParameters(paramSendAnimIdBitsNames.ToArray());

            _paramSendAnimId = _sendLayer.IntParameter(_sendAnimIdName);
        }

        private void GenerateSyncedBitLayer() 
        {
            var _paramSendAnimIdBitsList = _paramSendAnimIdBits.ToList(); 

            var entry = _bitLayer.NewState("Entry");


            // Local player encodes sync bits
            var localEncode = _bitLayer.NewSubStateMachine("Local encode");
            entry.TransitionsTo(localEncode).When(_bitLayer.BoolParameter("IsLocal").IsTrue());
            localEncode.TransitionsTo(localEncode);

            var localAnimationStates = new List<AacFlState>();
            Utils.CreateBinarySearchTree(new AacFlStateMachineWrapped(localEncode), new AacFlIntDecisionParameter(_paramSendAnimId, _numberOfBits), ref localAnimationStates);
            for (int i = 0; i < localAnimationStates.Count; i++) {
                localAnimationStates[i].State.name = $"Send {i}";
                localAnimationStates[i].Exits().Automatically();
                for (int j = 0; j < _paramSendAnimIdBitsList.Count(); j++)
                    localAnimationStates[i].Drives(_paramSendAnimIdBitsList[j], (i & (1 << j)) > 0);
            }

            // TODO: drive contacts directly instead of decoding
            // Remote player decodes sync bits
            var remoteDecode = _bitLayer.NewSubStateMachine("Remote decode");
            entry.TransitionsTo(remoteDecode).When(_bitLayer.BoolParameter("IsLocal").IsFalse());
            remoteDecode.TransitionsTo(remoteDecode);

            var remoteAnimationStates = new List<AacFlState>();
            Utils.CreateBinarySearchTree(new AacFlStateMachineWrapped(remoteDecode), new AacFlBoolGroupDecisionParameter(_paramSendAnimIdBits, _numberOfBits), ref remoteAnimationStates);
            for (int i = 0; i < remoteAnimationStates.Count; i++){
                remoteAnimationStates[i].State.name = $"Receive {i}";
                remoteAnimationStates[i].Drives(_paramSendAnimId, i);
                remoteAnimationStates[i].Exits().Automatically();
            }
        }

        private void GenerateSendLayer()
        {
            var readyState = _sendLayer.NewState("Ready");
            var exitState = _sendLayer.NewState("Done");

            readyState.TransitionsFromEntry();
            exitState.Exits().Automatically().WithTransitionDurationSeconds(0.5f);

            for (int i = 1; i < _animations.Count + 1; i++)
            {
                var currentSyncedAnimation = _animations[i - 1];
                var danceState = _sendLayer.NewState($"Dance {(currentSyncedAnimation.animation == null ? i : currentSyncedAnimation.animation.name)}");
                var musicState = _sendLayer.NewState($"Music {(currentSyncedAnimation.audio == null ? i : currentSyncedAnimation.audio.name)}");

                // Set the audio clip on the audio source
                if (currentSyncedAnimation.audio != null)
                {
                    musicState.Audio(_audioSource,
                        (a) =>
                        {
                            a.SelectsClip(VRC_AnimatorPlayAudio.Order.Roundabout,
                                new[] { currentSyncedAnimation.audio });
                            a.PlayAudio.PlayOnEnter = true;
                            a.PlayAudio.StopOnExit = true;
                            a.SetsLooping();
                        });
                }

                readyState.TransitionsTo(danceState).When(_paramSendAnimId.IsEqualTo(i));
                var musicConditions = danceState.TransitionsTo(musicState).WhenConditions();

                var recvParamNames = _paramRecvBits.ToList();
                // Ew: Toggles the bits for the animations
                var toggleClip = _aac.NewClip().Animating(a =>
                {
                    for (int j = 0; j < recvParamNames.Count; j++)//for (int j = recvParamNames.Count - 1; j >= 0; j--)
                    {
                        var wantedParamState = (i & (1 << j)) > 0;
                        musicConditions = musicConditions.And(recvParamNames[j].IsEqualTo(wantedParamState));
                        a.Animates(_contactSenders[j].gameObject).WithOneFrame(wantedParamState ? 1 : 0);
                    }
                });

                danceState.WithAnimation(toggleClip);
                musicState.WithAnimation(toggleClip);

                musicState.TransitionsTo(exitState).When(_paramSendAnimId.IsNotEqualTo(i));
            }
        }

        private void GenerateRecieveLayer() 
        {
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
            var paramRecvBitsWrapped = new AacFlBoolGroupDecisionParameter(_paramRecvBits, _numberOfBits);
            Utils.CreateBinarySearchTree(new AacFlStateMachineWrapped(danceState), paramRecvBitsWrapped, ref animationStates);

            foreach (var animationState in animationStates)
                animationState.Exits().When(paramRecvBitsWrapped.IsZeroed());

            for (int i = 1; i < _animations.Count + 1; i++)
            {
                var currentState = animationStates[i];
                var currentSyncedAnimation = _animations[i - 1];
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

            _contactReceivers = new List<VRCContactReceiver>();
            _contactSenders = new List<VRCContactSender>();

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

                _contactReceivers.Add(contactReceiver);
                _contactSenders.Add(contactSender);

                contactReceiver.allowOthers = true;
                contactReceiver.allowSelf = true;
                contactReceiver.collisionTags.Add(recvParamNames[i].Name);
                contactReceiver.parameter = recvParamNames[i].Name;
                contactReceiver.radius = 5;

                contactSender.collisionTags.Add(recvParamNames[i].Name);
                contactSender.radius = 5;
            }

            GenerateSyncedBitLayer();
            GenerateSendLayer();
            GenerateRecieveLayer();
        }

        private void CreateMenu()
        {
            const int animsPerPage = 7;
            // The last page can contain one more than the usual anims per page, so subtract
            // one from the total. Then use 'divisor minus 1'-trick for a ceiling div.
            int numPages = (_animations.Count + animsPerPage - 2) / animsPerPage;

            // Create a path of folders
            List<string> assetFolderPath = new() { "Assets", "OpenSyncDanceGenerated", _self.assetKey };
            for (int i = 1; i < assetFolderPath.Count; i++)
            {
                // We don't have to create the assets folder, so  we start at i = 1. Then for every
                // item, 
                var prefixPath = string.Join('/', assetFolderPath.Take(i));
                if (!AssetDatabase.IsValidFolder($"{prefixPath}/{assetFolderPath[i]}"))
                    AssetDatabase.CreateFolder(prefixPath, assetFolderPath[i]);
            }
            var assetFolder = string.Join('/', assetFolderPath);

            // Create VRC params asset
            var vrcParams = CreateInstance<VRCExpressionParameters>();
            //vrcParams.parameters 
            var tempParams = new List<VRCExpressionParameters.Parameter> {
                new() {
                    name = _paramSendAnimId.Name,
                    valueType = VRCExpressionParameters.ValueType.Int,
                    saved = false,
                    networkSynced = false,
                    defaultValue = 0,
                }
            };

            for (int i = 0; i < _numberOfBits; i++)
            {
                tempParams.Add(new() {
                    name = $"paramSendAnimIdBits_{i}",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    saved = false,
                    networkSynced = true,
                    defaultValue = 0,
                });
            }
            vrcParams.parameters = tempParams.ToArray();
            
            // TODO: uhmm aksually update this instead of overwriting
            AssetDatabase.CreateAsset(vrcParams, $"{assetFolder}/OSD_Params.asset");

            // Create VRC menu assets
            for (int pageId = 0, animationId = 0; pageId < numPages; pageId++)
            {
                bool isLastPage = pageId == numPages - 1;
                int animsOnThisPage = animsPerPage + (isLastPage ? 1 : 0);

                var vrcMenu = CreateInstance<VRCExpressionsMenu>();

                // Skip animations that we already put in pages, then take enough to fill the page.
                // Map the taken items to a VRC menu button.
                vrcMenu.controls = _animations.Skip(animsPerPage * pageId).Take(animsOnThisPage).Select((SyncedAnimation anim) =>
                {
                    animationId++;
                    return new VRCExpressionsMenu.Control
                    {
                        icon = null, // TODO: get from _self
                        labels = new VRCExpressionsMenu.Control.Label[] { },
                        name = $"Dance {animationId}", // TODO: get from _self
                        parameter = new VRCExpressionsMenu.Control.Parameter()
                        {
                            name = _paramSendAnimId.Name,
                        },
                        style = VRCExpressionsMenu.Control.Style.Style4,
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        value = animationId,
                    };
                }).ToList();

                // TODO: uhmm aksually update this instead of overwriting
                AssetDatabase.CreateAsset(vrcMenu, $"{assetFolder}/OSD_Menu_{pageId}.asset");
            }
        }
    }
}

#endif