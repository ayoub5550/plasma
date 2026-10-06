using System.IO;
using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Loads the baked CC0 character flipbooks from Resources/Models/*.bytes (made by tools/models/bake_glb.py):
    /// one static mesh per animation frame, vertex colour = atlas colour, vertex alpha = tint mask, so they
    /// instance with Plasma/Lit like the procedural meshes. Height is 1 unit, feet at y=0, facing +z.
    /// Returns null when a file is missing so callers can fall back to MeshFactory.
    /// </summary>
    public static class ModelLibrary
    {
        static Mesh[] _soldier, _enemy, _boss;
        static readonly System.Collections.Generic.Dictionary<string, float> _clip = new System.Collections.Generic.Dictionary<string, float>();

        /// <summary>Length in seconds of the baked animation clip (frames are evenly spaced over it).</summary>
        public static float ClipSeconds(string name) { Load(); return _clip.TryGetValue(name, out var s) && s > 0.05f ? s : 1f; }
        static bool _tried;

        public static Mesh[] Soldier { get { Load(); return _soldier; } }
        public static Mesh[] Enemy { get { Load(); return _enemy; } }
        public static Mesh[] Boss { get { Load(); return _boss; } }
        public static bool Available { get { Load(); return _soldier != null && _enemy != null && _boss != null; } }

        static void Load()
        {
            if (_tried) return;
            _tried = true;
            _soldier = Read("soldier");
            _enemy = Read("enemy");
            _boss = Read("boss");
        }

        static Mesh[] Read(string name)
        {
            var ta = Resources.Load<TextAsset>("Models/" + name);
            if (ta == null) { Debug.LogWarning("[Plasma] model missing: " + name); return null; }
            using (var r = new BinaryReader(new MemoryStream(ta.bytes)))
            {
                if (new string(r.ReadChars(4)) != "PLM1") { Debug.LogWarning("[Plasma] bad model: " + name); return null; }
                int frames = r.ReadInt32(), verts = r.ReadInt32(), indices = r.ReadInt32();
                _clip[name] = r.ReadSingle();
                var tris = new int[indices];
                for (int i = 0; i < indices; i++) tris[i] = r.ReadUInt16();
                var cols = new Color32[verts];
                for (int i = 0; i < verts; i++) cols[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                var meshes = new Mesh[frames];
                var pos = new Vector3[verts];
                var nor = new Vector3[verts];
                for (int f = 0; f < frames; f++)
                {
                    for (int i = 0; i < verts; i++) pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    for (int i = 0; i < verts; i++) nor[i] = new Vector3(r.ReadSByte(), r.ReadSByte(), r.ReadSByte()) / 127f;
                    var m = new Mesh { name = name + "_" + f };
                    m.vertices = pos; m.normals = nor; m.colors32 = cols; m.triangles = tris;
                    m.RecalculateBounds();
                    m.UploadMeshData(true);
                    meshes[f] = m;
                }
                return meshes;
            }
        }
    }
}
