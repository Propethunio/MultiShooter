using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Presets;
using UnityEditor;
#endif
using System.IO;

namespace cowsins
{
    public static class CowsinsUtilities
    {
        /// <summary>
        /// Returns a Vector3 that applies spread to the bullets shot
        /// </summary>
        public static Vector3 GetSpreadDirection(float amount, Camera camera)
        {
            var horSpread = Random.Range(-amount, amount);
            var verSpread = Random.Range(-amount, amount);
            var spread = camera.transform.InverseTransformDirection(new Vector3(horSpread, verSpread, 0));
            var dir = camera.transform.forward + spread;

            return dir;
        }

        public static void PlayAnim(string anim, Animator animator)
        {
            animator.SetTrigger(anim);
        }

        public static void ForcePlayAnim(string anim, Animator animator)
        {
            animator.Play(anim, 0, 0);
        }

        public static void StartAnim(string anim, Animator animated) => animated.SetBool(anim, true);

        public static void StopAnim(string anim, Animator animated) => animated.SetBool(anim, false);
#if UNITY_EDITOR
        public static void SavePreset(Object source, string name)
        {
            if (EmptyString(name))
            {
                Debug.LogError("ERROR: Do not forget to give your preset a name!");
                return;
            }

            var preset = new Preset(source);

            const string directoryPath = "Assets/" + "Cowsins/" + "CowsinsPresets/";

            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            AssetDatabase.CreateAsset(preset, directoryPath + name + ".preset");
            Debug.Log("Preset successfully saved");
        }

        public static void ApplyPreset(Preset preset, Object target)
        {
            preset.ApplyTo(target);
        }

        public static bool IsUsingUnity6()
        {
            var unityVersion = Application.unityVersion;

            var versionParts = unityVersion.Split('.');

            if (versionParts.Length <= 0 || !int.TryParse(versionParts[0], out var majorVersion)) return false;
            return majorVersion >= 6000 && !EditorPrefs.GetBool("Unity6EditorWindowDontShowAgain", false);
        }
#endif
        public static bool EmptyString(string string_)
        {
            if (string_.Length == 0) return true;
            var i = 0;
            while (i < string_.Length)
            {
                if (string_[i].ToString() == " ") return true;
                i++;
            }

            return false;
        }

        public static IDamageable GatherDamageableParent(Transform child)
        {
            var parent = child.transform.parent;
            while (parent != null)
            {
                var component = parent.GetComponent<IDamageable>();
                if (component != null)
                {
                    return component;
                }

                parent = parent.parent;
            }

            return null;
        }
    }
}