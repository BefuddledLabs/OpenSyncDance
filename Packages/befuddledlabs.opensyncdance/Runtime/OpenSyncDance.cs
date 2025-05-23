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
    public static class GUIStyle
    {
        public static bool IsGay;
        public static float GaySpeed = 0.25f;
        
        public static int LabelWidth = 50;

        private static Color32 GetGayColor(float offset)
        {
            var time = (float)EditorApplication.timeSinceStartup * GaySpeed + offset;
            return Color.HSVToRGB(time - Mathf.Floor(time), 0.5f, 1f);
        }

        public static UnityEngine.GUIStyle Light => new()
            { normal = new GUIStyleState { textColor = IsGay ? GetGayColor(0f) : new Color32(0xF6, 0xBA, 0xFB, 0xFF) } };

        public static UnityEngine.GUIStyle Mid => new()
            { normal = new GUIStyleState { textColor = IsGay ? GetGayColor(0.2f) : new Color32(0xE2, 0x6C, 0xD7, 0xFF) } };

        public static UnityEngine.GUIStyle Dark => new()
            { normal = new GUIStyleState { textColor = IsGay ? GetGayColor(0.4f) : new Color32(0x97, 0x29, 0xFC, 0xFF) } };

        public static UnityEngine.GUIStyle Black => new()
            { normal = new GUIStyleState { textColor = IsGay ? GetGayColor(0.6f) : new Color32(0x11, 0x00, 0x2A, 0xFF) } };
    }

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
    public class AnimationAudio
    {
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
        {
            var ui = ExtraGUI.Builder(property);
            ui.Draw(x => x
                    .DrawField("audioClip", "audio", GUIStyle.Mid)
                    .DrawEmpty()
                    .DrawField("audioType", "type", GUIStyle.Mid)
                    .DrawHorizontally());
            ui.Draw(x => x
                .DrawSlider("volume", 0f, 1f, "volume", GUIStyle.Dark)
                .DrawHorizontally());
            
            if ((AudioType)property.FindPropertyRelative("audioType").boxedValue == AudioType.Youtube)
            {
                ui.Draw(x => x
                    .DrawField("audioUrl", "URL", GUIStyle.Dark)
                    .DrawField("startTimeStamp", "start", GUIStyle.Dark)
                    .DrawField("endTimeStamp", "end", GUIStyle.Dark)
                    .DrawHorizontally());
            }

            return ui.DrawVertically();
        }

        public override void OnGUI(Rect space, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(space, label, property);
            EditorGUIUtility.labelWidth = GUIStyle.LabelWidth;

            GetLayout(property).Draw(space);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => GetLayout(property).height;
    }

    [Serializable]
    public class SyncedAnimation
    {
        public bool expanded;

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
                    .DrawField("animationClip", "anim", GUIStyle.Light)
                    .DrawField("animationUseFootIK", "foot ik", GUIStyle.Light)
                    .DrawField("syncMethod", "sync", GUIStyle.Light)
                    .DrawHorizontally())
                .DrawField("audio", "audio", UnityEngine.GUIStyle.none)
                .DrawFoldout("expanded");

        public override void OnGUI(Rect space, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(space, label, property);
            EditorGUIUtility.labelWidth = GUIStyle.LabelWidth;

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

        public bool expanded;

        public SyncedAnimation entry;
        public SyncedAnimation loop;
        public SyncedAnimation exit;
    }

    [CustomPropertyDrawer(typeof(SyncedEmote))]
    public class SyncedEmoteDrawer : PropertyDrawer
    {
        ExtraGUI.GUIBuilderElement GetLayout(SerializedProperty property)
        {
            return ExtraGUI.Builder(property)
                .Draw(x => x
                    .DrawField("name", "name", GUIStyle.Dark)
                    .DrawField("icon", "icon", GUIStyle.Dark)
                    .DrawHorizontally())
                .DrawField("entry", "Entry", GUIStyle.Mid)
                .DrawField("loop", "Loop", GUIStyle.Mid)
                .DrawField("exit", "Exit", GUIStyle.Mid)
                .DrawFoldout("expanded", property.FindPropertyRelative("name").stringValue);
        }

        public override void OnGUI(Rect space, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(space, label, property);
            EditorGUIUtility.labelWidth = GUIStyle.LabelWidth;

            GetLayout(property).Draw(space);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => GetLayout(property).height + 4;
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
        [NonReorderable] public VRCExpressionParameters vrcExpressionParameters;

        /// <summary>
        /// The VRC menu assets that we will overwrite. If not set, will generate a new one.
        /// </summary>
        public List<VRCExpressionsMenu> vrcExpressionMenus = new();

        /// <summary>
        /// Enables volume control in the menu/parameters
        /// </summary>
        public bool volumeControl = true;
        
        /// <summary>
        /// The default volume of the audio source, mainly intended for volumeControl
        /// </summary>
        public float defaultVolume = 1f;

        /// <summary>
        /// Makes sure that the required classes are initialized.
        /// </summary>
        public void EnsureInitialized()
        {
            animations ??= new();
            vrcExpressionMenus ??= new();
        }


        /// Generation code
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
        /// Layer that calculates and applies the volume of the current song
        /// </summary>
        private AacFlLayer _volumeLayer;

        /// <summary>
        /// Layer that manages syncing the current animation with the fewest amount of bits needed.
        /// </summary>
        private AacFlLayer _bitLayer;

        /// <summary>
        /// The number of bits that are used to sync with other players.
        /// </summary>
        private int _numberOfBits => Utils.NumberOfBitsToRepresent(animations.Count);

        /// <summary>
        /// Parameter format of the contact bit receiver. This value needs to be formatted with <tt>string.Format</tt>.
        /// </summary>
        private string _recvBitParamName => contactPrefix + "OSD_RecvBit{0}";

        /// <summary>
        /// Parameter name of the animation id that we want to broadcast.
        /// </summary>
        private string _sendAnimIdName = "OSD_SendAnim";

        // TODO: make this a variable that you can set from ui whatever idk??? lol --nara
        /// <summary>
        /// Component that gets plays the audio.
        /// </summary>
        private AudioSource _audioSource => GetComponentInChildren<AudioSource>();

        private List<VRCContactReceiver> _contactReceivers;
        private List<VRCContactSender> _contactSenders;

        private AnimatorController _animationControllerFX => animatorControllerFX;
        private AacFlBoolParameterGroup _paramRecvBits;
        private AacFlBoolParameterGroup _paramSendBits;
        private AacFlIntParameter _paramSendAnimId;
        private AacFlBoolParameterGroup _paramSendAnimIdBits;

        private const float _animDelay = 0.1f;

        internal void AnimatorSetup()
        {
            _aac = AacV1.Create(new AacConfiguration
            {
                SystemName = "OpenSyncDance",
                AnimatorRoot = transform,
                DefaultValueRoot = transform,
                AssetKey = assetKey,
                AssetContainer = _animationControllerFX,
                ContainerMode = AacConfiguration.Container.Everything,
                DefaultsProvider = new AacDefaultsProvider(true),
            });

            _aac.ClearPreviousAssets();

            _recvLayer = _aac.CreateSupportingArbitraryControllerLayer(animatorControllerAction, "recvLayer");
            _sendLayer = _aac.CreateSupportingArbitraryControllerLayer(_animationControllerFX, "sendLayer");
            _volumeLayer = _aac.CreateSupportingArbitraryControllerLayer(_animationControllerFX, "volumeLayer");
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
            _sendLayer.BoolParameters(receiveParamNames
                .ToArray()); // Ugly thing to get the same params on the Action animator
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

        private void GenerateVolumeLayer()
        {
            var volumeParam = _volumeLayer.FloatParameter("OSD_Volume");
            var songVolumeParam = _volumeLayer.FloatParameter("OSD_Song_Volume");

            var blendTree = _aac.NewBlendTree();
            blendTree.Direct().WithAnimation(_aac.NewBlendTree().Direct().WithAnimation(_aac.NewClip().Animating(a => {
                a.Animates(_audioSource, "m_Volume").WithFrameCountUnit(
                    f => {
                        f.Linear(0, 1);
                        f.Linear(1, 1);
                    });
            }), songVolumeParam), volumeParam);
            
            var blendTreeState = _volumeLayer.NewState("Volume BlendTree");
            blendTreeState.WithAnimation(blendTree);
        }

        private void GenerateSendLayer()
        {
            var readyState = _sendLayer.NewState("Ready");
            var lockState = _sendLayer.NewState("Lock");
            var exitState = _sendLayer.NewState("Done");
            
            var enabled = _sendLayer.BoolParameter("OSD_Enabled");
            var songVolumeParam = _sendLayer.FloatParameter("OSD_Song_Volume");

            exitState.Drives(songVolumeParam, 0);

            readyState.TransitionsTo(lockState).When(_paramRecvBits.IsAnyTrue());
            lockState.TransitionsTo(readyState).When(_paramRecvBits.AreFalse());
            exitState.Exits().Automatically().WithTransitionDurationSeconds(0.2f);

            for (int i = 1; i < animations.Count + 1; i++)
            {
                var currentSyncedAnimation = animations[i - 1];
                var danceState = _sendLayer.NewState($"Dance {currentSyncedAnimation.name}");


                var entryMusicState = _sendLayer.NewState($"Entry Music {currentSyncedAnimation.name}");
                var loopMusicState = _sendLayer.NewState($"Loop Music {currentSyncedAnimation.name}");
                var exitMusicState = _sendLayer.NewState($"Exit Music {currentSyncedAnimation.name}");

                // Set the audio clip on the audio source
                if (currentSyncedAnimation.entry.audio.audioClip && _audioSource)
                {
                    entryMusicState.Audio(_audioSource, (a) =>
                    {
                        SetCommonAudioSettings(a);
                        a.StartsPlayingOnEnterAfterSeconds(_animDelay);
                        a.SelectsClip(VRC_AnimatorPlayAudio.Order.Roundabout,
                            new[] { currentSyncedAnimation.entry.audio.audioClip });
                    });
                }

                if (currentSyncedAnimation.loop.audio.audioClip && _audioSource)
                {
                    loopMusicState.Audio(_audioSource, (a) =>
                    {
                        SetCommonAudioSettings(a);
                        a.SelectsClip(VRC_AnimatorPlayAudio.Order.Roundabout,
                            new[] { currentSyncedAnimation.loop.audio.audioClip });
                        a.SetsLooping();
                    });
                }

                if (currentSyncedAnimation.exit.audio.audioClip && _audioSource)
                {
                    exitMusicState.Audio(_audioSource, (a) =>
                    {
                        SetCommonAudioSettings(a);
                        a.SelectsClip(VRC_AnimatorPlayAudio.Order.Roundabout,
                            new[] { currentSyncedAnimation.exit.audio.audioClip });
                    });
                }

                readyState.TransitionsTo(danceState).When(_paramSendAnimId.IsEqualTo(i)).And(enabled.IsTrue());
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

                void SoundAnimation(AacFlEditClip a, SyncedAnimation an, float startVolume)
                {
                    if (_audioSource) {
                        var volume = a.AnimatesAnimator(songVolumeParam);
                        volume.WithUnit(AacFlUnit.Seconds, key => {
                            if (startVolume >= 0) {
                                var endTime = 0.2f;
                                if (an.animationClip)
                                    endTime = Mathf.Min(an.animationClip.length, endTime);
                                key.Linear(0f, startVolume);
                                key.Linear(endTime, an.audio.volume);
                            }
                            else
                            {
                                key.Linear(0f, an.audio.volume);
                            }
                        });
                    }
                }

                AacFlClip CreateClip(SyncedAnimation an, float startVolume = -1) =>
                    _aac.NewClip().Animating(a => {
                        ToggleBits(a);
                        SoundAnimation(a, an, startVolume);
                    });
                
                danceState.WithAnimation(_aac.NewClip().Animating(ToggleBits));

                entryMusicState.WithAnimation(CreateClip(currentSyncedAnimation.entry, 0));
                loopMusicState.WithAnimation(CreateClip(currentSyncedAnimation.loop));
                exitMusicState.WithAnimation(CreateClip(currentSyncedAnimation.exit));
                
                entryMusicState.Drives(songVolumeParam, currentSyncedAnimation.entry.audio.volume);
                loopMusicState.Drives(songVolumeParam, currentSyncedAnimation.loop.audio.volume);
                exitMusicState.Drives(songVolumeParam, currentSyncedAnimation.exit.audio.volume);

                // anim entry (with early exit)
                entryMusicState.TransitionsTo(exitMusicState).When(_paramSendAnimId.IsNotEqualTo(i));
                entryMusicState.TransitionsTo(loopMusicState).Automatically();

                // anim loop
                loopMusicState.TransitionsTo(exitMusicState).When(_paramSendAnimId.IsNotEqualTo(i));

                // anim exit
                exitMusicState.TransitionsTo(exitState).Automatically();
            }
        }

        private void GenerateSoundEnableLayer()
        {
            // This is your center left audio channel
            if (!_audioSource) // Quest pee pee poo poo
                return;
            
            var toggleLayer = _aac.CreateSupportingArbitraryControllerLayer(_animationControllerFX, "SoundToggle");
            var soundParam = toggleLayer.BoolParameter("OSD_Sound");

            var on = toggleLayer.NewState("On");
            var inBetween = toggleLayer.NewState("In between");
            var off = toggleLayer.NewState("Off");

            on.WithAnimation(_aac.NewClip().Toggling(_audioSource.gameObject, true));
            inBetween.WithAnimation(_aac.NewClip().Toggling(_audioSource.gameObject, true));
            off.WithAnimation(_aac.NewClip().Toggling(_audioSource.gameObject, false));

            inBetween.Audio(_audioSource, a =>
            {
                a.StopsPlayingOnEnter();
                a.StopsPlayingOnExit();
            });

            on.TransitionsTo(inBetween).When(soundParam.IsFalse());
            off.TransitionsTo(inBetween).When(soundParam.IsTrue()).And(_paramSendAnimId.IsEqualTo(0));

            inBetween.TransitionsTo(off).When(soundParam.IsFalse());
            inBetween.TransitionsTo(on).When(soundParam.IsTrue());
        }

        private void GenerateReceiveLayer()
        {
            var readyState = _recvLayer.NewState("Ready");
            var lockState = _recvLayer.NewState("Lock");
            var danceState = _recvLayer.NewSubStateMachine("Dance");

            var enabled = _recvLayer.BoolParameter("OSD_Enabled");

            readyState.TransitionsTo(lockState).When(enabled.IsFalse());
            lockState.TransitionsTo(readyState).When(enabled.IsTrue());

            // Ugly thing to not IK sync the animation :3
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.Head);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.LeftHand);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.RightHand);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.Hip);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.LeftFoot);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.RightFoot);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.LeftFingers);
            readyState.TrackingTracks(AacAv3.Av3TrackingElement.RightFingers);

            readyState.PlayableDisables(VRC_PlayableLayerControl.BlendableLayer.Action);

            // Transition to dance blend tree whenever an animation is triggered
            readyState.TransitionsFromEntry();
            readyState.TransitionsTo(danceState).When(_paramRecvBits.IsAnyTrue());
            danceState.TransitionsTo(readyState);

            var paramRecvBitsWrapped = new AacFlBoolGroupDecisionParameter(_paramRecvBits, _numberOfBits);
            foreach (var (waitState, parent, param) in Utils.CreateBinarySearchTree(
                         new AacFlStateMachineWrapped(danceState), paramRecvBitsWrapped))
            {
                var waitDoneState = parent.NewState("waitState");
                var entryState = parent.NewState("entryState");
                var loopState = parent.NewState("loopState");
                var exitState = parent.NewState("exitState");

                entryState.TransitionsTo(exitState).When(param.ExitCondition);
                entryState.TransitionsTo(loopState).Automatically();
                loopState.TransitionsTo(exitState).When(param.ExitCondition);
                exitState.Exits().Automatically();

                if (param.id == 0)
                    continue;
                if (param.id > animations.Count)
                    break;

                var item = animations[param.id - 1];

                // HACK: For some reason VRChat's play audio state behaviour has a delay.
                // We delay the action animation to match with the delayed audio.
                waitState.TransitionsTo(waitDoneState).WithTransitionDurationSeconds(0.25f)
                    .When(_recvLayer.BoolParameter("false").IsFalse());
                waitDoneState.TransitionsTo(entryState).Automatically();

                entryState.PlayableEnables(VRC_PlayableLayerControl.BlendableLayer.Action);

                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.Head);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftHand);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.RightHand);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.Hip);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftFoot);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.RightFoot);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.LeftFingers);
                entryState.TrackingAnimates(AacAv3.Av3TrackingElement.RightFingers);

                entryState.State.iKOnFeet = item.entry.animationUseFootIK;
                loopState.State.iKOnFeet = item.loop.animationUseFootIK;
                exitState.State.iKOnFeet = item.exit.animationUseFootIK;

                if (item.entry.animationClip)
                    entryState.WithAnimation(item.entry.animationClip);
                if (item.loop.animationClip)
                    loopState.WithAnimation(item.loop.animationClip);
                if (item.exit.animationClip)
                    exitState.WithAnimation(item.exit.animationClip);
            }
        }

        internal void GenerateAll()
        {
            // TODO: create new animation controller if it doesn't exist
            if (!animatorControllerFX)
                throw new ArgumentNullException();
            if (!animatorControllerAction)
                throw new ArgumentNullException();

            AnimatorSetup();
            CreateMenu();
            Generate();
        }

        internal void Generate()
        {
            // Destroy children >:)
            // TODO: add method to keep certain objects for e.g. props that may be used
            var contactContainer = transform.Find("OSD_Contacts")?.gameObject;
            while (contactContainer)
            {
                DestroyImmediate(contactContainer);
                contactContainer = transform.Find("OSD_Contacts")?.gameObject;
            }

            // Create contacts root object to hold
            contactContainer = new GameObject("OSD_Contacts");
            contactContainer.transform.parent = transform;

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
            GenerateVolumeLayer();
            GenerateSendLayer();
            GenerateReceiveLayer();
            GenerateSoundEnableLayer();
        }

        /// <summary>
        /// Creates or gets an asset from a path
        /// </summary>
        private T CreateOrLoadAsset<T>(string path)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset)
                return asset;

            asset = (T)ScriptableObject.CreateInstance(typeof(T));
            AssetDatabase.CreateAsset(asset, path);

            return asset;
        }

        internal void CreateMenu()
        {
            const int animsPerPage = 7;
            // The last page can contain one more than the usual anims per page, so subtract
            // one from the total. Then use 'divisor minus 1'-trick for a ceiling div.
            int numPages = (animations.Count + animsPerPage - 2 + 2) / animsPerPage;

            // Create a path of folders
            List<string> assetFolderPath = new() { "Assets", "OpenSyncDance", assetKey };
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
            if (!vrcExpressionParameters)
                vrcExpressionParameters = CreateOrLoadAsset<VRCExpressionParameters>($"{assetFolder}/OSD_Params.asset");

            //vrcParams.parameters 
            var tempParams = new List<VRCExpressionParameters.Parameter>
            {
                new()
                {
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

            tempParams.Add(new()
            {
                name = "OSD_Enabled",
                valueType = VRCExpressionParameters.ValueType.Bool,
                saved = true,
                networkSynced = true,
                defaultValue = 1,
            });

            tempParams.Add(new()
            {
                name = "OSD_Sound",
                valueType = VRCExpressionParameters.ValueType.Bool,
                saved = true,
                networkSynced = true,
                defaultValue = 1,
            });


            tempParams.Add(new()
            {
                name = "OSD_Volume",
                valueType = VRCExpressionParameters.ValueType.Float,
                saved = true,
                networkSynced = volumeControl, // doesn't count towards bits if not synced
                defaultValue = defaultVolume,
            });


            vrcExpressionParameters.parameters = tempParams.ToArray();

            // Ensure we have enough menus
            for (int pageId = 0; pageId < numPages; pageId++)
            {
                if (vrcExpressionMenus.Count <= pageId)
                    vrcExpressionMenus.Add(null);
                if (!vrcExpressionMenus[pageId])
                    vrcExpressionMenus[pageId] =
                        CreateOrLoadAsset<VRCExpressionsMenu>($"{assetFolder}/OSD_Menu_{pageId}.asset");

                // Clear menu!
                vrcExpressionMenus[pageId].controls = new();
            }

            for (int pageId = 0; pageId < numPages - 1; pageId++)
            {
                vrcExpressionMenus[pageId].controls.Add(new VRCExpressionsMenu.Control
                {
                    icon = null,
                    name = $"Page {pageId + 1}",
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = vrcExpressionMenus[pageId + 1],
                });
            }

            // Enable Toggle
            vrcExpressionMenus[0].controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Enabled",
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = "OSD_Enabled",
                },
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                value = 1,
            });

            // Enable Sound
            vrcExpressionMenus[0].controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Sound",
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = "OSD_Sound",
                },
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                value = 1,
            });

            if (volumeControl)
            {
                vrcExpressionMenus[0].controls.Add(new VRCExpressionsMenu.Control
                {
                    name = "Volume",
                    subParameters = new [] {new VRCExpressionsMenu.Control.Parameter()
                    {
                        name = "OSD_Volume",
                    }},
                    type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                    value = defaultVolume,
                });
            }

            int firstPageInitialControlCount = vrcExpressionMenus[0].controls.Count;

            // Setup menus
            var totalAnims = 0;
            for (int pageId = 0, animationId = 0; pageId < numPages; pageId++)
            {
                bool isLastPage = pageId == numPages - 1;
                bool isFirstPage = pageId == 0;
                int animsOnThisPage = animsPerPage + (isLastPage ? 1 : 0) - (isFirstPage ? (firstPageInitialControlCount-1) : 0);

                // Skip animations that we already put in pages, then take enough to fill the page.
                // Map the taken items to a VRC menu button.
                vrcExpressionMenus[pageId].controls.AddRange(animations.Skip(totalAnims).Take(animsOnThisPage).Select(
                    (SyncedEmote anim) =>
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

                totalAnims += animsOnThisPage;
            }

            AssetDatabase.SaveAssets();
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
        
        /// <summary>
        /// The menu is now gay
        /// </summary>
        public static bool IsGay;

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

            var contactPrefixProperty = serializedObject.FindProperty("contactPrefix");
            EditorGUILayout.PropertyField(contactPrefixProperty, true);

            var assetContainerProperty = serializedObject.FindProperty("animatorControllerAction");
            EditorGUILayout.PropertyField(assetContainerProperty, true);

            assetContainerProperty = serializedObject.FindProperty("animatorControllerFX");
            EditorGUILayout.PropertyField(assetContainerProperty, true);

            var emoteProperty = serializedObject.FindProperty("animations");
            EditorGUILayout.PropertyField(emoteProperty, true);
            
            GUILayout.BeginHorizontal();
            
            var volumeControlProperty = serializedObject.FindProperty("volumeControl");
            EditorGUILayout.PropertyField(volumeControlProperty, true);
            GUILayout.Label("Uses 8 bits when enabled.");

            GUILayout.EndHorizontal();
            
            var defaultVolumeProperty = serializedObject.FindProperty("defaultVolume");
            EditorGUILayout.Slider(defaultVolumeProperty, 0f, 1f);

            // Advanced settings for smarty pants. You probably don't need this.
            // ReSharper disable once AssignmentInConditionalExpression
            if (_uiAdvancedFoldoutState =
                EditorGUILayout.BeginFoldoutHeaderGroup(_uiAdvancedFoldoutState, "Advanced"))
            {
                GUIStyle.IsGay = EditorGUILayout.Toggle("colorful menu", GUIStyle.IsGay);
                
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

            if (DownloadManager.Hasytdlp && DownloadManager.HasFFmpeg)
            {
                if (GUILayout.Button("Clear Downloaded AudioClips"))
                    DownloadManager.ClearAudioFolder();
                if (GUILayout.Button("Download Missing AudioClips"))
                {
                    EditorGUI.BeginChangeCheck();
                    for (var index = 0; index < emoteProperty.arraySize; index++)
                    {
                        var syncedEmote = emoteProperty.GetArrayElementAtIndex(index);
                        var anims = new[]
                        {
                            syncedEmote.FindPropertyRelative("entry"),
                            syncedEmote.FindPropertyRelative("loop"),
                            syncedEmote.FindPropertyRelative("exit"),
                        };

                        foreach (var anim in anims)
                        {
                            var animObject = (SyncedAnimation)anim.boxedValue;
                            if (animObject.audio.audioType != AudioType.Youtube)
                                continue;
                            if (animObject.audio.audioClip)
                                continue;
                            if (!animObject.animationClip)
                                continue;

                            anim.FindPropertyRelative("audio").FindPropertyRelative("audioClip").boxedValue =
                                DownloadManager.DownloadYouTubeLink(animObject);
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

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Generate!"))
            {
                _self.GenerateAll();
            }

            if (_self.vrcExpressionParameters)
                EditorGUILayout.HelpBox(
                    "Expression parameter is set, this will be overwritten which may remove any previous set parameters!",
                    MessageType.Info);
            if (_self.vrcExpressionMenus == null || _self.vrcExpressionMenus.Count > 0)
                EditorGUILayout.HelpBox(
                    "Expression menus are set, this will be overwritten which may remove any previous set parameters!",
                    MessageType.Info);
        }
    }
}

#endif
