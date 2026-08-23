using System;
using OriginCore.Content;
using UnityEngine;

namespace OriginCore.Matches
{
    [DisallowMultipleComponent]
    public sealed class HeroDeploymentController : MonoBehaviour
    {
        [SerializeField] private HeroDefinition _heroDefinition;
        [Min(0f), SerializeField] private float _totalSeconds;
        [Min(0f), SerializeField] private float _remainingSeconds;
        [SerializeField] private bool _deploying;

        private MatchSessionService _matchSession;
        private float _nextRetryTime;

        public event Action<HeroDeploymentController> DeploymentCompleted;

        public HeroDefinition HeroDefinition => _heroDefinition;
        public bool IsDeploying => _deploying;
        public float TotalSeconds => Mathf.Max(0f, _totalSeconds);
        public float RemainingSeconds => Mathf.Max(0f, _remainingSeconds);
        public float NormalizedProgress => _totalSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(1f - _remainingSeconds / _totalSeconds);

        private void Update()
        {
            if (!_deploying || Time.deltaTime <= 0f)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.deltaTime);
            if (_remainingSeconds > 0f || Time.unscaledTime < _nextRetryTime)
            {
                return;
            }

            _nextRetryTime = Time.unscaledTime + 1f;
            if (_matchSession == null || !_matchSession.TryCompleteInitialHeroDeployment(this))
            {
                return;
            }

            _deploying = false;
            DeploymentCompleted?.Invoke(this);
        }

        public void Configure(
            MatchSessionService matchSession,
            HeroDefinition heroDefinition,
            float totalSeconds,
            float remainingSeconds)
        {
            _matchSession = matchSession;
            _heroDefinition = heroDefinition;
            _totalSeconds = Mathf.Max(0f, totalSeconds);
            _remainingSeconds = Mathf.Clamp(remainingSeconds, 0f, _totalSeconds);
            _deploying = _heroDefinition != null;
            _nextRetryTime = 0f;
        }
    }
}
