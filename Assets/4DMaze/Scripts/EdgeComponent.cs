using UnityEngine;

public class EdgeComponent : MonoBehaviour {
	public float RADIUS_THRESHOLD = .01f;

	private Color _color;
	public Color color { get { return _color; } set { _color = value; UpdateColor(); } }

	public Renderer Renderer;

	public NodeComponent Node1;
	public NodeComponent Node2;

	public void UpdatePosition() {
		if (!Node1.Visible || !Node2.Visible) {
			gameObject.SetActive(false);
			return;
		}
		transform.localPosition = (Node1.transform.localPosition + Node2.transform.localPosition) / 2f;
		Vector3 diff = Node2.transform.localPosition - Node1.transform.localPosition;
		float radius = 0.9f * Mathf.Max(Vector3MaxDim(Node1.transform.localScale), Vector3MaxDim(Node2.transform.localScale));
		if (radius < RADIUS_THRESHOLD) {
			transform.localScale = Vector3.zero;
			return;
		}
		transform.localScale = new Vector3(radius, radius, diff.magnitude);
		transform.localRotation = Quaternion.LookRotation(diff, Vector3.up);
	}

	public void UpdateColor() {
		Renderer.material.color = color;
	}

	private static float Vector3MaxDim(Vector3 v) {
		return Mathf.Max(new[] { v.x, v.y, v.z });
	}
}
