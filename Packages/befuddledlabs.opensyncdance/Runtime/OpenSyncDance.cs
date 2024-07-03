#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;

/*

Pokedance
https://www.youtube.com/watch?v=vikINVCvqCE
8.055
auto

Helltaker
https://www.youtube.com/watch?v=EnDXGQmCz3U
44.867
51.267

Ankha
https://www.youtube.com/watch?v=AFoWM83g-KY
8.743
34.875

Arona
https://www.youtube.com/watch?v=3Zo_1S-iVoA
0.460
auto

SAR
https://www.youtube.com/watch?v=oNxRqHovkBI
0.07
10.442

Badger Badger
https://www.youtube.com/watch?v=EIyixC9NsLI
0.55
30.55

Zufolo Impazzito
https://www.youtube.com/watch?v=STOKzUsSUPY
0.2
28.7

Distraction
https://www.youtube.com/watch?v=ZhFVt5uPdW0
5.761
auto

*/

namespace BefuddledLabs.OpenSyncDance
{
    [Serializable]
    public enum AudioType 
    {
        AudioFile,
        Youtube,
    }

    
    [Serializable]
    public enum AudioSyncMethod
    {
        None,
        ScaleAnimationToAudio,
        ClipAudioToAnimation,
    }
    
    [Serializable]
    public class AnimationAudio {
        public AudioClip audioClip;
        public AudioType audioType;

        public float volume = 1f;

        public string audioUrl;
        public string startTimeStamp;
        public string endTimeStamp;
    }

    [CustomPropertyDrawer(typeof(AnimationAudio))]
    public class AnimationAudioDrawer : PropertyDrawer
    {
        ExtraGUI.GUIBuilderElement GetLayout(SerializedProperty property)
            => ExtraGUI.Builder(property)
                .Draw(x => x
                    .DrawField("audioClip", "audio", GUIStyle.none)
                    .DrawField("audioType", "type", GUIStyle.none)
                    .DrawHorizontally())
                .Draw(x => x
                    .DrawField("audioUrl", "URL", GUIStyle.none)
                    .DrawField("startTimeStamp", "start", GUIStyle.none)
                    .DrawField("endTimeStamp", "end", GUIStyle.none)
                    .DrawHorizontally())
                .DrawVertically();

        public override void OnGUI(Rect space, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(space, label, property);
            
            GetLayout(property).Draw(space);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => GetLayout(property).height;
    }

    [Serializable]
    public class SyncedAnimation
    {
        public AnimationClip animationClip;
        public bool animationUseFootIK;
        public AudioSyncMethod syncMethod;
        public AnimationAudio audio;
    }

    [CustomPropertyDrawer(typeof(SyncedAnimation))]
    public class SyncedAnimationDrawer : PropertyDrawer
    {
        ExtraGUI.GUIBuilderElement GetLayout(SerializedProperty property)
            => ExtraGUI.Builder(property)
                .Draw(x => x
                    .DrawField("animationClip", "animation", GUIStyle.none)
                    .DrawField("animationUseFootIK", "foot ik", GUIStyle.none)
                    .DrawField("syncMethod", "sync", GUIStyle.none)
                    .DrawHorizontally())
                .DrawField("audio", "audio", GUIStyle.none)
                .DrawVertically();

        public override void OnGUI(Rect space, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(space, label, property);

            GetLayout(property).Draw(space);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => GetLayout(property).height;
    }

    [Serializable]
    public class SyncedEmote
    {
        public Texture2D icon;
        public string name;

        public SyncedAnimation entry;
        public SyncedAnimation loop;
        public SyncedAnimation exit;
    }

    [CustomPropertyDrawer(typeof(SyncedEmote))]
    public class SyncedEmoteDrawer : PropertyDrawer
    {
        ExtraGUI.GUIBuilderElement GetLayout(SerializedProperty property)
            => ExtraGUI.Builder(property)
                .Draw(x => x
                    .DrawField("name", "name", GUIStyle.none)
                    .DrawHorizontally())
                .DrawField("entry", "[P]", GUIStyle.none)
                .DrawField("loop", "[L]", GUIStyle.none)
                .DrawField("exit", "[S]", GUIStyle.none)
                .DrawVertically();

        public override void OnGUI(Rect space, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(space, label, property);

            GetLayout(property).Draw(space);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => GetLayout(property).height;
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
        public List<SyncedEmote> animations = new();

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
        private List<SyncedEmote> _animations => _self.animations;

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

        private const float _animDelay = 0.1f;

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
            
            if (DownloadManager.Hasytdlp && DownloadManager.HasFFmpeg) 
            {
                if (GUILayout.Button("Download Missing AudioClips")) 
                {
                    EditorGUI.BeginChangeCheck();
                    foreach (var animations in _self.animations) 
                    {
                        var anims = new [] {
                            animations.entry,
                            animations.loop,
                            animations.exit
                        };

                        foreach (var anim in anims)
                        {
                            if (anim.audio.audioType != AudioType.Youtube)
                                continue;
                            if (anim.audio.audioClip != null)
                                continue;
                            if (anim.animationClip == null)
                                continue;
                            
                            anim.audio.audioClip = DownloadManager.DownloadYouTubeLink(anim);
                        }
                        
                    }
                    if (EditorGUI.EndChangeCheck())
                        EditorUtility.SetDirty(_self);
                }
            }
            else 
            {
                if (GUILayout.Button("Click to download yt-dlp/ffmpeg to use the YouTube audio type.")) 
                    DownloadManager.DownloadBoth();
            }

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

            foreach (var (state, _, param) in Utils.CreateBinarySearchTree(
                new AacFlStateMachineWrapped(localEncode),
                new AacFlIntDecisionParameter(_paramSendAnimId, _numberOfBits)))
            {
                state.State.name = $"Send {param.id}";
                state.Exits().Automatically();

                _paramSendAnimIdBitsList.Zip(param.GetBits(), (p, b) => state.Drives(p, b)).ToList();
            }

            // Remote player decodes sync bits
            var remoteDecode = _bitLayer.NewSubStateMachine("Remote decode");
            entry.TransitionsTo(remoteDecode).When(_bitLayer.BoolParameter("IsLocal").IsFalse());
            remoteDecode.TransitionsTo(remoteDecode);

            foreach (var (state, _, param) in Utils.CreateBinarySearchTree(
                new AacFlStateMachineWrapped(remoteDecode),
                new AacFlBoolGroupDecisionParameter(_paramSendAnimIdBits, _numberOfBits)))
            {
                state.State.name = $"Receive {param.id}";
                state.Drives(_paramSendAnimId, param.id);
                state.Exits().Automatically();
            }
        }

        private void SetCommonAudioSettings(AacVRCFlEditAnimatorPlayAudio audio) 
        {
            audio.PlayAudio.PlayOnEnter = true;
            audio.PlayAudio.StopOnExit = true;
        }

        private void GenerateSendLayer()
        {
            var readyState = _sendLayer.NewState("Ready");
            var exitState = _sendLayer.NewState("Done");

            readyState.TransitionsFromEntry();
            exitState.Exits().Automatically().WithTransitionDurationSeconds(0.2f);

            for (int i = 1; i < _animations.Count + 1; i++)
            {
                var currentSyncedAnimation = _animations[i - 1];
                var danceState = _sendLayer.NewState($"Dance {currentSyncedAnimation.name}");
                
                var entryMusicState = _sendLayer.NewState($"Entry Music {currentSyncedAnimation.name}");
                var loopMusicState = _sendLayer.NewState($"Loop Music {currentSyncedAnimation.name}");
                var exitMusicState = _sendLayer.NewState($"Exit Music {currentSyncedAnimation.name}");

                // Set the audio clip on the audio source
                if (currentSyncedAnimation.entry.audio.audioClip != null)
                {
                    entryMusicState.Audio(_audioSource, (a) =>
                        {
                            SetCommonAudioSettings(a);
                            a.StartsPlayingOnEnterAfterSeconds(_animDelay);
                            a.SelectsClip(VRC_AnimatorPlayAudio.Order.Roundabout,
                                new[] { currentSyncedAnimation.entry.audio.audioClip });
                            a.SetsVolume(0);
                        });
                }
                if (currentSyncedAnimation.loop.audio.audioClip != null)
                {
                    loopMusicState.Audio(_audioSource, (a) =>
                        {
                            SetCommonAudioSettings(a);
                            a.SelectsClip(VRC_AnimatorPlayAudio.Order.Roundabout,
                                new[] { currentSyncedAnimation.loop.audio.audioClip });
                            a.SetsLooping();
                            a.SetsVolume(currentSyncedAnimation.loop.audio.volume);
                        });
                }
                if (currentSyncedAnimation.exit.audio.audioClip != null)
                {
                    exitMusicState.Audio(_audioSource, (a) =>
                        {
                            SetCommonAudioSettings(a);
                            a.SelectsClip(VRC_AnimatorPlayAudio.Order.Roundabout,
                                new[] { currentSyncedAnimation.exit.audio.audioClip });
                            a.SetsVolume(currentSyncedAnimation.loop.audio.volume);
                        });
                }

                readyState.TransitionsTo(danceState).When(_paramSendAnimId.IsEqualTo(i));
                var musicConditions = danceState.TransitionsTo(entryMusicState).WhenConditions();

                var recvParamNames = _paramRecvBits.ToList();

                void ToggleBits(AacFlEditClip a) 
                {
                    for (int j = 0; j < recvParamNames.Count; j++)
                    {
                        var wantedParamState = (i & (1 << j)) > 0;
                        musicConditions = musicConditions.And(recvParamNames[j].IsEqualTo(wantedParamState));
                        a.Animates(_contactSenders[j].gameObject).WithOneFrame(wantedParamState ? 1 : 0);
                    }
                }

                var toggleClip = _aac.NewClip().Animating(a => ToggleBits(a));

                danceState.WithAnimation(toggleClip);
                entryMusicState.WithAnimation(_aac.NewClip().Animating(a => {
                    ToggleBits(a);
                    var volume = a.Animates(_audioSource, "m_Volume");
                    volume.WithUnit(AacFlUnit.Seconds, (AacFlSettingKeyframes key) => {
                        key.Linear(0.0f, 0.0f);
                        key.Linear(0.2f, currentSyncedAnimation.entry.audio.volume);
                    });
                }));
                loopMusicState.WithAnimation(toggleClip);
                exitMusicState.WithAnimation(toggleClip);

                entryMusicState.TransitionsTo(loopMusicState).Automatically();
                loopMusicState.TransitionsTo(exitMusicState).When(_paramSendAnimId.IsNotEqualTo(i));
                exitMusicState.TransitionsTo(exitState).Automatically();
            }
        }

        private void GenerateReceiveLayer()
        {
            var readyState = _recvLayer.NewState("Ready");
            var danceState = _recvLayer.NewSubStateMachine("Dance");

            // Ugly thing to not IK sync the animation
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.Head);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.LeftHand);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.RightHand);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.Hip);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.LeftFoot);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.RightFoot);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.LeftFingers);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.RightFingers);

            danceState.PlayableEnables(VRC_PlayableLayerControl.BlendableLayer.Action);
            readyState.PlayableDisables(VRC_PlayableLayerControl.BlendableLayer.Action);

            // Transition to dance blend tree whenever an animation is triggered
            readyState.TransitionsFromEntry();
            readyState.TransitionsTo(danceState).When(_paramRecvBits.IsAnyTrue());
            danceState.TransitionsTo(readyState);

            var paramRecvBitsWrapped = new AacFlBoolGroupDecisionParameter(_paramRecvBits, _numberOfBits);
            foreach (var (entryState, parent, param) in Utils.CreateBinarySearchTree(new AacFlStateMachineWrapped(danceState), paramRecvBitsWrapped))
            {
                var loopState = parent.NewState("loopState");
                var exitState = parent.NewState("exitState");
                loopState.TransitionsTo(exitState).When(param.ExitCondition);
                exitState.Exits().Automatically();

                if (param.id == 0)
                    continue;
                if (param.id > _animations.Count)
                    break;

                var item = _animations[param.id - 1];

                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.Head);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftHand);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.RightHand);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.Hip);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftFoot);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.RightFoot);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftFingers);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.RightFingers);

                if (item.entry.animationClip != null)
                    entryState.WithAnimation(item.entry.animationClip);
                if (item.loop.animationClip != null)
                    loopState.WithAnimation(item.loop.animationClip);
                if (item.exit.animationClip != null)
                    exitState.WithAnimation(item.exit.animationClip);
            }
        }

        private void Generate()
        {
            // Destroy children >:)
            // TODO: add method to keep certain objects for e.g. props that may be used
            var contactContainer = _self.transform.Find("OSD_Contacts")?.gameObject;
            while (contactContainer != null) {
                DestroyImmediate(contactContainer);
                contactContainer = _self.transform.Find("OSD_Contacts")?.gameObject;
            }

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
                _vrcMenus[pageId].controls.AddRange(_animations.Skip(animsPerPage * pageId).Take(animsOnThisPage).Select((SyncedEmote anim) =>
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