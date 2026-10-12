using System;
using UnityEngine;

namespace Apagao.AI
{
    /// <summary>
    /// Estado base. Cada estado é uma classe com Enter, Tick e Exit.
    /// TOwner é quem o estado controla (ex.: SentinelaAI).
    /// </summary>
    public abstract class State<TOwner>
    {
        protected TOwner Owner { get; }

        protected State(TOwner owner) => Owner = owner;

        /// <summary>Nome exibido no debug.</summary>
        public virtual string Name => GetType().Name;

        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    /// <summary>
    /// Máquina de estados genérica, reaproveitada por todos os inimigos.
    /// Guarda o estado anterior para estados temporários (ex.: Paralisado pelo PEM volta ao anterior).
    /// </summary>
    public class StateMachine<TOwner>
    {
        public State<TOwner> Current { get; private set; }
        public State<TOwner> Previous { get; private set; }
        public float TimeInState { get; private set; }

        /// <summary>Disparado a cada troca: (anterior, novo).</summary>
        public event Action<State<TOwner>, State<TOwner>> Changed;

        public void ChangeState(State<TOwner> next)
        {
            if (next == null || next == Current) return;

            Current?.Exit();
            Previous = Current;
            Current = next;
            TimeInState = 0f;
            Current.Enter();
            Changed?.Invoke(Previous, Current);
        }

        /// <summary>Sai e entra de novo no estado atual (ex.: retomar depois da paralisia).</summary>
        public void Restart()
        {
            if (Current == null) return;
            Current.Exit();
            TimeInState = 0f;
            Current.Enter();
        }

        public void Tick()
        {
            if (Current == null) return;
            TimeInState += Time.deltaTime;
            Current.Tick();
        }
    }
}
