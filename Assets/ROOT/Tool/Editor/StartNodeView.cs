using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class StartNodeView : Node
{
    public Port OutputPort { get; private set; }

    private static readonly Color NodeColor = new Color(0.07f, 0.35f, 0.12f);

    public StartNodeView(Vector2 position)
    {
        title = "▶  START";
        titleContainer.style.backgroundColor = new StyleColor(NodeColor);

        // Cannot be deleted
        capabilities &= ~Capabilities.Deletable;

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        OutputPort.portName = "";
        outputContainer.Add(OutputPort);

        SetPosition(new Rect(position, Vector2.zero));
        RefreshExpandedState();
        RefreshPorts();
    }
}
