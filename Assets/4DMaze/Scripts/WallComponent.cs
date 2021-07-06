using UnityEngine;

public class WallComponent : MonoBehaviour {
	private Color _color;
	public Color color { get { return _color; } set { _color = value; UpdateColor(); } }

	public Renderer Renderer;

	private void UpdateColor() {
		Renderer.material.color = color;
	}
}
