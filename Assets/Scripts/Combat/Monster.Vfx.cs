using UnityEngine;

// Monster가 띄우는 일회성 이펙트. 전투 로직과 달리 여기서 하는 일은 "프리팹을 스폰하고,
// 속도/수명을 맞춰준 뒤, 알아서 사라지게 두는 것"뿐이라 따로 뒀다.
public partial class Monster
{
    // 지속되는 자식 오브젝트 대신 매번 새로 스폰한다 - 플레이어 자신의 피격 VFX와 달리
    // 여러 몬스터가 동시에 같은 플레이어에게 적중시킬 수 있어서, 공유 자식이라면 깔끔하게
    // 겹쳐 쌓이는 대신 하나의 Play()/Stop()을 두고 서로 다투게 된다.
    private void SpawnUltimateImpactVfx()
    {
        if (ultimateImpactVfxPrefab == null) return;

        // 크레이터/버스트가 이펙트의 원점에 위치하므로 그 원점이 지면에 닿아야 한다.
        // 플레이어 transform은 발이 아니라 캡슐 중심이라, 거기 바로 스폰하면 크리스탈이
        // 허리 높이에서 터져 다 떨어지기도 전에 터지는 것처럼 보였다.
        Vector3 impactPoint = Player.transform.position;
        Collider playerCollider = Player.GetComponent<Collider>();
        if (playerCollider != null) impactPoint.y = playerCollider.bounds.min.y;

        GameObject vfx = Instantiate(ultimateImpactVfxPrefab, impactPoint, Quaternion.identity);

        // Hovl 이펙트들은 데모 씬에서 계속 실행되도록 반복 재생이라, 한 번만 써도 파괴 타이머
        // 도중에 낙하 연출 전체가 재시작되어 두 번 발동한 것처럼 보인다. 한 번 사용 = 한 사이클.
        float speed = Mathf.Max(0.01f, ultimateImpactVfxSpeed);
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            // 시스템별 설정이라 모든 서브 이미터(크리스탈, 플래시, 스파크, 연기)에 다 넣어야
            // 한다 - 하나라도 1배속으로 남으면 나머지 이펙트보다 뒤처진다.
            main.simulationSpeed = speed;
        }

        // 이펙트가 이제 원래 재생 시간의 일부 만에 끝나버리므로, 정리 타이머도 함께
        // 줄여야 한다. 그렇지 않으면 텅 빈 오브젝트가 그대로 남아있게 된다.
        Destroy(vfx, ultimateImpactVfxLifetime / speed);
    }
}
