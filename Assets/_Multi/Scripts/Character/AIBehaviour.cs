using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class AIBehaviour : NetworkBehaviour
    {
        public string navmeshAgentName = "Humanoid";

        private const float SmoothMovementTime = 0.1f;

        private WeaponControlSystem _weaponControlSystem;
        private HealthController _healthController;
        private PlayerMovement _rigidbodyCharacterController;
        private CharacterIdentityControl _identityControl;

        private float _distanceToOpenFire;
        private float _targetUpdateRate;
        private float _maneuverAngle;
        private float _minDistanceOfManeuver;
        private float _maxDistanceOfManeuver;
        private float _minDistanceToUpdateManeuverPoint;
        private float _maneuverExitTime;

        private Vector3 _movementVelocity = Vector3.zero;
        private Vector3 _currentMovementInput;

        private Transform _targetTransform;
        private Vector3 _moveToPoint;
        private float _lastPointUpdateTime;

        private void Start()
        {
            _weaponControlSystem = GetComponent<WeaponControlSystem>();
            _healthController = GetComponent<HealthController>();
            _rigidbodyCharacterController = GetComponent<PlayerMovement>();
            _identityControl = GetComponent<CharacterIdentityControl>();

            var modelIndex = _identityControl.spawnParameters.Value.ModelIndex;
            var config = SettingsManager.Instance.ai.configs[modelIndex];

            _healthController.Initialize(config.health);
            _healthController.OnDeath += () => GetComponent<CharacterEffectsController>().RunDestroyScenario(false);

            GameManager.Instance.userControl.AddAIObject(NetworkObject);

            _distanceToOpenFire = config.distanceToOpenFire;
            _targetUpdateRate = config.targetUpdateRate;

            _maneuverAngle = config.maneuverAngle;
            _minDistanceOfManeuver = config.minDistanceOfManeuver;
            _maxDistanceOfManeuver = config.maxDistanceOfManeuver;
            _minDistanceToUpdateManeuverPoint = config.minDistanceToUpdateManeuverPoint;
            _maneuverExitTime = config.maneuverExitTime;

            gameObject.name = config.botPrefab.name;

            StartCoroutine(RunUpdateTargetLoop());
        }

        private void FixedUpdate()
        {
            if (IsOwner == false) return;

            if (GameManager.Instance.gameState == GameState.GameIsOver) _rigidbodyCharacterController.Stop();

            if (_healthController.isAlive == false) _rigidbodyCharacterController.Stop();

            if (GameManager.Instance.gameState != GameState.ActiveGame) return;

            if (_targetTransform == null) return;
            var lookDirection = _targetTransform.position - transform.position;
            lookDirection.y = 0;

            _weaponControlSystem.lineOfSightTransform.localRotation = Quaternion.LookRotation(lookDirection);

            var moveDirection = _moveToPoint - transform.position;

            if (moveDirection.magnitude < _minDistanceToUpdateManeuverPoint ||
                Time.time > _lastPointUpdateTime + _maneuverExitTime)
            {
                var maneuverPoint = CalculateManeuverPoint();
                _moveToPoint = GetNextNavigationPoint(maneuverPoint);
                moveDirection = _moveToPoint - transform.position;
                _lastPointUpdateTime = Time.time;
            }

            _currentMovementInput = Vector3.SmoothDamp(_currentMovementInput, moveDirection.normalized,
                ref _movementVelocity, SmoothMovementTime);
            if (lookDirection.magnitude < _distanceToOpenFire)
                _weaponControlSystem.Fire();
        }

        private Vector3 CalculateManeuverPoint()
        {
            var halfAngle = _maneuverAngle * 0.5f;
            var lookRotation = Quaternion.LookRotation(transform.position - _targetTransform.position);
            lookRotation *= Quaternion.Euler(0, Random.Range(-halfAngle, halfAngle), 0);

            return _targetTransform.position + (lookRotation * Vector3.forward) *
                Random.Range(_minDistanceOfManeuver, _maxDistanceOfManeuver);
        }

        private IEnumerator RunUpdateTargetLoop()
        {
            while (_healthController.isAlive)
            {
                _targetTransform = FindNearestTarget();
                yield return new WaitForSeconds(_targetUpdateRate);
            }
        }

        private Transform FindNearestTarget()
        {
            var targets = GameManager.Instance.userControl.allCharacters;

            Transform nearestTarget = null;
            var minDistance = float.MaxValue;

            foreach (var t in targets)
            {
                if (t == null || t.transform == transform) continue;

                var distance = (transform.position - t.transform.position).magnitude;

                if (!(distance < minDistance)) continue;
                nearestTarget = t.transform;
                minDistance = distance;
            }

            return nearestTarget;
        }

        private Vector3 GetNextNavigationPoint(Vector3 targetPoint)
        {
            var path = new NavMeshPath();

            var navMeshQueryFilter = new NavMeshQueryFilter
            {
                areaMask = NavMesh.AllAreas,
                agentTypeID = GetAgentIdByName(navmeshAgentName)
            };

            const float maxDistanceFromNavMesh = 1;

            if (NavMesh.SamplePosition(targetPoint, out var hit, maxDistanceFromNavMesh, navMeshQueryFilter))
            {
                NavMesh.CalculatePath(transform.position, hit.position, navMeshQueryFilter, path);
            }
            else
                Debug.Log("Unable to calculate path. Target point is too far from NavMesh.");

            if (Application.isEditor)
                DrawPath(path.corners, 1, Color.red);

            return path.corners.Length > 1 ? path.corners[1] : targetPoint;
        }

        private static void DrawPath(Vector3[] corners, float duration, Color color)
        {
            for (var i = 0; i < corners.Length - 1; i++)
            {
                Debug.DrawLine(corners[i], corners[i + 1], color, duration);
            }
        }

        private static int GetAgentIdByName(string agentName)
        {
            var count = NavMesh.GetSettingsCount();
            for (var i = 0; i < count; i++)
            {
                var id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                var name = NavMesh.GetSettingsNameFromID(id);

                if (name == agentName)
                    return id;
            }

            Debug.Log("Agent name: " + agentName + " does not exists.");
            return 0;
        }
    }
}