using UnityEngine;

// Animator에 물려 있는 클립의 실제 길이를 이름으로 찾아온다.
//
// 애니메이션 클립이 전부 서드파티 애셋 팩의 FBX 안에 있어서 클립에 애니메이션 이벤트를
// 심을 수 없다(심어도 .fbx.meta에 저장돼 재임포트하면 날아간다). 그래서 차선책으로,
// 타이밍의 원본은 클립에 두고 코드는 그 길이를 읽어서 쓴다 - "몇 초"를 코드에 베껴 적으면
// 클립이 바뀌었을 때 아무도 알려주지 않는다.
//
// Monster와 WeaponSwing이 각자 들고 있던 같은 구현을 여기로 합쳤다.
public static class AnimClipTiming
{
    // fallback은 클립을 이름으로 못 찾았을 때만 쓰는 안전망이다(이름 오타, 컨트롤러 교체 등).
    // 정상 경로가 아니므로 fallback이 쓰이고 있다면 값이 이미 어긋나 있을 수 있다.
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

    // 클립 안의 한 지점(임팩트, 착지 등)을 길이의 비율로 잡는다. 초 단위로 박아두는 것과
    // 달리 클립이 다른 길이로 교체돼도 비례해서 따라간다.
    //
    // 다만 비율 자체는 여전히 사람이 눈으로 맞춘 값이다 - 임팩트가 클립 안에서 비례 이동하지
    // 않는 교체(앞부분 예비동작만 길어진 클립 등)라면 다시 재야 한다. 그것까지 클립이 소유하게
    // 하려면 애니메이션 이벤트가 필요하고, 그건 위에 적은 이유로 지금은 불가능하다.
    public static float ResolveClipTime(Animator animator, string clipName, float fallbackLength, float fraction)
        => ResolveClipLength(animator, clipName, fallbackLength) * fraction;
}
