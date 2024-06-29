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
        public Texture2D icon;
        public string name;
        public AnimationClip animation;
        public AudioClip audio;
        public float volume = 1f; // TODO: for some reason adding a new anim to the list doesn't set volume to 1
    }

    [CustomPropertyDrawer(typeof(SyncedAnimation))]
    public class SyncAnimationDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect space, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(space, label, property);

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUI.DrawRect(new Rect(space.x, space.y, space.width, 2), new Color32(0x11, 0x00, 0x02, 0xFF));
            space.y += 8;

            //        44    12            16
            //      ┌───────┬─┐           ┌┐
            //    ┌ ┌───────┬─┐┌────────┬─┐┌────────┬─┐ ┐
            //    │ │Icon   │ ││Name    │o││Anim    │o│ │ 20
            // 44 │ │       │O│├────────┼─┤├────────┼─┤ ┤  4 (not shown)
            //    │ │       │ ││Audio   │o││Volume  │o│ │ 20
            //    └ └───────┴─┘└────────┴─┘└────────┴─┘ ┘
            //      └─────────┘└──────────────────────┘
            //         56         (width - 56 - 16) / 2

            Rect iconRect = new(space.x, space.y, 56, 44);
            space.x += 60;
            space.width = (space.width - 16 - 60) / 2;

            Rect nameRect = new(space.x, space.y, space.width, 20);
            Rect animRect = new(space.x + space.width + 16, space.y, space.width, 20);
            space.y += 24;
            Rect audioRect = new(space.x, space.y, space.width, 20);
            Rect volRect = new(space.x + space.width + 16, space.y, space.width, 20);

            EditorGUIUtility.labelWidth = 50;

            EditorGUI.PropertyField(iconRect, property.FindPropertyRelative("icon"), GUIContent.none);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.normal.textColor = new Color32(0xE2, 0x6C, 0xD7, 0xFF);
            DrawPropertyFieldWithLabel(nameRect, property.FindPropertyRelative("name"), "Name", labelStyle);
            DrawPropertyFieldWithLabel(animRect, property.FindPropertyRelative("animation"), "Anim", labelStyle);

            labelStyle.normal.textColor = new Color32(0x97, 0x29, 0xFC, 0xFF);
            DrawPropertyFieldWithLabel(audioRect, property.FindPropertyRelative("audio"), "Audio", labelStyle);
            SliderFieldWithLabel(volRect, property.FindPropertyRelative("volume"), 0, 1, "Volume", labelStyle);

            EditorGUI.EndProperty();

            EditorGUI.indentLevel = oldIndent;
        }

        private void DrawPropertyFieldWithLabel(Rect rect, SerializedProperty property, string label, GUIStyle labelStyle)
        {
            EditorGUI.LabelField(rect, label, labelStyle);
            rect.x += EditorGUIUtility.labelWidth;
            rect.width -= EditorGUIUtility.labelWidth;
            EditorGUI.PropertyField(rect, property, GUIContent.none);
        }

        private void SliderFieldWithLabel(Rect rect, SerializedProperty property, float leftValue, float rightValue, string label, GUIStyle labelStyle)
        {
            EditorGUI.LabelField(rect, label, labelStyle);
            rect.x += EditorGUIUtility.labelWidth;
            rect.width -= EditorGUIUtility.labelWidth;
            EditorGUI.Slider(rect, property, leftValue, rightValue, GUIContent.none);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Default height is 20
            return 56;
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
        public List<SyncedAnimation> animations = new();

        /// <summary>
        /// The VRC parameter asset that we will overwrite. If not set, will generate a new one.
        /// </summary>
        [NonReorderable]
        public VRCExpressionParameters vrcExpressionParameters;

        /// <summary>
        /// The VRC menu assets that we will overwrite. If not set, will generate a new one.
        /// </summary>
        public List<VRCExpressionsMenu> vrcExpressionMenus = new();

        /// <summary>
        /// Makes sure that the required classes are initialized.
        /// </summary>
        public void EnsureInitialized()
        {
            animations ??= new();
            vrcExpressionMenus ??= new();
        }
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

        private VRCExpressionParameters _vrcParams
        {
            get => _self.vrcExpressionParameters;
            set => _self.vrcExpressionParameters = value;
        }

        private List<VRCExpressionsMenu> _vrcMenus
        {
            get => _self.vrcExpressionMenus;
            set => _self.vrcExpressionMenus = value;
        }

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

        bool _uiAdvancedFoldoutState = false;

        private void OnEnable()
        {
            _self = (OpenSyncDance)target;
            if (string.IsNullOrWhiteSpace(_self.assetKey))
                _self.assetKey = GUID.Generate().ToString();
        }

        public override void OnInspectorGUI()
        {
            // Make sure it's initialized, other wise we might check null variables!
            _self.EnsureInitialized();

            serializedObject.Update();

            var contactPrefix_property = serializedObject.FindProperty("contactPrefix");
            EditorGUILayout.PropertyField(contactPrefix_property, true);

            var assetContainer_property = serializedObject.FindProperty("animatorControllerAction");
            EditorGUILayout.PropertyField(assetContainer_property, true);

            assetContainer_property = serializedObject.FindProperty("animatorControllerFX");
            EditorGUILayout.PropertyField(assetContainer_property, true);

            var animation_property = serializedObject.FindProperty("animations");
            EditorGUILayout.PropertyField(animation_property, true);

            // Advanced settings for smarty pants. You probably don't need this.
            if (_uiAdvancedFoldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(_uiAdvancedFoldoutState, "Advanced"))
            {
                var guidPoperty = serializedObject.FindProperty("assetKey");
                EditorGUILayout.PropertyField(guidPoperty, true);

                var vrcParamsProperty = serializedObject.FindProperty("vrcExpressionParameters");
                EditorGUILayout.PropertyField(vrcParamsProperty, true);

                EditorGUILayout.EndFoldoutHeaderGroup();

                var vrcMenuProperty = serializedObject.FindProperty("vrcExpressionMenus");
                EditorGUILayout.PropertyField(vrcMenuProperty, true);
            }
            else
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
            }


            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Generate!"))
            {
                // TODO: create new animation controller if it doesn't exist
                if (!_animationControllerFX)
                    throw new ArgumentNullException();
                if (!_self.animatorControllerAction)
                    throw new ArgumentNullException();

                AnimatorSetup();
                CreateMenu();
                Generate();
            }

            if (_self.vrcExpressionParameters)
                EditorGUILayout.HelpBox("Expression parameter is set, this will be overwritten which may remove any previous set parameters!", MessageType.Info);
            if (_self.vrcExpressionMenus == null || _self.vrcExpressionMenus.Count > 0)
                EditorGUILayout.HelpBox("Expression menus are set, this will be overwritten which may remove any previous set parameters!", MessageType.Info);
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
                DefaultsProvider = new AacDefaultsProvider(true),
            });

            _aac.ClearPreviousAssets();

            _recvLayer = _aac.CreateSupportingArbitraryControllerLayer(_self.animatorControllerAction, "recvLayer");
            _sendLayer = _aac.CreateSupportingArbitraryControllerLayer(_animationControllerFX, "sendLayer");
            _bitLayer = _aac.CreateSupportingArbitraryControllerLayer(_animationControllerFX, "BitConverter");

            // Create the parameters for receiving the animation index;
            var receiveParamNames = new List<string>();
            var paramSendAnimIdBitsNames = new List<string>();
            for (int i = 0; i < _numberOfBits; i++)
            {
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
            for (int i = 0; i < localAnimationStates.Count; i++)
            {
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
            for (int i = 0; i < remoteAnimationStates.Count; i++)
            {
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
                            a.SetsVolume(currentSyncedAnimation.volume);
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

        private void GenerateReceiveLayer()
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
            //doneState.TrackingTracks(AacAv3.Av3TrackingElement.Eyes); // Most likely don't need to animate the eyes...
            //doneState.TrackingTracks(AacAv3.Av3TrackingElement.Mouth);

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
                    //currentState.TrackingAnimates(AacAv3.Av3TrackingElement.Eyes); // Most likely don't need to animate the eyes...
                    //currentState.TrackingAnimates(AacAv3.Av3TrackingElement.Mouth);
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
            GenerateReceiveLayer();
        }

        /// <summary>
        /// Creates or gets an asset from a path
        /// </summary>
        private T CreateOrLoadAsset<T>(string path)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            return asset;
        }

        private void CreateMenu()
        {
            const int animsPerPage = 7;
            // The last page can contain one more than the usual anims per page, so subtract
            // one from the total. Then use 'divisor minus 1'-trick for a ceiling div.
            int numPages = (_animations.Count + animsPerPage - 2) / animsPerPage;

            // Create a path of folders
            List<string> assetFolderPath = new() { "Assets", "OpenSyncDance", _self.assetKey };
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
            if (!_vrcParams)
                _vrcParams = CreateOrLoadAsset<VRCExpressionParameters>($"{assetFolder}/OSD_Params.asset");

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
                tempParams.Add(new()
                {
                    name = $"paramSendAnimIdBits_{i}",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    saved = false,
                    networkSynced = true,
                    defaultValue = 0,
                });
            }

            _vrcParams.parameters = tempParams.ToArray();

            // Ensure we have enough menus
            for (int pageId = 0; pageId < numPages; pageId++)
            {
                if (_vrcMenus.Count <= pageId)
                    _vrcMenus.Add(null);
                if (!_vrcMenus[pageId])
                    _vrcMenus[pageId] = CreateOrLoadAsset<VRCExpressionsMenu>($"{assetFolder}/OSD_Menu_{pageId}.asset");
                
                // Clear menu!
                _vrcMenus[pageId].controls = new();
            }

            for (int pageId = 0; pageId < numPages - 1; pageId++)
            {
                _vrcMenus[pageId].controls.Add(new VRCExpressionsMenu.Control {
                    icon = null,
                    name = $"Page {pageId + 1}",
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = _vrcMenus[pageId + 1],
                });
            }

            // Setup menus
            for (int pageId = 0, animationId = 0; pageId < numPages; pageId++)
            {
                bool isLastPage = pageId == numPages - 1;
                int animsOnThisPage = animsPerPage + (isLastPage ? 1 : 0);

                // Skip animations that we already put in pages, then take enough to fill the page.
                // Map the taken items to a VRC menu button.
                _vrcMenus[pageId].controls.AddRange(_animations.Skip(animsPerPage * pageId).Take(animsOnThisPage).Select((SyncedAnimation anim) =>
                {
                    animationId++;
                    return new VRCExpressionsMenu.Control
                    {
                        icon = anim.icon,
                        name = anim.name,
                        parameter = new VRCExpressionsMenu.Control.Parameter()
                        {
                            name = _paramSendAnimId.Name,
                        },
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        value = animationId,
                    };
                }));
            }

            AssetDatabase.SaveAssets();
        }
    }
}

#endif