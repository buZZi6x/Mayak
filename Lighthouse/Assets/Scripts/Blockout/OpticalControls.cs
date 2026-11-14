using UnityEngine;
using UnityEngine.UIElements;

namespace LighthouseGame
{
    [UxmlElement]
    public partial class MenuRule : VisualElement
    {
        public MenuRule()
        {
            pickingMode=PickingMode.Ignore;
            generateVisualContent+=context=>
            {
                var painter=context.painter2D;painter.lineWidth=1;painter.strokeColor=resolvedStyle.color;
                for(float x=0;x<contentRect.width;x+=5)
                {painter.BeginPath();painter.MoveTo(new Vector2(x,.5f));painter.LineTo(new Vector2(Mathf.Min(x+2,contentRect.width),.5f));painter.Stroke();}
            };
        }
    }

    [UxmlElement]
    public partial class OpticalFrame : VisualElement
    {
        public OpticalFrame()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            bool tab = ClassListContains("tab-corners");
            float length = tab ? 11.5f : 23, inset = tab ? 1.5f : 1;
            float width = contentRect.width, height = contentRect.height;
            painter.strokeColor = resolvedStyle.color;
            painter.lineWidth = tab ? 3 : 1.5f;
            foreach (var corner in new[] { new Vector2(inset, inset), new Vector2(width-inset, inset),
                new Vector2(inset, height-inset), new Vector2(width-inset, height-inset) })
            {
                var direction = new Vector2(corner.x < width/2 ? 1 : -1, corner.y < height/2 ? 1 : -1);
                painter.BeginPath(); painter.MoveTo(corner + new Vector2(0, direction.y * length));
                painter.LineTo(corner); painter.LineTo(corner + new Vector2(direction.x * length, 0)); painter.Stroke();
            }
        }
    }

    [UxmlElement]
    public partial class OpticalSlider : Slider
    {
        private static readonly Color Orange = new Color32(245, 154, 66, 255);
        public OpticalSlider() : base(0, 100)
        {
            AddToClassList("optical-slider");
            this.RegisterValueChangedCallback(_ => MarkDirtyRepaint());
            generateVisualContent += Draw;
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Home && evt.keyCode != KeyCode.End) return;
                value = evt.keyCode == KeyCode.Home ? lowValue : highValue;
                evt.StopPropagation();
            });
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = contentRect.width, height = contentRect.height;
            if (width <= 20) return;
            var painter = context.painter2D;
            float left = 10, right = width - 10, y = height * .5f;
            float position = Mathf.Lerp(left, right, Mathf.InverseLerp(lowValue, highValue, value));
            int count = Mathf.Max(8, Mathf.RoundToInt((right - left) / 8.6f));
            painter.lineWidth = 3.5f;
            for (int i = 0; i <= count; i++)
            {
                float x = Mathf.Lerp(left, right, (float)i/count);
                painter.strokeColor = x <= position ? Orange : new Color(0.83f,.82f,.76f,.42f);
                painter.BeginPath(); painter.MoveTo(new Vector2(x,y-5)); painter.LineTo(new Vector2(x,y+5)); painter.Stroke();
            }
            painter.lineWidth = 1.3f; painter.strokeColor = new Color(.96f,.94f,.86f,.75f);
            for (int i = 0; i <= 7; i++)
            {
                float x = Mathf.Lerp(left, right, i/7f);
                painter.BeginPath(); painter.MoveTo(new Vector2(x,y-11)); painter.LineTo(new Vector2(x,y+11)); painter.Stroke();
            }
            painter.fillColor = Orange;
            painter.BeginPath(); painter.MoveTo(new Vector2(position-10,y-24));
            painter.LineTo(new Vector2(position+10,y-24)); painter.LineTo(new Vector2(position,y-5)); painter.ClosePath(); painter.Fill();
        }
    }
}
