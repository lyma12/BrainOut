using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class RequiredNodeView: Node
{
        public Port InputPort { get; private set; }
        
        private static readonly Color NodeColor = new Color(0.45f, 0.08f, 0.08f);

        public RequiredNodeView()
        {
                title = "■  RequiredNodeView";
                titleContainer.style.backgroundColor = new StyleColor(NodeColor);
                
        }
}