using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 2D 正交割草场：镜头钉在原点，小队居中，敌人从圆环刷入。
    /// XY 是平面，Z 恒为 0。不跟角色、不做横版卷轴。
    /// </summary>
    public static class BattleField
    {
        public const float CameraSize = 6.2f;
        public const float SquadRadius = 1.45f;
        public const float SpawnRadius = 5.0f;
        public const float PlaneZ = 0f;

        public static Vector3 SquadSlot(int index, int count)
        {
            if (count <= 1) return new Vector3(0f, 0f, PlaneZ);
            float ang = (Mathf.PI * 2f * index / count) - Mathf.PI * 0.5f;
            return new Vector3(Mathf.Cos(ang) * SquadRadius, Mathf.Sin(ang) * SquadRadius, PlaneZ);
        }

        public static Vector3 EnemySpawnOnRing()
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = SpawnRadius + Random.Range(-0.25f, 0.35f);
            return new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, PlaneZ);
        }

        public static Vector3 ClampToArena(Vector3 pos)
        {
            pos.z = PlaneZ;
            float max = SpawnRadius + 0.6f;
            if (pos.sqrMagnitude > max * max)
                pos = (Vector3)((Vector2)pos).normalized * max;
            pos.z = PlaneZ;
            return pos;
        }

        public static void ApplyCamera(Camera cam)
        {
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.orthographicSize = CameraSize;
            cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }
    }
}
