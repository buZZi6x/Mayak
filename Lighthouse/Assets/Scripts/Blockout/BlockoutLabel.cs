using UnityEngine;
namespace LighthouseGame
{
    public class BlockoutLabel : MonoBehaviour
    {
        Renderer label;
        BlockoutSession session;
        void Awake() { label = GetComponent<Renderer>(); session=FindAnyObjectByType<BlockoutSession>(); }
        void LateUpdate()
        {
            if (!Camera.main) return;
            Vector3 away = transform.position - Camera.main.transform.position;
            label.enabled = session&&session.HelpVisible&&!session.TakingPhoto&&!session.Player.UIHidden&&away.magnitude < 6.5f && Mathf.Abs(away.y) < 3 && !Physics.Raycast(Camera.main.transform.position, away.normalized, Mathf.Max(0,away.magnitude - .35f), ~(1 << 2), QueryTriggerInteraction.Ignore);
            away.y = 0;
            if (away.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(away);
        }
    }
}
