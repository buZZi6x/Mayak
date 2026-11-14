using UnityEngine;

namespace LighthouseGame
{
    public class BlockoutInteractable : MonoBehaviour
    {
        public enum Station { Door, Lamp, Radio, Bed, Telescope }
        public Station Kind;
        public string Label;
        public bool Open;
        Quaternion closed;
        float doorAngle;
        public string Prompt => Kind == Station.Door ? (Open ? "F — закрыть дверь" : "F — открыть дверь")
            : Kind == Station.Radio ? "F — настроить радио"
            : Kind == Station.Bed ? "F — лечь спать" : "F — " + Label;
        void Awake() { closed = transform.rotation; }
        void Update()
        {
            if (Kind == Station.Door)
            {
                doorAngle = Mathf.MoveTowards(doorAngle, Open ? -100 : 0, Time.deltaTime * 100);
                transform.rotation = Quaternion.AngleAxis(doorAngle, Vector3.up) * closed;
            }
        }
        public void Animate(float seconds)
        { foreach (var m in GetComponentsInChildren<BlockoutMotion>()) m.Pulse(seconds); }
        public void Use(BlockoutSession session)
        {
            if (Kind == Station.Door) { Open = !Open; return; }
            if (Kind == Station.Telescope) { Animate(2); session.Notify("ПКМ — приблизить вид. ЛКМ — сделать снимок."); return; }
            session.Work(this);
        }
    }
}
