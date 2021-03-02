using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace Scream.UniUIP
{
    [RequireComponent(typeof(Animator))]
    public class UIPlayable : MonoBehaviour
    {

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            AnimatorController preview = Animator.runtimeAnimatorController as AnimatorController;
            if (!preview || preview.name != "UIPlayable Preview")
            {
                preview = new AnimatorController();
                preview.name = "UIPlayable Preview";
                preview.hideFlags = HideFlags.DontSave;
                preview.AddLayer("Base Layer");
                preview.AddLayer("Preview Layer");
                preview.layers[1].defaultWeight = 0;

                Animator.runtimeAnimatorController = preview;
            }

            foreach (var childAnimatorState in preview.layers[1].stateMachine.states)
            {
                var animatorState = childAnimatorState.state;
                bool exist = States.Any(state => state.Animation == animatorState.motion || state.LoopAnimation == animatorState.motion);
                if (!exist)
                {
                    preview.RemoveLayer(1);
                    preview.AddLayer("Preview Layer");
                    preview.layers[1].defaultWeight = 0;
                    break;
                }
            }
            foreach (State state in States)
            {
                if (state.Animation) AddMotionIfNotExist(preview, state.Animation);
                if (state.LoopAnimation) AddMotionIfNotExist(preview, state.LoopAnimation);
            }
        }

        static void AddMotionIfNotExist(AnimatorController preview, AnimationClip clip)
        {
            bool exist = preview.animationClips.Any(controllerClip => controllerClip == clip);
            if (!exist)
            {
                var motion = preview.AddMotion(clip, 1);
                motion.name = clip.name;
            }
        }

#endif
        public List<State> States = new List<State>();
        
        public Dictionary<string, State> StatesDic = new Dictionary<string, State>();

        public StateAnimationType DefaultStateAnimation;

        Animator m_Animator;

        public Animator Animator
        {
            get
            {
                if (m_Animator == null)
                    m_Animator = GetComponent<Animator>();
                return m_Animator;
            }
        }

        public State CurrentState => m_PlayingStateInfo?.State ?? null;

        public State DefaultState
        {
            get => States.Find(state => state.IsDefaultState);
            set
            {
                States.ForEach(s => s.IsDefaultState = false);
                if (value != null)
                    value.IsDefaultState = true;
            }
        }

        PlayableGraph m_PlayableGraph;

        AnimationPlayableOutput m_PlayableOutput;

        AnimationMixerPlayable m_RootMixerPlayable;

        PlayingStateInfo m_PlayingStateInfo;

        public virtual void Play(State state)
        {
            if (m_PlayableGraph.IsValid())
            {
                if (m_PlayingStateInfo != null)
                    m_PlayingStateInfo.Destroy();
                m_PlayingStateInfo = new PlayingStateInfo(m_PlayableGraph, m_RootMixerPlayable, state);
                Update();
            }
        }

        public void Play(State state, UnityAction onEnterAction)
        {
            if (onEnterAction != null)
                state.OnAnimationEndDisposable.AddListener(onEnterAction);
            Play(state);
        }

        public void Play(string stateName)
        {
            State state = States.FirstOrDefault(state_2 => state_2.Name == stateName);
            Assert.IsNotNull(state);
            Play(state);
        }

        public void Play(string stateName, UnityAction onEnterAction)
        {
            State state = States.FirstOrDefault(state_2 => state_2.Name == stateName);
            Assert.IsNotNull(state);
            Play(state, onEnterAction);
        }

        protected virtual void OnEnable()
        {
            CreateGraph();

            if (DefaultState != null)
            {
                Play(DefaultState);
                if (DefaultStateAnimation == StateAnimationType.Loop)
                    m_PlayingStateInfo.PlayLoopAnimation();
            }
        }

        protected virtual void OnDisable()
        {
            m_PlayingStateInfo = null;
            m_PlayableGraph.Destroy();
        }

        protected virtual void Update()
        {
            if (m_PlayingStateInfo != null)
            {
                if (!m_PlayingStateInfo.IsLooping)
                {
                    if (!m_PlayingStateInfo.State.Animation || m_PlayingStateInfo.ClipPlayable.GetTime() >= m_PlayingStateInfo.State.Animation.length)
                    {
                        m_PlayingStateInfo.PlayLoopAnimation();
                        OnPlay(m_PlayingStateInfo.State);
                    }
                }
            }
        }

        protected virtual void OnPlay(State state)
        {
            state.OnAnimationEnd.Invoke();
            state.OnAnimationEndDisposable.Invoke();
            state.OnAnimationEndDisposable.RemoveAllListeners();
        }

        void CreateGraph()
        {
            m_PlayableGraph = PlayableGraph.Create(name);
            m_PlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            m_PlayableOutput = AnimationPlayableOutput.Create(m_PlayableGraph, "Animation", Animator);
            m_RootMixerPlayable = AnimationMixerPlayable.Create(m_PlayableGraph, 1);
            m_PlayableOutput.SetSourcePlayable(m_RootMixerPlayable);

            m_PlayableGraph.Play();
        }

        void Awake() => States.ForEach(st => StatesDic.Add(st.Name, st));

        public enum StateAnimationType
        {
            Enter,
            Loop,
        }

    }



    [Serializable]
    public class State
    {
        public string Name;
        public bool IsDefaultState;
        [FormerlySerializedAs("enterAnimation")]
        public AnimationClip Animation;
        public AnimationClip LoopAnimation;
        [FormerlySerializedAs("onEnter")]
        public UnityEvent OnAnimationEnd = new UnityEvent();
        [HideInInspector] public UnityEvent OnAnimationEndDisposable = new UnityEvent();
    }

    [Serializable]
    public class PlayingStateInfo
    {
        public AnimationMixerPlayable RootMixerPlayable;
        public bool IsLooping;
        public PlayableGraph PlayableGraph;
        public State State;
        public AnimationClipPlayable ClipPlayable;

        public void PlayLoopAnimation()
        {
            IsLooping = true;

            if (State.LoopAnimation)
            {
                ClipPlayable.Destroy();
                ClipPlayable = AnimationClipPlayable.Create(PlayableGraph, State.LoopAnimation);
                PlayableGraph.Connect(ClipPlayable, 0, RootMixerPlayable, 0);
            }
            else if (State.Animation)
                ClipPlayable.SetTime(State.Animation.length);
        }

        public void Destroy() => ClipPlayable.Destroy();

        public PlayingStateInfo(PlayableGraph playableGraph, AnimationMixerPlayable rootMixerPlayable, State state)
        {
            State = state;
            PlayableGraph = playableGraph;
            RootMixerPlayable = rootMixerPlayable;

            ClipPlayable = AnimationClipPlayable.Create(playableGraph, state.Animation);
            playableGraph.Connect(ClipPlayable, 0, rootMixerPlayable, 0);
            rootMixerPlayable.SetInputWeight(0, 1);
            ClipPlayable.Play();
        }
    }
}

