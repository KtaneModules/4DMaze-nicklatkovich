using UnityEngine;

public class ButtonComponent : MonoBehaviour {
	private string _text;
	public string text { get { return _text; } set { _text = value; UpdateText(); } }

	public TextMesh Text;
	public KMSelectable Selectable;

	private void UpdateText() {
		Text.text = text;
	}
}
