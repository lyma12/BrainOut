using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class EndNodeView : Node
{
    public Port InputPort { get; private set; }

    private static readonly Color NodeColor = new Color(0.45f, 0.08f, 0.08f);

    public EndNodeView(Vector2 position)
    {
        title = "■  END";
        titleContainer.style.backgroundColor = new StyleColor(NodeColor);

        // Cannot be deleted
        capabilities &= ~Capabilities.Deletable;

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "";
        inputContainer.Add(InputPort);

        SetPosition(new Rect(position, Vector2.zero));
        RefreshExpandedState();
        RefreshPorts();
    }
}
