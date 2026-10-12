using System;
using System.Collections.Generic;
using UnityEngine;

namespace Apagao.Systems
{
    public enum NoiseType { Passo, Queda, Impacto, Distracao, Arma, Explosao, Eletrico }

    /// <summary>Um som relevante para a IA. Raio em metros, intensidade de 0 a 1.</summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly float Intensity;
        public readonly NoiseType Type;
        public readonly GameObject Emitter;

        public NoiseEvent(Vector3 position, float radius, float intensity, NoiseType type, GameObject emitter)
        {
            Position = position;
            Radius = radius;
            Intensity = Mathf.Clamp01(intensity);
            Type = type;
            Emitter = emitter;
        }
    }

    public interface INoiseListener
    {
        void OnNoise(in NoiseEvent e);
    }

    /// <summary>
    /// Distribui eventos de ruído para todos os ouvintes registrados (spec 06).
    /// Cada ouvinte decide se ouviu (distância, paredes).
    /// </summary>
    public static class NoiseSystem
    {
        private static readonly List<INoiseListener> listeners = new List<INoiseListener>();

        /// <summary>Disparado depois de cada emissão (debug, áudio).</summary>
        public static event Action<NoiseEvent> Emitted;

        // Fast Enter Play Mode: estáticos não são resetados sozinhos.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            listeners.Clear();
            Emitted = null;
        }

        public static void Register(INoiseListener listener)
        {
            if (!listeners.Contains(listener)) listeners.Add(listener);
        }

        public static void Unregister(INoiseListener listener) => listeners.Remove(listener);

        public static void Emit(Vector3 position, float radius, float intensity, NoiseType type, GameObject emitter = null) =>
            Emit(new NoiseEvent(position, radius, intensity, type, emitter));

        public static void Emit(in NoiseEvent e)
        {
            // De trás para frente: um ouvinte pode se remover ao reagir.
            for (int i = listeners.Count - 1; i >= 0; i--)
                if (i < listeners.Count) listeners[i].OnNoise(e);

            Emitted?.Invoke(e);
#if UNITY_EDITOR
            DrawDebugCircle(e.Position, e.Radius, e.Intensity);
#endif
        }

#if UNITY_EDITOR
        private static void DrawDebugCircle(Vector3 center, float radius, float intensity)
        {
            const int segments = 32;
            var color = Color.Lerp(new Color(0.4f, 0.8f, 1f), new Color(1f, 0.3f, 0.2f), intensity);
            Vector3 prev = center + new Vector3(radius, 0.1f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0.1f, Mathf.Sin(a) * radius);
                Debug.DrawLine(prev, next, color, 1f);
                prev = next;
            }
        }
#endif
    }
}
