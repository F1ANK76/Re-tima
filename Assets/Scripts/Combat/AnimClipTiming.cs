using UnityEngine;

public static class AnimClipTiming
{
    public static float ResolveClipLength(Animator animator, string clipName, float fallback)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == clipName && clip.length > 0f) return clip.length;
            }
        }

        return fallback;
    }

    public static float ResolveClipTime(Animator animator, string clipName, float fallbackLength, float fraction)
        => ResolveClipLength(animator, clipName, fallbackLength) * fraction;
}
