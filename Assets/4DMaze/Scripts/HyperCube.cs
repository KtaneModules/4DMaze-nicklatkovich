using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HyperCube : MonoBehaviour {
	public const float SIZE = 0.4f;

	public NodeComponent NodePrefab;
	public EdgeComponent EdgePrefab;

	public HashSet<Vector4Int> renderedCubes = new HashSet<Vector4Int>();
	public Vector4Int id;
	// public bool destroy = false;
	// public float distructionAnim = 1f;

	private Vector4 _pos;
	public Vector4 pos { get { return _pos; } set { _pos = value; if (_nodes == null) Start(); else UpdateNodesPositions(); } }

	private Color _color;
	public Color color { get { return _color; } set { _color = value; UpdateColor(); } }

	private NodeComponent[] _nodes;
	private List<EdgeComponent> _edges = new List<EdgeComponent>();

	private void Start() {
		if (_nodes != null) return;
		_nodes = new NodeComponent[16];
		for (int i = 0; i < 16; i++) {
			_nodes[i] = Instantiate(NodePrefab);
			_nodes[i].transform.parent = transform;
			_nodes[i].transform.localPosition = Vector3.zero;
			_nodes[i].transform.localScale = Vector3.one;
			_nodes[i].transform.localRotation = Quaternion.identity;
		}
		for (int i = 0; i < 16; i += 2) _edges.Add(CreateEdge(i, i + 1));
		for (int i = 0; i < 16; i += 4) for (int j = 0; j < 2; j++) _edges.Add(CreateEdge(i + j, i + j + 2));
		for (int i = 0; i < 16; i += 8) for (int j = 0; j < 4; j++) _edges.Add(CreateEdge(i + j, i + j + 4));
		for (int i = 0; i < 8; i++) _edges.Add(CreateEdge(i, i + 8));
		UpdateNodesPositions();
	}

	private void Update() {
		// if (destroy) distructionAnim = Mathf.Max(0, distructionAnim - Time.deltaTime);
		// else distructionAnim = Mathf.Min(1, distructionAnim + Time.deltaTime);
		// foreach (NodeComponent node in _nodes) node.distructionAnim = distructionAnim;
	}

	private EdgeComponent CreateEdge(int from, int to) {
		EdgeComponent edge = Instantiate(EdgePrefab);
		edge.transform.parent = transform;
		edge.Node1 = _nodes[from];
		edge.Node2 = _nodes[to];
		return edge;
	}

	public void Render(Vector4 observer, FourDimRotation lookRotation) {
		foreach (NodeComponent node in _nodes) node.Visible = false;
		if (renderedCubes.Contains(Vector4Int.left) && observer.x < pos.x - SIZE) for (int i = 0; i < 16; i += 2) _nodes[i].Visible = true;
		else if (renderedCubes.Contains(Vector4Int.right) && observer.x > pos.x + SIZE) for (int i = 1; i < 16; i += 2) _nodes[i].Visible = true;
		if (renderedCubes.Contains(Vector4Int.down) && observer.y < pos.y - SIZE) for (int i = 0; i < 16; i += 4) for (int j = 0; j < 2; j++) _nodes[i + j].Visible = true;
		else if (renderedCubes.Contains(Vector4Int.up) && observer.y > pos.y + SIZE) for (int i = 2; i < 16; i += 4) for (int j = 0; j < 2; j++) _nodes[i + j].Visible = true;
		if (renderedCubes.Contains(Vector4Int.back) && observer.z < pos.z - SIZE) for (int i = 0; i < 16; i += 8) for (int j = 0; j < 4; j++) _nodes[i + j].Visible = true;
		else if (renderedCubes.Contains(Vector4Int.front) && observer.z > pos.z + SIZE) {
			for (int i = 4; i < 16; i += 8) for (int j = 0; j < 4; j++) _nodes[i + j].Visible = true;
		}
		if (renderedCubes.Contains(Vector4Int.kata) && observer.w < pos.w - SIZE) for (int i = 0; i < 8; i++) _nodes[i].Visible = true;
		else if (renderedCubes.Contains(Vector4Int.ana) && observer.w > pos.w + SIZE) for (int i = 8; i < 16; i++) _nodes[i].Visible = true;
		foreach (NodeComponent node in _nodes) node.Render(observer, lookRotation);
		foreach (EdgeComponent edge in _edges) edge.UpdatePosition();
	}

	private void UpdateNodesPositions() {
		for (int i = 0; i < 16; i++) {
			float x = i % 2 == 0 ? -SIZE : SIZE;
			float y = i / 2 % 2 == 0 ? -SIZE : SIZE;
			float z = i / 4 % 2 == 0 ? -SIZE : SIZE;
			float w = i / 8 % 2 == 0 ? -SIZE : SIZE;
			_nodes[i].pos = pos + new Vector4(x, y, z, w);
		}
	}

	private void UpdateColor() {
		foreach (NodeComponent node in _nodes) node.color = color;
		foreach (EdgeComponent edge in _edges) edge.color = color;
	}
}
