using UnityEngine;

public partial class Monster
{
    private void SpawnUltimateImpactVfx()
    {
        if (ultimateImpactVfxPrefab == null) return;

        Vector3 impactPoint = Player.transform.position;
        Collider playerCollider = Player.GetComponent<Collider>();
        if (playerCollider != null) impactPoint.y = playerCollider.bounds.min.y;

        GameObject vfx = Instantiate(ultimateImpactVfxPrefab, impactPoint, Quaternion.identity);

        float speed = Mathf.Max(0.01f, ultimateImpactVfxSpeed);
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.simulationSpeed = speed;
        }

        Destroy(vfx, ultimateImpactVfxLifetime / speed);
    }
}
