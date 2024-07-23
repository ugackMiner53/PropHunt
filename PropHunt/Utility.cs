using UnityEngine;

namespace PropHunt;

public static class Utility
{
    public static GameObject FindClosestConsole(GameObject origin, float radius)
    {
        Collider2D bestCollider = null;
        float bestDist = 9999;
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(origin.transform.position, radius))
        {
            if (collider.GetComponent<Console>() != null)
            {
                float dist = Vector2.Distance(origin.transform.position, collider.transform.position);
                if (dist < bestDist)
                {
                    bestCollider = collider;
                    bestDist = dist;
                }
            }
        }
        return bestCollider ? bestCollider.gameObject : null;
    }

    public static System.Collections.IEnumerator KillConsoleAnimation()
    {
        if (Constants.ShouldPlaySfx())
        {
            SoundManager.Instance.PlaySound(ShipStatus.Instance.SabotageSound, false, 0.8f);
            HudManager.Instance.FullScreen.color = new Color(1f, 0f, 0f, 0.375f);
            HudManager.Instance.FullScreen.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            HudManager.Instance.FullScreen.gameObject.SetActive(false);
        }
        yield break;
    }
}