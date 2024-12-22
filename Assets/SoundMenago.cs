using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class SoundMenago : NetworkBehaviour {

    // Public method to request playing a sound
    public void PlaySound(AudioClip clip, float delay, float pitchAdded, bool randomPitch, float spatialBlend, Vector3 soundPosition, bool ohterThanFire = true) {
        if(IsHost || IsClient) {
            // Send the request to the server

            PlaySoundServerRpc(clip.name, delay, pitchAdded, randomPitch, 1f, soundPosition, ohterThanFire);
        }
    }

    // Server handles the request and broadcasts to clients
    [ServerRpc(RequireOwnership = false)]
    private void PlaySoundServerRpc(string clipName, float delay, float pitchAdded, bool randomPitch, float spatialBlend, Vector3 soundPosition, bool ohterThanFire = true) {
        // Passes the sound data to all clients
        PlaySoundClientRpc(clipName, delay, pitchAdded, randomPitch, spatialBlend, soundPosition, ohterThanFire);
    }

    // Clients play the sound at the specified position
    [ClientRpc]
    private void PlaySoundClientRpc(string clipName, float delay, float pitchAdded, bool randomPitch, float spatialBlend, Vector3 soundPosition, bool ohterThanFire = true) {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + clipName);
        if(clip != null) {
            StartCoroutine(Play(clip, delay, pitchAdded, randomPitch, spatialBlend, soundPosition, ohterThanFire));
        }
    }

    // Coroutine to handle delayed playback
    private IEnumerator Play(AudioClip clip, float delay, float pitch, bool randomPitch, float spatialBlend, Vector3 position, bool ohterThanFire = true) {
        if(!clip) yield break;

        yield return new WaitForSeconds(delay);

        GameObject tempAudioSourceObj = new GameObject("TempAudioSource_" + clip.name + "_" + Time.time);
        tempAudioSourceObj.transform.position = position;

        AudioSource tempSrc = tempAudioSourceObj.AddComponent<AudioSource>();
        tempSrc.spatialBlend = spatialBlend;

        float pitchVariation = randomPitch ? Random.Range(-pitch, pitch) : pitch;
        tempSrc.pitch = 1 + pitchVariation;

        if(ohterThanFire) {
            tempSrc.rolloffMode = AudioRolloffMode.Custom;
            tempSrc.minDistance = 1f;
            tempSrc.maxDistance = 30f;

            AnimationCurve customCurve = new AnimationCurve();
            customCurve.AddKey(0f, 1f);
            customCurve.AddKey(5f, .75f);
            customCurve.AddKey(15f, .3f);
            customCurve.AddKey(30f, 0f);

            tempSrc.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customCurve);
        }

        tempSrc.PlayOneShot(clip);

        Destroy(tempAudioSourceObj, clip.length + delay);
    }
}