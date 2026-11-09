using System.Runtime.CompilerServices;
using UnityEngine;

// 1.7 的 Assembly-CSharp 使用 UnityStubs 里的 UnityEngine 类型；
// CE 程序集则引用 UnityEngine.CoreModule。这里做编译期类型转发，
// 让 GrayRaceCE 能同时使用两边而不需要把 UnityStubs 改名。
[assembly: TypeForwardedTo(typeof(Vector2))]
[assembly: TypeForwardedTo(typeof(Vector3))]
[assembly: TypeForwardedTo(typeof(Vector4))]
[assembly: TypeForwardedTo(typeof(Vector2Int))]
[assembly: TypeForwardedTo(typeof(Vector3Int))]
[assembly: TypeForwardedTo(typeof(Quaternion))]
[assembly: TypeForwardedTo(typeof(Matrix4x4))]
[assembly: TypeForwardedTo(typeof(Color))]
[assembly: TypeForwardedTo(typeof(Color32))]
[assembly: TypeForwardedTo(typeof(Rect))]
[assembly: TypeForwardedTo(typeof(RectInt))]
[assembly: TypeForwardedTo(typeof(Bounds))]
[assembly: TypeForwardedTo(typeof(BoundsInt))]
[assembly: TypeForwardedTo(typeof(Mathf))]
