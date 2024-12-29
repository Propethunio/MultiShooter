using cowsins;
using UnityEngine;
using HEAVYART.TopDownShooter.Netcode;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WeaponSway : MonoBehaviour
{
    #region shared

    [System.Serializable]
    public enum SwayMethod
    {
        Simple,
    }

    public SwayMethod swayMethod;

    private delegate void Sway();
    private Sway sway;
    
    #endregion

    #region simple

    [Header("Position")] [SerializeField] private float amount = 0.02f;
    [SerializeField] private float maxAmount = 0.06f;
    [SerializeField] private float smoothAmount = 6f;

    [Header("Tilting")] [SerializeField] private float tiltAmount = 4f;
    [SerializeField] private float maxTiltAmount = 5f;
    [SerializeField] private float smoothTiltAmount = 12f;
    private WeaponController weaponconroller;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float InputX;
    private float InputY;
    private float playerMultiplier;

    #endregion

    #region pivotBased

    [SerializeField] private Transform pivot;
    [SerializeField] private float swaySpeed;
    [SerializeField] private Vector2 swayMovementAmount;
    [SerializeField] private Vector2 swayRotationAmount;
    [SerializeField] private float swayTiltAmount;
    private PlayerMovement player;

    #endregion

    private void Start()
    {
        if (swayMethod != SwayMethod.Simple) return;
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
        var topParent = GetTopParent(gameObject);
        weaponconroller = topParent.GetComponent<WeaponController>();
        player = topParent.GetComponent<PlayerMovement>();
        sway = SimpleSway;
    }

    private static GameObject GetTopParent(GameObject obj)
    {
        var current = obj.transform;

        while (current.parent != null)
        {
            current = current.parent;
        }

        return current.gameObject;
    }

    private void Update()
    {
        if (!PlayerStats.Controllable) return;
        sway?.Invoke();
    }

    private void SimpleSway()
    {
        CalculateSway();
        MoveSway();
        TiltSway();
    }

    private void CalculateSway()
    {
        InputX = -player.mousex / 10 - 2;
        InputY = -player.mousey / 10 - 2;

        playerMultiplier = weaponconroller.isAiming ? 5f : 1f;
    }

    private void MoveSway()
    {
        var moveX = Mathf.Clamp(InputX * amount, -maxAmount, maxAmount) / playerMultiplier;
        var moveY = Mathf.Clamp(InputY * amount, -1, 1) / playerMultiplier;

        var finalPosition = new Vector3(moveX, moveY, 0);

        transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition + initialPosition,
            Time.fixedDeltaTime * smoothAmount * playerMultiplier);
    }

    private void TiltSway()
    {
        var moveX = Mathf.Clamp(InputX * tiltAmount, -maxTiltAmount, maxTiltAmount) / playerMultiplier;
        var finalRotation = Quaternion.Euler(0, 0, moveX);

        transform.localRotation = Quaternion.Lerp(transform.localRotation, finalRotation * initialRotation,
            Time.fixedDeltaTime * smoothTiltAmount * playerMultiplier);
    }
}
#if UNITY_EDITOR
[CustomEditor(typeof(WeaponSway))]
public class WeaponSwayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var myScript = target as WeaponSway;

        EditorGUILayout.LabelField("WEAPON SWAY", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("swayMethod"));
        EditorGUILayout.Space(10f);

        if (myScript != null && myScript.swayMethod == WeaponSway.SwayMethod.Simple)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("POSITION");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("amount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothAmount"));
            EditorGUILayout.LabelField("ROTATION");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTiltAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothTiltAmount"));
        }
        else
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pivot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swaySpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swayMovementAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swayRotationAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swayTiltAmount"));
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5f);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif