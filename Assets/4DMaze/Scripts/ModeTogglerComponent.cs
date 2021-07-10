using UnityEngine;

public class ModeTogglerComponent : MonoBehaviour {
	public Renderer Renderer;
	public Material AdvancedModeMaterial;
	public Material SimpleModeMaterial;
	public KMSelectable Selectable;

	private bool _advanced = true;
	public bool advanced { get { return _advanced; } set { _advanced = value; UpdateMode(); } }

	private void Start() {
		UpdateMode();
	}

	public void UpdateMode() {
		Renderer.material = advanced ? AdvancedModeMaterial : SimpleModeMaterial;
	}
}
